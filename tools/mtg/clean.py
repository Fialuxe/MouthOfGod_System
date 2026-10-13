"""Mechanical clean-up of a Whisper .srt: drop known hallucinations, collapse
repeats, apply corrections.tsv. Writes <name>.clean.srt and <name>.clean.txt.

Usage: python clean.py <file.srt>
"""
import re
import sys
from pathlib import Path

# Whole-cue patterns Whisper invents in silence/noise (trained on YouTube subtitles).
HALLUCINATIONS = [
    r"ご視聴(ありがとう|いただき).*",
    r"チャンネル登録.*",
    r"高評価.*",
    r"字幕.*(提供|作成|翻訳).*",
    r"音楽",
    r"♪.*",
    r"(最後まで)?ご覧いただきありがとうございました。?",
]
HALLU_RE = re.compile(r"^(?:%s)$" % "|".join(HALLUCINATIONS))
LOW_LOGPROB = -0.7  # Whisper avg_logprob below this = the model itself is unsure
MAX_REPEAT = 2  # a cue repeated more than this many times in a row is collapsed


def load_corrections(path: Path):
    pairs = []
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            if line.strip() and not line.startswith("#") and "\t" in line:
                wrong, right = line.split("\t", 1)
                pairs.append((wrong, right))
    return pairs


def load_confidence(src: Path):
    conf = {}
    f = src.with_suffix(".conf.tsv")
    if f.exists():
        for line in f.read_text(encoding="utf-8").splitlines():
            start, logprob, _ = line.split("\t")
            conf[start] = float(logprob)
    return conf


def parse_srt(text: str):
    cues = []
    for block in re.split(r"\n\s*\n", text.strip()):
        lines = block.strip().splitlines()
        if len(lines) >= 3 and "-->" in lines[1]:
            cues.append([lines[1], " ".join(lines[2:]).strip()])
    return cues


def main(src: Path):
    pairs = load_corrections(Path(__file__).with_name("corrections.tsv"))
    # local-only pairs (e.g. member names), kept out of the public repo:
    # <transcripts dir>/, <root>/ or <root>/config/ corrections.local.tsv (root = the run.ps1 work folder)
    root = src.resolve().parent.parent
    for d in (src.resolve().parent, root, root / "config"):
        pairs += load_corrections(d / "corrections.local.tsv")
    cues = parse_srt(src.read_text(encoding="utf-8"))
    conf = load_confidence(src)
    kept, dropped, run = [], 0, 0
    prev = None
    for ts, text in cues:
        if HALLU_RE.match(text.strip().strip("。、 ")) or not text:
            dropped += 1
            continue
        run = run + 1 if text == prev else 1
        prev = text
        if run > MAX_REPEAT:
            dropped += 1
            continue
        for wrong, right in pairs:
            text = text.replace(wrong, right)
        low = conf.get(ts.split(" --> ")[0], 0.0) < LOW_LOGPROB
        kept.append((ts, text, low))

    srt, txt = [], []
    for i, (ts, text, low) in enumerate(kept, 1):
        srt.append(f"{i}\n{ts}\n{text}\n")
        hms = ts.split(",")[0]  # HH:MM:SS
        # MM:SS under 1 h, H:MM:SS after (recordings can be 2-3 h; [12:34] alone would be ambiguous)
        label = hms[3:] if hms.startswith("00:") else f"{int(hms[:2])}:{hms[3:]}"
        txt.append(f"[{label}] {'?' if low else ''}{text}")
    src.with_suffix(".clean.srt").write_text("\n".join(srt), encoding="utf-8")
    src.with_suffix(".clean.txt").write_text("\n".join(txt), encoding="utf-8")
    n_low = sum(1 for *_, low in kept if low)
    print(f"{src.name}: {len(cues)} cues -> {len(kept)} kept, {dropped} dropped, {n_low} low-confidence (?)")


if __name__ == "__main__":
    main(Path(sys.argv[1]))
