# Gore Sandbox: working rules

Unity 6 project, built entirely in C# code (`Assets/SolarEclipse/Scripts`, namespace `SolarEclipse`). There are no
scenes or prefabs to edit; meshes, textures and sounds are procedural.

## Every update is shipped through the auto-updater (owner's standing instruction)

Whenever a change is made, release it — don't leave it unpushed:

1. **Bump the version** in `Assets/SolarEclipse/Scripts/Core/GameInfo.cs` (`Version`). Players' games only update when
   this number goes up. Small fixes: patch (0.4.0 → 0.4.1); features: minor (0.4.x → 0.5.0).
2. **Add a changelog entry** at the top of the `Text` string in
   `Assets/SolarEclipse/Scripts/UI/Screens/ChangelogScreen.cs`. It's one `@"..."` verbatim string: insert the new entry
   right after the opening `@"` and never add a second `@"`. A literal `"` must be written `""`. Plain ASCII plus
   `•` and `—` only (mobile fonts lack other symbols). The newest entry becomes the GitHub release notes.
3. **Compile-check** before pushing. There is no Unity here: compile the scripts against Unity reference DLLs with
   `dotnet build` (netstandard2.1). Reference only the Unity modules enabled in `Packages/manifest.json` (plus
   CoreModule, SharedInternalsModule, TextRenderingModule, InputLegacyModule). A module missing there is a compile
   error in Unity. If new code needs another module, add it to `Packages/manifest.json`. Also compile once with
   `UNITY_ANDROID` defined instead of `UNITY_STANDALONE`: `#if UNITY_ANDROID` code only exists in phone builds.
4. **Push to `main`.** `.github/workflows/build.yml` builds with the GameCI CLI, signing in with the owner's Unity
   account (secrets `UNITY_EMAIL` + `UNITY_PASSWORD`; free Personal license, no license file). Pushes build the
   **Android APK only** (the owner plays on a phone); run the workflow by hand with `platforms: windows|both` for
   Windows. The `publish` job then creates the GitHub release `v<Version>` with `GoreSandbox-Android.apk` (and
   `GoreSandbox-Windows.zip` when built). Android copies show the update with a download button; Windows copies
   (v0.3.1+) install it themselves. The repository must stay public. Never commit signing keys or passwords: optional
   Android signing comes from secrets `ANDROID_KEYSTORE_BASE64` + `ANDROID_KEYSTORE_PASS` (not set yet, so APKs are
   debug-signed and updating means uninstalling first). Test risky build changes on another branch with
   `gh workflow run build.yml --ref <branch> -f platforms=android` (only `main` publishes).
   The publish job also uploads to itch.io with butler when secret `BUTLER_API_KEY` and `ITCH_GAME`
   ("user/game", variable or secret) exist. Community links live in `GameInfo` (`DiscordUrl`, `ItchUrl`, `EarlyAccess`).
5. After pushing, watch the Actions run (`gh run list -R DrPhantomDotNet/Gore-Sandbox`) and report the result.

## Unity editor content (for the owner)

- Weapons (`GoreWeaponAsset`) and stories (`GoreStoryAsset`) can be ScriptableObject assets under
  `Assets/Resources/GoreContent`; `Scripts/Content/GoreContent.cs` loads them. `[GoreMenuButton]` screens appear in EXTRAS.
- Editor tools: `Editor/GoreDevHub.cs` (SolarEclipse > Dev Hub), `Editor/GoreContentEditors.cs`, `Editor/GoreStoryDrawers.cs`.
  Guide for humans: `Assets/SolarEclipse/DEVELOPING.md`. Keep it in sync when these change.

## VR (Windows only)

- OpenXR via `com.unity.xr.management` + `com.unity.xr.openxr` (manifest). `Editor/VRSetup.cs` configures them by reflection
  (OpenXR loader for Standalone, init-on-start OFF, controller profiles, Multi Pass because the custom unlit shaders have no
  single-pass-instanced support). Runtime code only uses UnityEngine.XR (XRModule/VRModule/SubsystemsModule; the strict
  compile checks reference those) and reflection. `Scripts/VR`: VRManager (start/stop, input), VRRig (player), VRUI (panel).

## Open World (Port Eclipse)

- `Scripts/OpenWorld`: map (`PortEclipseMap`), `OWDirector` (loadout, keys, host world: cars, people, police, wanted,
  bounties), `OWHeists`, `OWJobs`, `OWPhone` + `PhoneScreen`, `OWHud` (minimap), shops/locker/garage (`OWScreens`),
  items (`OWItems`, ids are saved: never rename), cash save (`OWSave`, local + capped), cars (`OWCar`, networked props "car|...").
- Network: Msg 230-241 in `OWNet.cs` (requests and events share one layout). Interactables that act locally implement
  `ILocalInteractable` so clients don't relay them to the host.

## Conventions

- UI is IMGUI through the `UI` static class (`UI/UIKit.cs`) on a virtual canvas 1080 units high.
- Multiplayer is host-authoritative (`Scripts/Net`). World-changing code must respect `NetState` (only the host or
  singleplayer runs AI, spawning and physics; clients send requests).
- Don't change the dev panel login (stored as SHA-256 hashes in `Dev/DevPanel.cs`).
