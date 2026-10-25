#!/usr/bin/env bash
# Unity EditMode 테스트를 batchmode로 실행하고 결과 요약을 출력한다.
cd "$(dirname "$0")/.."
mkdir -p TestResults
"/c/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults "$(pwd -W)/TestResults/editmode.xml" -logFile TestResults/unity.log
code=$?
grep -o '<test-run[^>]*' TestResults/editmode.xml 2>/dev/null | grep -oE '(result|total|passed|failed)="[^"]*"' | tr '\n' ' '
echo; grep -E "error CS" TestResults/unity.log | head -10
exit $code
