# Build And Dependencies

## Build Command

Run from the repository root:

```sh
dotnet build Source/ZeFlammenwerfer.csproj -c Release
```

The current project has no automated test project. Treat a clean build as the minimum check, then use in-game validation for flamethrower targeting, refuel behavior, asset bundle loading, fire/smoke rendering, and RimBridge tool changes.

For quieter local builds:

```sh
./scripts/build-quiet.sh -c Release /p:RIMWORLD_MOD_DIR=
```

## Outputs

Release builds write:

- `1.6/Assemblies/ZeFlammenwerfer.dll`
- `1.6/BridgeTools/ZeFlammenwerfer.BridgeTools.dll`

The project has a `CopyToRimworld` MSBuild target that runs when `RIMWORLD_MOD_DIR` is set. That target:

- deletes `1.6/Assemblies/0Harmony.dll` if a package accidentally places it there.
- copies the `1.4` and `1.6` folders plus root metadata/assets into `$(RIMWORLD_MOD_DIR)\ZeFlammenwerfer`.
- zips that copied mod folder as `$(RIMWORLD_MOD_DIR)\ZeFlammenwerfer.zip`.

On macOS with a Unix shell, MSBuild still prints Windows-style path separators in the target body because the project file uses backslashes.

## Package References

Current top-level package references in `Source/ZeFlammenwerfer.csproj`:

- `Brrainz.RimWorld.CrossPromotion`
- `Krafs.Rimworld.Ref`
- `Lib.Harmony`, with `ExcludeAssets="runtime"`
- `Microsoft.NETCore.Platforms`
- `Microsoft.NETFramework.ReferenceAssemblies.net472`
- `TaskPubliciser`

`Source/BridgeTools/ZeFlammenwerfer.BridgeTools.csproj` builds the RimBridge companion tool assembly against the official `RimBridgeServer.Sdk` NuGet package and writes it to `1.6/BridgeTools`. `NuGet.config` clears inherited package sources and enables only `nuget.org`, so restores do not accidentally consume a sibling local SDK feed. The companion reference uses `PrivateAssets="all"` and `ExcludeAssets="runtime"` so `RimBridgeServer.Sdk.dll` is not deployed beside the companion DLL.

`Lib.Harmony` is a compile-time package here. The runtime Harmony dependency is provided by the RimWorld Harmony mod declared in `About/About.xml`.

## Publicised RimWorld Reference

`Source/ZeFlammenwerfer.csproj` contains two custom targets:

- `MyCode`, before `UpdateReferences`, publicises `$(PkgKrafs_Rimworld_Ref)\ref\net472\Assembly-CSharp.dll` into `Assembly-CSharp_publicised.dll`, then adds the publicised assembly as a reference.
- `UpdateReferences`, after `ResolveLockFileReferences`, removes the original non-publicised `Assembly-CSharp.dll` reference.

This is central to the codebase because the mod touches RimWorld internals for combat, pathing, rendering, and job behavior.

## Version Metadata

`Directory.Build.props` owns:

- `ModName`
- `ModFileName`
- `Repository`
- `ModVersion`
- `ProjectGuid`

The `PostBuildAction` target writes `ModVersion` into:

- `About/About.xml` at `//ModMetaData/modVersion`
- `About/Manifest.xml` at `//Manifest/version`

## RimWorld Payload Files

`LoadFolders.xml` intentionally loads shared root content plus per-version folders:

- RimWorld `1.4`: `/`, then `1.4`
- RimWorld `1.6`: `/`, then `1.6`

Root payload directories are therefore still active:

- `About`: RimWorld metadata, manifest, and preview image.
- `Defs`: shared flamethrower defs.
- `Resources`: platform-specific Unity asset bundles.
- `Sounds`: flamethrower audio clips.
- `Textures`: item/debug/tank textures.

RimWorld 1.6-only job and workgiver defs live under `1.6/Defs`.

## Asset Bundles

The Unity project lives at `Originals/FlameThrowerUnity` and is built through:

```sh
./scripts/build-assetbundles.sh
```

The deployed bundle files are:

- `Resources/flamethrower-win`
- `Resources/flamethrower-linux`
- `Resources/flamethrower-mac`

The runtime loader falls back to the legacy `Resources/flamethrower` bundle name if a platform-specific bundle is missing.
