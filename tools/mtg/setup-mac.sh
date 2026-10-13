#!/bin/zsh
# Set up and check a Mac for MTG transcription (Apple Silicon, CPU only). Safe to run again: it checks first and installs only what is missing.
# Scope: the environment and a transcription check. The automatic watching of a folder (watch.ps1 / run.ps1) is Windows only.
#
# Usage:
#   ./tools/mtg/setup-mac.sh                 # check, install what is missing
#   ./tools/mtg/setup-mac.sh --check-only    # only report; install nothing
#   ./tools/mtg/setup-mac.sh --test          # also transcribe 3 seconds of audio (downloads the small "tiny" model, about 75 MB)
#   ./tools/mtg/setup-mac.sh --transcribe FILE   # transcribe one recording with large-v3 on the CPU (slow: leave it running)
set -u

CHECK_ONLY=0; DO_TEST=0; AUDIO=""; MODEL="large-v3"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --check-only) CHECK_ONLY=1 ;;
    --test) DO_TEST=1 ;;
    --transcribe) shift; AUDIO="${1:-}" ;;
    --model) shift; MODEL="${1:-large-v3}" ;;
    -h|--help) sed -n '2,10p' "$0"; exit 0 ;;
    *) echo "unknown option: $1"; exit 2 ;;
  esac
  shift
done

SCRIPT_DIR="${0:A:h}"
VENV="${MTG_VENV:-$HOME/.mtg-venv}"
typeset -a RESULTS
add() { RESULTS+=("$(printf '%-24s %-10s %s' "$1" "$2" "$3")"); }
say() { print -P "%F{cyan}== $1%f"; }
has() { command -v "$1" >/dev/null 2>&1; }

say "OS / CPU"
if [[ "$(uname -s)" == "Darwin" ]]; then
  if [[ "$(uname -m)" == "arm64" ]]; then add "mac (Apple Silicon)" OK "CPU mode (int8). No NVIDIA GPU is used"; else add "mac (Intel)" WARN "Works on the CPU, but slower"; fi
else
  add "os" WARN "Not macOS ($(uname -s)). Checking the same things anyway"
fi

say "Homebrew"
if has brew; then add "homebrew" OK ""; else add "homebrew" MISSING 'Install from https://brew.sh then run this again (it is not installed automatically)'; fi

brew_install() {  # brew_install <command> <formula>
  local cmd="$1" formula="$2"
  if has "$cmd"; then add "$cmd" OK ""; return; fi
  if (( CHECK_ONLY )) || ! has brew; then add "$cmd" MISSING "brew install $formula"; return; fi
  brew install "$formula" && has "$cmd" && add "$cmd" INSTALLED "" || add "$cmd" FAIL "brew install $formula"
}

say "Tools"
brew_install git git
brew_install ffmpeg ffmpeg
brew_install gh gh
if has python3 && python3 -c 'import sys; sys.exit(0 if sys.version_info >= (3, 10) else 1)'; then
  add "python3 (>=3.10)" OK "$(python3 --version 2>&1)"
elif (( CHECK_ONLY )) || ! has brew; then
  add "python3 (>=3.10)" MISSING "brew install python@3.12"
else
  brew install python@3.12 && add "python3 (>=3.10)" INSTALLED "$(python3 --version 2>&1)" || add "python3 (>=3.10)" FAIL "brew install python@3.12"
fi

say "faster-whisper (virtual environment: $VENV)"
if [[ -x "$VENV/bin/python" ]] && "$VENV/bin/python" -c 'import faster_whisper' >/dev/null 2>&1; then
  add "faster-whisper" OK "$VENV"
elif (( CHECK_ONLY )); then
  add "faster-whisper" MISSING "run this script without --check-only"
elif has python3; then
  [[ -x "$VENV/bin/python" ]] || python3 -m venv "$VENV"
  "$VENV/bin/python" -m pip install --upgrade pip >/dev/null 2>&1
  # av 19 breaks faster-whisper (it rejects the argument metadata_errors): keep av below 19
  "$VENV/bin/python" -m pip install --upgrade faster-whisper "av<19" \
    && "$VENV/bin/python" -c 'import faster_whisper' >/dev/null 2>&1 \
    && add "faster-whisper" INSTALLED "$VENV" || add "faster-whisper" FAIL "$VENV/bin/python -m pip install faster-whisper"
else
  add "faster-whisper" SKIPPED "python3 is not ready"
fi

say "GitHub / Claude Code"
if has gh; then
  if gh auth status >/dev/null 2>&1; then add "gh (logged in)" OK ""; else add "gh (logged in)" MISSING "gh auth login"; fi
else
  add "gh (logged in)" SKIPPED "gh is not installed"
fi
if has claude; then add "claude" OK "Run 'claude' once and log in if you have not"; else add "claude" INFO "Needed only for the minutes step. See https://docs.claude.com/en/docs/claude-code/setup"; fi

run_transcribe() {  # run_transcribe <audio> <model> <out_dir>
  local audio="$1" model="$2" out="$3"
  local norm; norm="$(mktemp -t mtgnorm).wav"
  # same loudness normalisation as transcribe.ps1
  ffmpeg -y -loglevel error -i "$audio" -af "highpass=f=80,dynaudnorm=f=200:g=21:p=0.9:m=30,loudnorm=I=-16:TP=-1.5:LRA=11" -ar 16000 -ac 1 "$norm" || return 1
  "$VENV/bin/python" -I "$SCRIPT_DIR/transcribe.py" "$norm" "$out" --stem "${audio:t:r}" --model "$model" --device cpu
  local rc=$?
  rm -f "$norm"
  return $rc
}

if (( DO_TEST )); then
  say "Transcription test (3 seconds, model tiny, CPU)"
  if [[ -x "$VENV/bin/python" ]] && has ffmpeg; then
    T="$(mktemp -d)"
    ffmpeg -y -loglevel error -f lavfi -i "sine=frequency=440:duration=3" -ar 16000 -ac 1 "$T/test.wav"
    START=$SECONDS
    if run_transcribe "$T/test.wav" tiny "$T/out" >"$T/log" 2>&1 && [[ -f "$T/out/test.srt" ]]; then
      add "transcribe test" OK "$((SECONDS - START)) s (includes the model download the first time)"
    else
      add "transcribe test" FAIL "$(tail -n 2 "$T/log" | tr '\n' ' ')"
    fi
    rm -rf "$T"
  else
    add "transcribe test" SKIPPED "faster-whisper or ffmpeg is not ready"
  fi
fi

if [[ -n "$AUDIO" ]]; then
  say "Transcribe: $AUDIO (model $MODEL, CPU)"
  if [[ ! -f "$AUDIO" ]]; then
    add "transcribe" FAIL "file not found: $AUDIO"
  else
    OUT="${MTG_OUT:-$HOME/mtg-transcripts}/${AUDIO:t:r}"
    START=$SECONDS
    if run_transcribe "$AUDIO" "$MODEL" "$OUT"; then
      python3 -I "$SCRIPT_DIR/clean.py" "$OUT/${AUDIO:t:r}.srt" >/dev/null 2>&1
      add "transcribe" OK "$OUT  ($((SECONDS - START)) s)"
    else
      add "transcribe" FAIL "see the output above"
    fi
  fi
fi

print ""
print -- "Step                     Status     Note"
print -- "------------------------ ---------- ----"
for r in "${RESULTS[@]}"; do print -r -- "$r"; done
print ""
print -P "%F{yellow}Not automated on a Mac:%f watching a folder and the minutes (run.ps1 / watch.ps1) run on Windows."
BAD=$(printf '%s\n' "${RESULTS[@]}" | grep -cE ' (FAIL|MISSING) ' || true)
if (( BAD > 0 )); then print -P "%F{red}Not ready: $BAD item(s)%f"; exit 1; else print -P "%F{green}Ready.%f"; fi
