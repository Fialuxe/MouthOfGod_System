"""Transcribe with faster-whisper (large-v3 by default; fits in 8GB VRAM).

Usage: python transcribe.py <audio> <out_dir> [--stem NAME] [--model large-v3]
Writes <stem>.srt (read by clean.py) and <stem>.txt.
"""
import argparse
from pathlib import Path

from faster_whisper import WhisperModel


def stamp(t: float) -> str:
    ms = int(round(t * 1000))
    h, ms = divmod(ms, 3_600_000)
    m, ms = divmod(ms, 60_000)
    s, ms = divmod(ms, 1000)
    return f"{h:02d}:{m:02d}:{s:02d},{ms:03d}"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("audio")
    ap.add_argument("out_dir")
    ap.add_argument("--stem")
    ap.add_argument("--model", default="large-v3")
    a = ap.parse_args()

    glossary = Path(__file__).with_name("glossary.txt")
    prompt = ""
    if glossary.exists():
        prompt = "、".join(l.strip() for l in glossary.read_text(encoding="utf-8").splitlines() if l.strip())

    out = Path(a.out_dir)
    out.mkdir(parents=True, exist_ok=True)
    stem = a.stem or Path(a.audio).stem

    model = WhisperModel(a.model, device="cuda", compute_type="float16")
    # VAD and hallucination_silence_threshold are off: both dropped quiet speech in
    # distant-mic recordings. clean.py removes the made-up text instead.
    segments, _ = model.transcribe(
        a.audio, language="ja", beam_size=5, vad_filter=False,
        condition_on_previous_text=False, initial_prompt=prompt,
        word_timestamps=True,
    )
    srt, txt = [], []
    for i, s in enumerate(segments, 1):
        text = s.text.strip()
        srt.append(f"{i}\n{stamp(s.start)} --> {stamp(s.end)}\n{text}\n")
        txt.append(text)
    (out / f"{stem}.srt").write_text("\n".join(srt), encoding="utf-8")
    (out / f"{stem}.txt").write_text("\n".join(txt), encoding="utf-8")
    print(f"{stem}: {len(srt)} segments")


if __name__ == "__main__":
    main()
