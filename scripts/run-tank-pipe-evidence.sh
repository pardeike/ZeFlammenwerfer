#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SAVE_NAME="zeflammenwerfer walkthrough"
SAVE_FIXTURE="$ROOT/Originals/${SAVE_NAME}.rws"
SAVE_DIR="${RIMWORLD_SAVE_DIR:-/Users/ap/Documents/OlderRimWorlds/RimWorldMac1.6-UserData/Saves}"
SAVE_TARGET="$SAVE_DIR/${SAVE_NAME}.rws"

if [[ ! -f "$SAVE_FIXTURE" ]]; then
	echo "missing save fixture: $SAVE_FIXTURE" >&2
	exit 1
fi

mkdir -p "$SAVE_DIR"
cp -p "$SAVE_FIXTURE" "$SAVE_TARGET"

exec node "$ROOT/scripts/lib/run-rimbridge-tool.mjs" \
	--suite tank-pipe-16-aims-sdk \
	--save-name "$SAVE_NAME" \
	"$@"
