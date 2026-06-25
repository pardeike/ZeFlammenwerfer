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
