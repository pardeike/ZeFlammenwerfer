# Release Flow

## GitHub Release

The GitHub release workflow is `.github/workflows/release-mod.yml`.

It runs on the default branch when `Directory.Build.props` changes, and it can also be started manually from GitHub Actions. The workflow:

- reads `ModFileName` and `ModVersion` from `Directory.Build.props`.
- builds `Source/ZeFlammenwerfer.csproj` in Release configuration.
- stages the mod through the project `CopyToRimworld` target.
- creates or verifies tag `v<ModVersion>`.
- creates a GitHub release if needed.
- uploads `dist/ZeFlammenwerfer.zip`.

`.github/workflows/main.yml` moves the `latest` tag when a GitHub release is published. `About/Manifest.xml` points its `manifestUri` at that `latest` tag.

## Release Prep

For a normal release:

```bash
./scripts/build-quiet.sh -c Release /p:RIMWORLD_MOD_DIR=
git diff --check
```

If asset bundles changed, rebuild them before the release commit:

```bash
./scripts/build-assetbundles.sh --full
```

When publishing a new version, update `ModVersion` in `Directory.Build.props`, run a Release build, and include the generated release artifacts that intentionally changed:

- `Directory.Build.props`
- `About/About.xml`
- `About/Manifest.xml`
- `1.6/Assemblies/ZeFlammenwerfer.dll`
- `1.6/Assemblies/RimBridgeServer.Annotations.dll`, if the dependency changed
- `Resources/flamethrower-*`, if asset bundles changed

For ordinary source-only development commits, restore generated assemblies before committing unless the commit is intentionally release-related.

## Steam Workshop

This mod has not been published to Steam Workshop yet. Do not add `About/PublishedFileId.txt` by hand.

For the first Steam upload, use the SteamWorkshopAgent `new-mod` flow after the GitHub release artifact is ready. That first publish should create the Workshop item and add `About/PublishedFileId.txt`; commit that file afterward as the permanent Workshop id.

After the first publish, future Steam updates should use the normal release publish path against the existing `PublishedFileId.txt`.
