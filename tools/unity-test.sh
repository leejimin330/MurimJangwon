#!/usr/bin/env bash
# Unity EditMode 테스트를 batchmode로 실행하고 결과 요약을 출력한다. 사용: tools/unity-test.sh [테스트 필터]
cd "$(dirname "$0")/.."
mkdir -p TestResults
filter=()
[ -n "$1" ] && filter=(-testFilter "$1")
rm -f TestResults/editmode.xml
"/c/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode "${filter[@]}" -testResults "$(pwd -W)/TestResults/editmode.xml" -logFile TestResults/unity.log
code=$?
grep -o '<test-run[^>]*' TestResults/editmode.xml 2>/dev/null | grep -oE '(result|total|passed|failed)="[^"]*"' | tr '\n' ' '
echo
grep -E "error CS" TestResults/unity.log | head -10
python - <<'PY' 2>/dev/null
import xml.etree.ElementTree as ET
t = ET.parse("TestResults/editmode.xml")
for c in t.iter("test-case"):
    if c.get("result") == "Failed":
        m = c.find("failure/message")
        print("FAIL", c.get("fullname"), "::", (m.text or "").strip()[:600])
PY
exit $code
