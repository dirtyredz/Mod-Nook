# Releasing Mod Nook

The shared rules live in [12-versioning-and-release.md](https://github.com/dirtyredz/chest-labels/blob/main/12-versioning-and-release.md).
This file is what is specific to this mod, and where it currently stands.

## Packaging

```powershell
.\mods\ModNook\pack.ps1
```

Produces `dist/ModNook-<version>.zip` laid out as Nexus and Vortex expect:

```
BepInEx/plugins/ModNook/ModNook.dll
```

Note that is **not** the dev deploy path. `dotnet build` deploys to
`plugins/MoonlightPeaksMods/ModNook/` to keep hand-built DLLs clear of Vortex; players get the
plain `plugins/ModNook/` layout. `pack.ps1` builds with `SkipDeploy=true`, so packaging never
overwrites the copy under test.

The script reads the version from the csproj; `Plugin.cs` derives the same value at build time via
`ModBuildInfo.Version`, so the archive name and the version the DLL reports can never disagree.

## No test project, on purpose

Every code path either reads another mod's BepInEx config or clones a live Unity object out of the
running game. A runner outside the game could not exercise any of it. Verification is manual — see
[TESTING.md](TESTING.md).

If a pure-logic layer ever appears here — the tag parser, or the list join/split — it should get a
runner.

## Pre-release checklist

Verified for 1.0.0:

- [x] **Font** — `defaultFontAsset` appears only as the fallback inside `GameFonts.Apply`
- [x] **Colour** — colour literals confined to `PanelSprite.cs` and `Palette`
- [x] **Shape** — panels are 9-sliced and rounded; the backdrop, header and corner prompt are the
      game's own objects rather than imitations
- [x] **Version set in the csproj** — `Plugin.cs` derives it via `ModBuildInfo.Version`
- [x] **CHANGELOG** has exactly one entry for this version
- [x] **Diagnostics off** — `VerboseLogging` defaults to `false`
- [x] **No dependency in either direction** — nothing references this assembly, and it references
      no other mod

Still to do by hand before publishing:

- [ ] Full pass of [TESTING.md](TESTING.md) on a fresh config
- [ ] Screenshots — see [screenshots/README.md](screenshots/README.md)
- [ ] Confirm it behaves with Mod Menu installed alongside, and with it absent
- [ ] Confirm the game's own Settings screen is undamaged after a session with the panel open —
      the backdrop, header and prompt bar are all borrowed from it

## Compatibility note for the page

Mod Nook patches `PauseScreen.OnShow`, `PauseScreen.OnHide` and
`PauseMenuState.ProcessContinueInput`. Mod Menu patches the last of those too. Harmony runs both
prefixes, so they coexist — but if pause input ever misbehaves with both installed, that shared
method is where to look first.

Because it integrates with the pause screen and clones objects out of the Settings screen, a game
update may require a compatibility update. Say so on the page rather than letting players discover
it.
