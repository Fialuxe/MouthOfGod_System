"""Small GUI for the MTG pipeline (runs tools/mtg/run.ps1). Only needs Python (tkinter).

Start it with tools/mtg/mtg-gui.cmd (double click), or: python gui.py
Self test without opening a window: python gui.py --selftest
"""
import glob
import os
import queue
import shutil
import subprocess
import sys
import threading
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox, ttk

HERE = Path(__file__).resolve().parent
RUN_PS1 = HERE / "run.ps1"
AUDIO_EXT = {".mp3", ".m4a", ".wav", ".aac", ".flac", ".mp4", ".ogg"}
DEFAULT_ROOT = Path.home() / "Github" / "MouthOfGod_transcripts"
PRIVATE_REPO = "Fialuxe/MouthOfGod_transcripts"
NO_WINDOW = getattr(subprocess, "CREATE_NO_WINDOW", 0)


def run_quiet(args, timeout=30):
    """Run a command, return (returncode, output). Never raises."""
    try:
        r = subprocess.run(args, capture_output=True, text=True, encoding="utf-8",
                           errors="replace", timeout=timeout, creationflags=NO_WINDOW)
        return r.returncode, (r.stdout + r.stderr).strip()
    except Exception as e:  # missing exe, timeout, ...
        return 1, str(e)


def find_ffmpeg():
    if shutil.which("ffmpeg"):
        return True
    pattern = os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\WinGet\Packages\Gyan.FFmpeg*\**\ffmpeg.exe")
    return bool(glob.glob(pattern, recursive=True))  # run.ps1 finds it there even before a PATH refresh


def preflight(root: Path):
    """[(ok, label, hint)]. ok=None means a warning, not a failure."""
    checks = []
    checks.append((find_ffmpeg(), "ffmpeg", "winget install Gyan.FFmpeg"))
    rc, _ = run_quiet([sys.executable, "-c", "import faster_whisper"])
    checks.append((rc == 0, "faster-whisper", "pip install faster-whisper"))
    checks.append((run_quiet(["nvidia-smi"])[0] == 0, "NVIDIA GPU (nvidia-smi)", "needs an NVIDIA GPU with a driver"))
    checks.append((shutil.which("git") is not None, "git", "install Git for Windows"))
    checks.append((shutil.which("claude") is not None, "claude (Claude Code)", "install Claude Code and log in"))
    gh_ok = shutil.which("gh") is not None and run_quiet(["gh", "auth", "status"])[0] == 0
    checks.append((gh_ok, "gh (GitHub CLI, logged in)", "gh auth login"))
    if gh_ok:
        rc, out = run_quiet(["gh", "repo", "view", PRIVATE_REPO, "--json", "visibility", "-q", ".visibility"])
        checks.append((rc == 0 and out == "PRIVATE", f"private repo {PRIVATE_REPO}", "ask to be invited to the private repo"))
    members = root / "config" / "members.local.tsv"
    checks.append((True if members.exists() else None, "config/members.local.tsv (assignees)", f"optional: put it in {root / 'config'}"))
    return checks


def build_cmd(root: Path, dry: bool, no_archive: bool):
    def q(s):
        return "'" + str(s).replace("'", "''") + "'"
    ps = ("[Console]::OutputEncoding=[Text.Encoding]::UTF8; "
          f"& {q(RUN_PS1)} -Root {q(root)}" + (" -DryRun" if dry else "") + (" -NoArchive" if no_archive else ""))
    return ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", ps]


def stream(cmd, on_line, on_start=None):
    """Run cmd, call on_line for each output line, return the exit code."""
    p = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, creationflags=NO_WINDOW)
    if on_start:
        on_start(p)
    for raw in iter(p.stdout.readline, b""):
        on_line(raw.decode("utf-8", errors="replace").rstrip("\r\n"))
    return p.wait()


class App:
    def __init__(self, master):
        self.m = master
        master.title("MTG 文字起こし・議事録")
        master.geometry("820x540+40+30")
        self.proc = None
        self.q = queue.Queue()
        self.root_var = tk.StringVar(value=str(DEFAULT_ROOT))
        self.dry = tk.BooleanVar(value=True)
        self.no_archive = tk.BooleanVar(value=False)
        self.status = tk.StringVar(value="待機中")
        self._build()
        self.refresh_inbox()
        self.m.after(100, self._drain)

    # ---- layout
    def _build(self):
        f = ttk.Frame(self.m, padding=8)
        f.pack(fill="both", expand=True)

        r = ttk.Frame(f)
        r.pack(fill="x")
        ttk.Label(r, text="作業フォルダ").pack(side="left")
        ttk.Entry(r, textvariable=self.root_var).pack(side="left", fill="x", expand=True, padx=6)
        ttk.Button(r, text="参照…", command=self.pick_root).pack(side="left")

        ib = ttk.LabelFrame(f, text="inbox（ここの録音が処理される。ファイルの更新日が MTG の日付）", padding=6)
        ib.pack(fill="x", pady=8)
        self.lb = tk.Listbox(ib, height=4)
        self.lb.pack(side="left", fill="x", expand=True)
        bf = ttk.Frame(ib)
        bf.pack(side="left", padx=6)
        ttk.Button(bf, text="録音を追加…", command=self.add_audio).pack(fill="x")
        ttk.Button(bf, text="更新", command=self.refresh_inbox).pack(fill="x", pady=2)
        ttk.Button(bf, text="inbox を開く", command=lambda: self.open_dir(self.root() / "inbox")).pack(fill="x")

        o = ttk.Frame(f)
        o.pack(fill="x")
        ttk.Checkbutton(o, text="試運転（GitHub にもリポジトリにも書き込まない。提案書だけ作る）", variable=self.dry).pack(anchor="w")
        ttk.Checkbutton(o, text="文字起こしを非公開リポジトリに保存しない（テスト用）", variable=self.no_archive).pack(anchor="w")

        a = ttk.Frame(f)
        a.pack(fill="x", pady=6)
        self.run_btn = ttk.Button(a, text="実行", command=self.run)
        self.run_btn.pack(side="left")
        self.stop_btn = ttk.Button(a, text="停止", command=self.stop, state="disabled")
        self.stop_btn.pack(side="left", padx=6)
        ttk.Button(a, text="環境チェック", command=self.check).pack(side="left")
        ttk.Label(a, textvariable=self.status).pack(side="left", padx=12)

        # the button row is packed first (side=bottom) so it stays visible on small / scaled screens
        b = ttk.Frame(f)
        b.pack(side="bottom", fill="x", pady=(6, 0))
        ttk.Button(b, text="提案書（最新）を開く", command=self.open_proposal).pack(side="left")
        ttk.Button(b, text="文字起こしフォルダ", command=lambda: self.open_dir(self.root() / "transcripts")).pack(side="left", padx=6)
        ttk.Button(b, text="ログフォルダ", command=lambda: self.open_dir(self.root() / "logs")).pack(side="left")

        lf = ttk.Frame(f)
        lf.pack(fill="both", expand=True)
        self.log = tk.Text(lf, wrap="word", state="disabled", font=("Consolas", 9))
        sb = ttk.Scrollbar(lf, command=self.log.yview)
        self.log.configure(yscrollcommand=sb.set)
        sb.pack(side="right", fill="y")
        self.log.pack(side="left", fill="both", expand=True)

    # ---- helpers
    def root(self):
        return Path(self.root_var.get()).expanduser()

    def say(self, text):
        self.log.configure(state="normal")
        self.log.insert("end", text + "\n")
        self.log.see("end")
        self.log.configure(state="disabled")

    def open_dir(self, p: Path):
        p.mkdir(parents=True, exist_ok=True)
        os.startfile(str(p))

    def pick_root(self):
        d = filedialog.askdirectory(initialdir=str(self.root()))
        if d:
            self.root_var.set(d)
            self.refresh_inbox()

    def refresh_inbox(self):
        self.lb.delete(0, "end")
        inbox = self.root() / "inbox"
        if inbox.exists():
            for f in sorted(inbox.iterdir()):
                if f.is_file() and f.suffix.lower() in AUDIO_EXT:
                    self.lb.insert("end", f"{f.name}    ({f.stat().st_size / 1e6:.0f} MB)")
        if not self.lb.size():
            self.lb.insert("end", "（録音がありません。「録音を追加…」で入れてください）")

    def add_audio(self):
        files = filedialog.askopenfilenames(title="録音を選ぶ", filetypes=[("音声", "*.mp3 *.m4a *.wav *.aac *.flac *.mp4 *.ogg"), ("すべて", "*.*")])
        inbox = self.root() / "inbox"
        inbox.mkdir(parents=True, exist_ok=True)
        for f in files:
            shutil.copy2(f, inbox)  # copy2 keeps the modified time (= the MTG date)
        self.refresh_inbox()

    def check(self):
        self.status.set("環境チェック中…")
        root = self.root()

        def work():
            res = preflight(root)
            self.q.put(("check", res))
        threading.Thread(target=work, daemon=True).start()

    def open_proposal(self):
        files = list((self.root() / "transcripts").glob("*.proposal.md"))
        if not files:
            messagebox.showinfo("提案書", "提案書がまだありません（試運転で作られます）")
            return
        os.startfile(str(max(files, key=lambda p: p.stat().st_mtime)))

    # ---- run
    def run(self):
        if self.proc:
            return
        has_audio = any(f.suffix.lower() in AUDIO_EXT for f in (self.root() / "inbox").glob("*")) if (self.root() / "inbox").exists() else False
        if not has_audio:
            messagebox.showinfo("実行", "inbox に録音がありません。")
            return
        if not self.dry.get():
            if not messagebox.askokcancel("実行", "試運転ではありません。\nGitHub の Issue へのコメント・ラベル変更・クローズ・PR 作成が、実際に行われます。\n実行しますか？"):
                return
        cmd = build_cmd(self.root(), self.dry.get(), self.no_archive.get())
        self.say(f"=== 開始 ({'試運転' if self.dry.get() else '本番'}) ===")
        self.run_btn.configure(state="disabled")
        self.stop_btn.configure(state="normal")
        self.status.set("実行中…（数分〜数十分かかります）")

        def work():
            code = stream(cmd, lambda line: self.q.put(("line", line)), lambda p: setattr(self, "proc", p))
            self.q.put(("done", code))
        threading.Thread(target=work, daemon=True).start()

    def stop(self):
        if self.proc:
            subprocess.run(["taskkill", "/PID", str(self.proc.pid), "/T", "/F"], creationflags=NO_WINDOW, capture_output=True)
            self.say("=== 停止しました ===")

    def _drain(self):
        try:
            while True:
                kind, val = self.q.get_nowait()
                if kind == "line":
                    self.say(val)
                elif kind == "done":
                    self.proc = None
                    self.run_btn.configure(state="normal")
                    self.stop_btn.configure(state="disabled")
                    self.status.set("完了" if val == 0 else f"終了（コード {val}）。ログを確認してください")
                    self.say(f"=== 終了（コード {val}）===")
                    self.refresh_inbox()
                elif kind == "check":
                    self.status.set("待機中")
                    for ok, label, hint in val:
                        mark = "✓" if ok else ("△" if ok is None else "✗")
                        self.say(f"{mark} {label}" + ("" if ok else f"  → {hint}"))
        except queue.Empty:
            pass
        self.m.after(100, self._drain)


def selftest():
    root = tk.Tk()
    root.withdraw()
    App(root)
    print("window: ok")
    cmd = build_cmd(DEFAULT_ROOT, True, True)
    assert "-DryRun" in cmd[-1] and "-NoArchive" in cmd[-1], cmd
    out = []
    code = stream(["powershell.exe", "-NoProfile", "-Command", "[Console]::OutputEncoding=[Text.Encoding]::UTF8; 'こんにちは'; exit 3"], out.append)
    assert code == 3 and "こんにちは" in out, (code, out)
    print("stream (utf-8, exit code): ok")
    for ok, label, hint in preflight(DEFAULT_ROOT):
        print(("OK  " if ok else "WARN" if ok is None else "NG  "), label)
    root.destroy()


if __name__ == "__main__":
    if "--selftest" in sys.argv:
        selftest()
    else:
        r = tk.Tk()
        App(r)
        r.mainloop()
