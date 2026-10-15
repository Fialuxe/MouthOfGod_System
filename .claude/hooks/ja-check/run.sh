#!/usr/bin/env bash
# 動く Python を探して ja_check.py を呼ぶ（Windows は python、クラウドの Linux は python3 のことが多い）。
# Python が無ければ、何もせずに通す。
for p in python python3; do
  if "$p" -c "" >/dev/null 2>&1; then
    exec "$p" "$(dirname "$0")/ja_check.py" "$@"
  fi
done
exit 0
