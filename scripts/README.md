# Scripts

## Build

Use `build-quiet.sh` for routine C# builds:

```bash
./scripts/build-quiet.sh -c Release /p:RIMWORLD_MOD_DIR=
```

If `RIMWORLD_MOD_DIR` is set, the script refuses to deploy over a running RimWorld process unless it can stop it first. Use `--no-stop-rimworld` when you want that refusal instead of automatic shutdown.

## Asset Bundles

Use `build-assetbundles.sh` to compile the Unity project in `Originals/FlameThrowerUnity` and deploy the generated bundles into the mod's root `Resources` folder.

The mod consumes these files:

```text
Resources/flamethrower-win
Resources/flamethrower-linux
Resources/flamethrower-mac
```

Unity may create intermediates under:

```text
Originals/FlameThrowerUnity/Assets/AssetBundles/
Originals/FlameThrowerUnity/Library/
Originals/FlameThrowerUnity/Temp/
Originals/FlameThrowerUnity/Logs/
Originals/FlameThrowerUnity/UserSettings/
```

Those are generated build/cache state unless you are intentionally changing the Unity project.

Full rebuild:

```bash
./scripts/build-assetbundles.sh
./scripts/build-assetbundles.sh --full
```

Quick current-platform rebuild:

```bash
./scripts/build-assetbundles.sh --current
./scripts/build-assetbundles.sh --quick
```

Single explicit target:

```bash
./scripts/build-assetbundles.sh --os MacOS
./scripts/build-assetbundles.sh --os Linux
./scripts/build-assetbundles.sh --os Win64
```

Use a non-default Unity editor executable:

```bash
UNITY_EDITOR=/path/to/Unity.app/Contents/MacOS/Unity ./scripts/build-assetbundles.sh --current
```

The default editor path is:

```text
/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity
```

## Log Summary

Use `summarize-rimworld-log.sh` to deduplicate RimWorld error blocks:

```bash
./scripts/summarize-rimworld-log.sh "$HOME/Library/Logs/Unity/Player.log"
```

## RimBridge Evidence Suites

Use `run-tank-pipe-evidence.sh` to run the RimBridge companion-tool suite that poses Rocha against 16 aim targets around his occupied cell and captures 16 close-up `rimworld/screenshot_cell_rect` images of the tank and pipe rendering:

```bash
./scripts/run-tank-pipe-evidence.sh
```

The suite requests a `1x1` cell rect for Rocha's cell and defaults to `paddingCells=2` plus `rootSize=2.5` for detailed pawn/equipment captures. The screenshot tool still owns its own framing and crop safety.

The wrapper copies `Originals/zeflammenwerfer walkthrough.rws` into the configured RimWorld save folder before running the companion tool. Override paths and game identity with environment variables:

```bash
RIMWORLD_SAVE_DIR="/path/to/UserData/Saves" \
GABS_BIN="/path/to/gabs" \
GABS_GAME_ID="rimworld-direct" \
./scripts/run-tank-pipe-evidence.sh --force-takeover
```

Generated evidence is copied into:

```text
artifacts/rimbridge-evidence/tank-pipe-16-aims-sdk/<run-id>/
```

The authoritative in-game sequence lives in the companion tool `zeflammenwerfer/render_tank_pipe_pose_sweep`; the shell/Node launcher only starts GABS, calls that tool, and collects the screenshot files plus JSON manifest. The tool uses RimBridgeServer v2 SDK calls to query/call bridge tools and await real game ticks from C#.
