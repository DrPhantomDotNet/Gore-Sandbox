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
5. After pushing, watch the Actions run (`gh run list -R DrPhantomDotNet/Gore-Sandbox`) and report the result.

## Conventions

- UI is IMGUI through the `UI` static class (`UI/UIKit.cs`) on a virtual canvas 1080 units high.
- Multiplayer is host-authoritative (`Scripts/Net`). World-changing code must respect `NetState` (only the host or
  singleplayer runs AI, spawning and physics; clients send requests).
- Don't change the dev panel login (stored as SHA-256 hashes in `Dev/DevPanel.cs`).
