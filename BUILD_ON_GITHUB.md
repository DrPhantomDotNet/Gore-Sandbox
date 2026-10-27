> **Current setup (v0.6.0):** the cloud build signs in with just `UNITY_EMAIL` + `UNITY_PASSWORD` (a free Unity
> account works, no license file needed; 2-step verification must be off). Every push to `main` builds the
> **Android APK** and publishes it as a GitHub release. Windows builds run when you start the workflow by hand
> (Actions → Build Gore Sandbox → Run workflow → platforms: windows or both). The sections below are older notes.

# Build the game .exe on GitHub (no Unity on your PC)

GitHub builds the game in the cloud and gives you a zip with `GoreSandbox.exe`.

The first time takes about 15 minutes of setup. After that, every build is one click.

You need:
- a free **GitHub** account;
- a free **Unity** account;
- 5 minutes on **any** computer that can install **Unity Hub**. This is only the small launcher, not the Unity editor. It's needed once, to get your license file.

---

## Step 1: Get your Unity license file (one time)

1. On any computer, install **Unity Hub** from https://unity.com/download.
2. Open Unity Hub and **sign in** with your Unity account.
3. Go to **Settings (gear icon) → Licenses → Add → Get a free personal license**.
4. Find the license file:

   | Computer | Location |
   |---|---|
   | Windows | `C:\ProgramData\Unity\Unity_lic.ulf` (ProgramData is hidden: paste the path into File Explorer's address bar) |
   | Mac | `/Library/Application Support/Unity/Unity_lic.ulf` |
   | Linux | `~/.local/share/unity3d/Unity/Unity_lic.ulf` |

5. Open `Unity_lic.ulf` with Notepad and **copy everything** in it. You'll paste it in Step 3.

## Step 2: Put the project on GitHub

1. On https://github.com, click **New repository**.
2. Name it e.g. `gore-sandbox`, choose **Private**, and click **Create repository**.
3. Upload the **contents** of the `GoreSandboxUnity` folder, so that `Assets`, `Packages`, `ProjectSettings`, `.github` and `README.md` sit at the top of the repository.

   Easiest: use **GitHub Desktop** (https://desktop.github.com):
   1. **File → Add local repository** → pick the `GoreSandboxUnity` folder → **create a repository** here.
   2. Click **Publish repository**.

   Or upload in the browser with **Add file → Upload files**. The browser accepts at most 100 files at a time, so upload in two batches:
   1. the `Assets` folder;
   2. everything else: `Packages`, `ProjectSettings`, `.github`, `.gitignore`, `README.md`, `BUILD_ON_GITHUB.md`.

   Make sure the **`.github`** folder gets uploaded; it contains the build script.

## Step 3: Add your Unity details as secrets

In your repository, go to **Settings → Secrets and variables → Actions → New repository secret** and add these three:

| Name | Value |
|---|---|
| `UNITY_LICENSE` | Everything you copied from `Unity_lic.ulf` |
| `UNITY_EMAIL` | Your Unity account email |
| `UNITY_PASSWORD` | Your Unity account password |

Secrets are encrypted. Nobody, not even you, can read them back. They are only used to start Unity during the build.

## Step 4: Build

1. Open the **Actions** tab. If GitHub asks, click **I understand my workflows, go ahead and enable them**.
2. Click **Build Gore Sandbox → Run workflow → Run workflow**.
3. Wait. The first build takes about **20–40 minutes**, because GitHub downloads Unity. Later builds are faster.

## Step 5: Download and play

1. When the run shows a green ✓, click it.
2. Scroll to **Artifacts** and download **GoreSandbox-Windows** (a zip).
3. Unzip it and run **`GoreSandbox.exe`**. Keep all the files together; the exe needs the `GoreSandbox_Data` folder next to it.
4. If Windows SmartScreen warns you, click **More info → Run anyway**. The game isn't signed, which is normal for homemade games.

Every time you change the project and push it to GitHub, a new build starts automatically.

---

## If something goes wrong

| What you see | Fix |
|---|---|
| ❌ "Missing repository secrets" | One of the three secrets isn't set, or its name is misspelled. Names must match exactly. |
| ❌ The build fails around "activating license" | Re-copy the whole `.ulf` file into `UNITY_LICENSE`, and check your email/password. If your Unity account uses two-step login with an authenticator app, GameCI may not be able to sign in; turn it off, or check the GameCI docs below. |
| ❌ The build fails with red `error CS...` lines | That's a code error. Copy those lines and send them to me, and I'll fix them. |
| ⏱ It runs out of time or disk space | Click **Re-run jobs**. The second try is usually faster because Unity is cached. |

The build uses GameCI (https://game-ci.com/docs/github/getting-started), the standard free tool for building Unity games on GitHub.

## Auto-updates

Every successful cloud build also publishes a **GitHub release** named after the game version
(`GameInfo.Version`, e.g. `v0.3.1`) with `GoreSandbox-Windows.zip` attached. The build writes your repository name
into the game, so the copies people have check that release page when they start, then download, install and restart
by themselves.

- **To ship an update:** raise `Version` in `Assets/SolarEclipse/Scripts/Core/GameInfo.cs`, add a changelog entry, and push.
  The release notes are taken from the newest changelog entry. Players only update when the version number goes up.
- **The repository must be public** so players' games can download releases without logging in.
- **Admin rights:** updating needs no admin rights unless the game sits in a protected folder like Program Files. In that
  case the game says so and offers the download page instead.
- **Only from 0.3.1:** auto-update is built into 0.3.1 and later. Anyone on an older copy needs to download 0.3.1 once by hand.

## No Unity license for GitHub? Release by hand (auto-update still works)

The cloud build needs the Unity license secrets. Without them it skips itself, and you publish each update from Unity:

1. Get the latest project (GitHub → **Code → Download ZIP**, or pull it) and open it in Unity 6.
2. Menu **SolarEclipse → Build → Windows (x64)**. When it finishes it opens `Builds/GoreSandbox-Windows.zip`.
3. On GitHub: **Releases → Draft a new release**. Tag: `v` + the version shown in the dialog (for example `v0.5.0`).
   Attach `GoreSandbox-Windows.zip` and click **Publish release**.

Players' games (0.3.1 or newer) find that release on startup, then download, install and restart by themselves. The
game already knows the repository (`GameInfo.UpdateRepo = "DrPhantomDotNet/Gore-Sandbox"`).

