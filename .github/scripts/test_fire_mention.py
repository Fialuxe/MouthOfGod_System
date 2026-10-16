"""fire_mention.should_fire のテスト。python .github/scripts/test_fire_mention.py"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from datetime import datetime, timedelta, timezone  # noqa: E402

from fire_mention import count_recent, over_limit, should_fire  # noqa: E402


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
    # 回数の上限: owner 以外は 30 分に 2 回まで
    now = datetime(2026, 10, 16, 12, 0, tzinfo=timezone.utc)

    def cm(login, minutes_ago, body="@claude ?"):
        t = (now - timedelta(minutes=minutes_ago)).strftime("%Y-%m-%dT%H:%M:%SZ")
        return {"user": {"login": login}, "body": body, "created_at": t}

    def event(login, assoc):
        return {"comment": {"user": {"login": login}, "author_association": assoc}}

    limit_cases = [
        (over_limit(event("a", "COLLABORATOR"), [cm("a", 0), cm("a", 10)], now), False),          # 2 回目まで
        (over_limit(event("a", "COLLABORATOR"), [cm("a", 0), cm("a", 10), cm("a", 20)], now), True),  # 3 回目
        (over_limit(event("a", "COLLABORATOR"), [cm("a", 0), cm("a", 10), cm("a", 40)], now), False),  # 40 分前は数えない
        (over_limit(event("a", "COLLABORATOR"), [cm("a", 0), cm("b", 5), cm("b", 6)], now), False),   # 他の人は数えない
        (over_limit(event("a", "COLLABORATOR"), [cm("a", 0), cm("a", 5, body="ふつうのコメント"), cm("a", 6, body="なし")], now), False),
        (over_limit(event("o", "OWNER"), [cm("o", i) for i in range(10)], now), False),                 # owner は無制限
        (count_recent([cm("a", 1)], "a", now), 1),
    ]
    for got, want in limit_cases:
        if got != want:
            bad.append((got, want))
            print("FAIL limit", want, got)
    print("ok" if not bad else "failed")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
