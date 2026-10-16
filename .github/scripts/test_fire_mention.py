"""fire_mention.should_fire のテスト。python .github/scripts/test_fire_mention.py"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from fire_mention import should_fire  # noqa: E402


def ev(body="@claude どういうこと？", assoc="COLLABORATOR", user_type="User", action="created"):
    return {"action": action, "comment": {"body": body, "author_association": assoc, "user": {"type": user_type}}}


def main() -> int:
    cases = [
        (ev(), True),
        (ev(assoc="OWNER"), True),
        (ev(assoc="MEMBER"), True),
        (ev(assoc="CONTRIBUTOR"), False),   # 外部の人
        (ev(assoc="NONE"), False),
        (ev(user_type="Bot"), False),
        (ev(body="どういうこと？"), False),   # 呼びかけ無し
        (ev(body="mail@claude.com に送る"), False),  # メールアドレスは呼びかけではない
        (ev(body="@claude どういうこと？\n<!-- claude-question -->"), False),  # Claude 自身の文
        (ev(action="edited"), False),
    ]
    bad = [(c, want) for c, want in cases if should_fire(c)[0] != want]
    for c, want in bad:
        print("FAIL", want, c)
    print("ok" if not bad else "failed")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
