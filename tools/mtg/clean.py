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
MAX_REPEAT = 2  # a cue repeated more than this many times in a row is collapsed


def load_corrections(path: Path):
    pairs = []
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            if line.strip() and not line.startswith("#") and "\t" in line:
                wrong, right = line.split("\t", 1)
                pairs.append((wrong, right))
    return pairs


def parse_srt(text: str):
    cues = []
    for block in re.split(r"\n\s*\n", text.strip()):
        lines = block.strip().splitlines()
        if len(lines) >= 3 and "-->" in lines[1]:
            cues.append([lines[1], " ".join(lines[2:]).strip()])
    return cues


def main(src: Path):
    pairs = load_corrections(Path(__file__).with_name("corrections.tsv"))
    cues = parse_srt(src.read_text(encoding="utf-8"))
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
        kept.append((ts, text))

    srt, txt = [], []
    for i, (ts, text) in enumerate(kept, 1):
        srt.append(f"{i}\n{ts}\n{text}\n")
        mm_ss = ts.split(",")[0][3:]  # HH:MM:SS -> MM:SS
        txt.append(f"[{mm_ss}] {text}")
    src.with_suffix(".clean.srt").write_text("\n".join(srt), encoding="utf-8")
    src.with_suffix(".clean.txt").write_text("\n".join(txt), encoding="utf-8")
    print(f"{src.name}: {len(cues)} cues -> {len(kept)} kept, {dropped} dropped")


if __name__ == "__main__":
    main(Path(sys.argv[1]))
