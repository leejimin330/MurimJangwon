#!/usr/bin/env bash
# Unity batchmode로 에디터 메서드를 실행한다. 사용: tools/unity-run.sh <Method> [--nographics]
cd "$(dirname "$0")/.."
mkdir -p TestResults
extra=""
[ "$2" = "--nographics" ] && extra="-nographics"
"/c/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe" -batchmode $extra -projectPath . \
  -executeMethod "$1" -quit -logFile "TestResults/$1.log"
code=$?
grep -E "error CS|Exception|스냅샷|씬을 만들" "TestResults/$1.log" | head -20
exit $code
