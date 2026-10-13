"""機能フォルダが、他の機能の名前空間を参照していないかを調べる（警告だけで、止めない）。

決まり（CONTRIBUTING「書き方」、docs/architecture.md「2. 機能と依存の向き」）:
- Assets/Scripts/<Feature>/ のコードが参照してよい名前空間は、自分の MouthOfGod.<Feature> と MouthOfGod.Core だけ。
- Bootstrap は、全機能を参照できる唯一の場所。
見つけたら、GitHub の警告（::warning）として PR に出す。終了コードは常に 0。
"""
import pathlib
import re
import sys

SCRIPTS = pathlib.Path("Assets/Scripts")
ALLOWED_EVERYWHERE = {"Core"}
UNRESTRICTED = {"Bootstrap"}
NAMESPACE_USE = re.compile(r"\bMouthOfGod\.([A-Za-z0-9_]+)")


def main() -> int:
    if not SCRIPTS.is_dir():
        return 0
    found = 0
    for feature_dir in sorted(p for p in SCRIPTS.iterdir() if p.is_dir()):
        feature = feature_dir.name
        if feature in UNRESTRICTED:
            continue
        for cs in sorted(feature_dir.rglob("*.cs")):
            for lineno, line in enumerate(cs.read_text(encoding="utf-8-sig").splitlines(), 1):
                if line.lstrip().startswith("//"):
                    continue
                for other in NAMESPACE_USE.findall(line):
                    if other == feature or other in ALLOWED_EVERYWHERE:
                        continue
                    found += 1
                    print(
                        f"::warning file={cs.as_posix()},line={lineno}::"
                        f"{feature} が {other} を参照している。機能どうしは直接参照せず、"
                        f"Core の interface かイベントでつなぐ（docs/architecture.md）。"
                    )
    print(f"他の機能への参照: {found} 件（警告だけで、止めない）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
