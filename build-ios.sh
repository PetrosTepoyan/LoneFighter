#!/usr/bin/env bash
# build-ios.sh — CLI wrapper for the Unity → Xcode export.
#
# Usage:
#   ./build-ios.sh                              # auto-bumps the build number
#   ./build-ios.sh --build-number 42            # pin a specific buildNumber
#   ./build-ios.sh --version 0.2.0              # pin a specific marketing version
#   UNITY=/path/to/Unity ./build-ios.sh         # override Unity binary location
#
# Defaults to the LTS Unity 6 path on macOS. Adjust UNITY env var if yours is elsewhere.

set -euo pipefail

UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.0.32f1/Unity.app/Contents/MacOS/Unity}"
PROJECT="$(cd "$(dirname "$0")" && pwd)"
LOG="$PROJECT/Build/iOS-build.log"
EXTRA=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version) EXTRA="$EXTRA version=$2"; shift 2 ;;
    --build-number) EXTRA="$EXTRA buildNumber=$2"; shift 2 ;;
    -h|--help)
      grep '^#' "$0" | head -20
      exit 0
      ;;
    *) echo "Unknown arg: $1"; exit 2 ;;
  esac
done

if [[ ! -x "$UNITY" ]]; then
  echo "Unity binary not found at: $UNITY"
  echo "Set UNITY=/path/to/Unity.app/Contents/MacOS/Unity and rerun."
  exit 1
fi

mkdir -p "$PROJECT/Build"
echo "Building iOS Xcode project. Log: $LOG"

"$UNITY" \
  -batchmode -quit -nographics \
  -projectPath "$PROJECT" \
  -buildTarget iOS \
  -executeMethod LoneFighter.EditorTools.Build.IOSBuildScript.BuildFromCli \
  -logFile "$LOG" \
  $EXTRA

echo "Done. Xcode project: $PROJECT/Build/iOS/Unity-iPhone.xcworkspace"
echo "Open it in Xcode, set signing team, then Product > Archive."
