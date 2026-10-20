# Gore Sandbox — Unity 6 project

**SolarEclipse Games** · Version 0.4.0 (The Horde Update) · Access: DEV

A GoreBox-style physics gore sandbox. Everything is generated in code when the game starts, so there are
no models, textures, audio files or prefabs to import. That includes the Goredoll models, faces, hands,
guns, organs, maps, sounds, icons and UI. The project is small and opens fast.

> **Status:** this project was written and compile-checked outside Unity, against Unity reference
> assemblies. It has not been opened in the Unity editor yet. Expect a first round of tuning once it runs:
> physics strengths, poses, sizes and volumes. See *Known limitations* below.

---

## Opening it

1. Install **Unity 6** (6000.0 LTS; any 6000.x should work) with the **Built-in Render Pipeline**.
   No template is needed.
2. In Unity Hub, choose **Add → Add project from disk** and select this folder (`GoreSandboxUnity`).
3. On first open, `ProjectSetup` runs automatically. It:
   - creates `Assets/Scenes/Main.unity` and adds it to Build Settings;
   - sets the company/product name, the version and Linear colour space;
   - sets input handling to **Both**;
   - sets mobile landscape orientation and the physics layer names.

   If Unity asks to restart because of the input setting, click **Yes**. You can rerun it any time from
   **SolarEclipse → Set Up Project**.
4. Open `Assets/Scenes/Main.unity` and press **Play**. The scene is empty on purpose. A
   `[RuntimeInitializeOnLoadMethod]` bootstrap creates the `GameManager`, which builds everything else.

To build, use **SolarEclipse → Build → Windows / macOS / Linux / Android**, or the normal Build Settings window.

## Controls (PC)

| Key | Action |
|---|---|
| WASD / Shift / Ctrl or C / Space | Move / sprint / crouch / jump |
| 1–9, mouse wheel | Switch item. Slot 1 is Fists (equipped at start), slot 2 is the Spawn Gun |
| LMB / RMB | Fire / aim down sights. With fists: punch (hold to charge a haymaker) / guard. With the knife: slash / stab |
| R / Q / B | Reload / inspect weapon / fire mode (UZI) or Admin Gun mode |
| F | Kick (press F in mid-air for a dropkick) |
| E | Use. On a Goredoll it opens the **wound inspector** |
| V | Noclip. You can't pass through anything named **Boundaries** |
| Esc | Game settings / pause. **With the Spawn Gun held, it opens the Spawn Menu** |
| Tab / F1 / F2 / F3 / F11 | Session stats / help / screenshot / hide HUD / fullscreen |
| F5 | Dev panel (login required) |

**Spawn Gun**

| Input | Action |
|---|---|
| Middle click | Spawn the selected thing where you aim |
| Hold LMB | Grab and carry |
| Scroll | Push the object further away or pull it closer |
| Hold R | Rotate with the mouse. The camera locks; hold Shift to roll |
| F | Freeze it in mid-air |
| RMB | Throw (or unfreeze what you aim at) |
| X / Delete | Delete |
| Z | Undo the last spawn |
| + / − | Scale |

**Mobile:**
- Touch controls turn on automatically on phones. To force them on or off, use **Settings → Controls → Touch controls**.
- On the left is a floating move stick; drag on the right side of the screen to look.
- Buttons: fire, aim, jump, crouch, sprint, reload, use, kick, and the Spawn Gun's spawn / rotate / push / pull. There are also menu and noclip buttons.
- Tap the item bar to switch weapons.

## What's in it

**Boot and menus**
- "SolarEclipse Games" eclipse logo animation with synthesised sound effects.
- Live 3D main menu with an idle Goredoll you can poke.
- Version 0.0.1 / Access DEV shown on the menu.
- Main menu items:
  - **Play**: map select with options for time of day, Goredoll count, loadout and AI ignore.
  - **Settings**: 6 tabs — graphics, audio, controls, gameplay, gore, interface.
  - **Mods**: coming soon.
  - **Credits**: Quibbo/DrPhantom (Owner-Developer), AlexKiller (Developer), You (playing the game).
  - **Quit**: a random farewell message, then the game closes.
- Also on the menu: stats & 27 achievements, changelog and help.

**Maps (5)**

| Map | Notes |
|---|---|
| Flatgrass Lab | — |
| Crimson Arena | Blood moon |
| Downtown | Night |
| Warehouse 13 | Indoor |
| The Backrooms | Secret: an old cheat code on the menu unlocks it, and it has nextbots |

Every map has:
- invisible `Boundaries` walls;
- hidden rubber ducks;
- interactables: doors, buttons, jump pads, fans, a hydraulic press, spike traps, a vending machine, TVs, radios, bells, shooting targets, and explosive barrels and propane tanks.

**Goredoll (the only AI)**
- Active ragdoll. It balances, walks, runs, falls, gets back up and crawls with broken legs. Poses include hands up, cower, clutching a wound, guard, punch, wave and dance.
- Model and face: higher-poly lofted body, hands with fingers, and a sculpted head with eyes, eyelids, brows, lips, a mouth cavity, nose, ears, hair and beards. The face blinks, looks at you, talks and changes expression.
- AI brain:
  - utility AI with a personality (bravery, aggression, curiosity, sociability) and fear/anger;
  - sight cone, hearing (gunshots, impacts, bullets whizzing past), and it notices bodies;
  - nav-grid pathfinding, taking cover, fleeing, surrendering, begging, fighting back with punches and playing dead;
  - presets: Normal, Coward, Brave, Aggressive, Passive.
- **Anatomy and physiology:**
  - 17 body parts, and organs including brain, eyes, trachea, carotids, spine segments, heart, aorta, lungs, liver, stomach, spleen, kidneys, intestines and major arteries.
  - Wound tracks pass through the organs they hit.
  - Simulated blood volume, heart rate, blood pressure, breathing rate, SpO₂, consciousness, shock, GCS/AVPU and cause of death.
  - **Neck shot:** choking, then blood sprays downward from the neck in time with the heartbeat and leaves trails where it lands.
  - **Mid-back (spine) shot:** paralysis while the NPC stays conscious. You hear it breathing; breathing gets heavier with every hit and faster as blood is lost, until death.
  - All vocal sounds stop when the Goredoll is unconscious or dead; blood sounds continue.
- **Wound inspector (E):**
  - body map and live vitals with an ECG trace;
  - a medical description of each wound: gunshot entry/exit, through-and-through, laceration, stab, fracture, amputation, organ involvement and bleeding type;
  - first aid that really changes the simulation: pressure, tourniquet and adrenaline.

**Gore**
- Blood:
  - entry/exit wounds;
  - exit spray that splatters the wall behind;
  - mist, arterial spurts, drips and growing pools;
  - particle blood that leaves decals where it lands.
- Dismemberment:
  - **procedural mesh slicing at the exact place the knife crossed the limb**;
  - capped flesh and bone stumps;
  - decapitation and head gibs.
- Low-poly organs that spill out, plus an X-ray view.
- Gore level 0–3 and 7 blood colours.

**Weapons**

| Weapon | Description |
|---|---|
| Fists | Boxing guard with both hands; jab/cross, charged haymaker, block |
| Knife | Slicing slash, deep stab |
| Glock 19 | 15+1 rounds; slide locks back on empty |
| UZI | 32 rounds; auto/semi |
| AR-15 | 5.56 rifle; penetrates bodies |
| AR-17 | 2-shell semi-auto 12-gauge shotgun; loads one shell at a time |
| Admin Gun | 7 modes: smite, gib, launch, revive, delete, explode, pacify |
| Spawn Gun | — |

The viewmodel shows two hands with jointed fingers on every weapon.

Every gun has:
- hip fire and aim-down-sights;
- recoil springs and spread/bloom;
- muzzle flash, light and smoke;
- tracers and ejected brass/shells;
- surface-specific impacts and bullet holes, ricochets, and wood/glass penetration;
- tactical reloads (+1 in the chamber) vs empty reloads that drop a physical magazine;
- a slide/charging-handle animation, an equip animation and an inspect animation.

**Spawn menu (Esc while holding the Spawn Gun)**

| Tab | Contents |
|---|---|
| AI | Goredoll only, with personality and look options |
| Items | Empty for now, but wired: `SpawnCatalog.RegisterItem` |
| Props | 20, with rendered icons |
| Guns | **Equip** buttons |
| Entities | 5 nextbots |
| Options | Noclip, freeze on spawn, undo, cleanup |

**Game settings (Esc in a map)**
- Includes **No fall damage, Invincibility, AI ignore player**, plus about 30 more rules: infinite ammo, freeze AI, infighting, one-hit kill, always dismember, explosive bullets, time scale, gravity, X-ray, AI thoughts, colliders, and others.

**Dev panel (F5)**
- Login: username `DrPhantom`, password `DrPhantomRocks`. They are stored as SHA-256 hashes; 5 wrong tries lock it for 30 s.
- Live **Unity console output** (logs, warnings, errors and exceptions from any thread) with filters, search, collapse, stack traces and copy.
- Command line with Tab completion, history and `;` chaining.
- **About 150 console commands** (`help` lists them all).
- **90 quick-action buttons**.
- Spawn/give buttons for everything, an AI roster with per-NPC actions, player tools, all rules, and a **sound board with every synthesised sound**.
- FPS graph, memory, physics and system info.

**Item bar** at the top of the screen with photographed weapon icons. In total there are **224 procedural
icon glyphs** plus about 34 rendered model icons (weapons, props, Goredoll portraits, nextbot faces).

**Audio:** about 150 named sounds (343 variants) synthesised on worker threads at startup:
- guns, reload parts, impacts, ricochets and whizzes;
- flesh, bone, arterial spray, drips and gurgles;
- a formant voice synth for screams, words, choking, coughing and breathing;
- footsteps per surface, ambiences, UI, the logo stinger and menu music.

To replace any sound, drop a clip into `Assets/Resources/AudioOverrides/<sound_name>`.

**Easter eggs (16)**, nextbots, and rubber ducks to find. The `eggs` command in the dev console gives hints.

## Multiplayer (v0.2.0)

**Main menu → PLAY MULTIPLAYER.** No paid servers: the host's game *is* the server (peer-to-peer).

- **HOST A SERVER** → pick map, time, Goredolls, max players (2–16), password, PvP, loadout → **HOST SERVER**.
  It pops up in **HOSTED SERVERS** for everyone: instantly on the same Wi-Fi/LAN (broadcast), and online within a few
  seconds (the server card is posted on a free public MQTT broker, used only as a listing board).
- **JOIN** from the list. Internet joins use STUN + UDP hole punching, so most home connections work without port forwarding.
  If two players are both behind strict NATs, the host can forward **UDP 27015**, or use **DIRECT IP**.
- **Host powers (Esc → PLAYERS):** kick, ban (by player id + IP, saved; unban from the BANNED list), teleport to a player,
  PvP on/off, and **Creative mode** per player (or for everyone who joins).
- **Creative mode** unlocks noclip, the Spawn Gun + spawn menu, the Admin Gun and console cheats. Without it, players get
  the host's loadout (fists, or fists + guns). The host always has Creative. Only the host changes game rules.
- **Everything is synchronised:** players (full-body avatars holding their weapon, walking/crouching/aiming/dying),
  Goredolls (pose, vitals, wounds, blood, voice, death, dismemberment and knife slices), props, spawn gun grab/rotate/
  throw/freeze/scale/delete, gunfire + tracers, explosions, nextbots, doors/buttons, rules and time scale. Late joiners
  get the current world. Goredolls and nextbots react to every player.
- **Chat:** T or Enter. **Voice:** hold **G** (push-to-talk) or open mic — choose the exact **microphone** in
  **Settings → Voice**, test it (you hear yourself, with a level meter), set gain/sensitivity/volume, 3D proximity voice,
  mute players from the player list.
- The first time you host, Windows may ask to allow the game through the firewall — click **Allow**.
- **Server codes:** every server has an 8-letter code (host: Esc → PLAYERS). Anyone can join with it from
  **JOIN BY CODE / IP**, also private servers. Codes work on any network.

### School / work network mode (no admin password)

If hosting or joining asks for an **admin password** (Windows Firewall on a school/work PC), turn on
**School / work network mode** at the bottom of the Multiplayer screen. The game then never opens a network port, so
there is no firewall prompt and nothing to approve. Instead, all game traffic goes **out** through free public relay
servers (MQTT brokers) over secure WebSockets — port **443**, the same as any website — so it gets through networks that
block games and UDP. Hosting, the server list and joining by code all work; you only lose a little latency (typically
+50–150 ms).

- Players in school mode and players in normal mode can play together (normal hosts also accept relay joins, and normal
  joiners fall back to the relay automatically if a direct connection fails).
- Relays used (all at once, so one being down doesn't matter): `mqtt.eclipseprojects.io` (wss :443),
  `broker.emqx.io` (wss :8084), `broker.hivemq.com` (wss :8884), `test.mosquitto.org` (wss :8081). The status of each is
  shown under JOIN BY CODE / IP → Network options.
- If your network needs a **web proxy**, it's detected from the system settings; otherwise type it in
  **Network options** (host:port). You can also point everyone at your **own relay** (any MQTT broker URL, e.g.
  `wss://user:pass@my.broker/mqtt`).
- The relays are public: anyone who knows a server code could read that game's traffic. Don't reuse an important
  password as a server password.
- Please follow your school's rules for using its network.

How it works (code in `Scripts/Net/`): host-authoritative simulation; clients run their own movement and send requests
for anything that changes the world; the host sends 20 Hz snapshots plus reliable events. The UDP transport (reliable
ordered channel, fragmentation), STUN, MQTT lobby + relay (TCP / TLS / WebSocket, HTTP proxy CONNECT), LAN discovery and the
ADPCM voice codec are plain C#
(`Scripts/Net/Core/`) and were tested outside Unity (40% packet loss, 25 KB fragmented messages, sequence wrap-around,
LAN broadcast, lobby against a real MQTT broker, relay over secure WebSockets through a restrictive HTTP proxy, codec
quality ≈ 40 dB SNR).

## Project layout

```
Assets/
  Resources/Shaders/          SolarEclipse/Lit, Decal, Particle, Additive, Sky, Xray (built-in RP)
  SolarEclipse/Editor/        ProjectSetup (scene, settings, layers, build menu)
  SolarEclipse/Scripts/
    Core/      damage model (IDamageable, DamageInfo), settings, rules, stats, events, layers
    Game/      Bootstrap, GameManager (state flow), WorldRoot, MenuBackdrop
    Rendering/ MeshBuilder, materials, procedural textures
    Audio/     DSP + synth recipes, SoundBank, AudioManager (buses, pooling)
    Goredoll/  anatomy, model, face, physiology, wounds, active ragdoll, AI brain, voice, bleeding
    Gore/      blood FX, mesh slicer, organs, dismemberment
    Effects/   particles, decals, debris
    Player/    controller, input (keyboard + touch), health
    Weapons/   weapon data (WeaponCatalog), viewmodel + hands, firearms, ballistics, knife, fists, admin & spawn gun
    World/     maps, lighting, nav grid, props, interactables, explosions
    UI/        IMGUI kit, HUD, screens, icons, touch controls
    Net/       multiplayer: NetManager (sessions, sync, requests), avatars, voice chat; Net/Core = transport, STUN, lobby, codec
    Dev/       dev console + commands + F5 panel
    Fun/       nextbots, easter eggs
```

How to add things:
- **A gun:** add a `WeaponDef` to `WeaponCatalog` and a model in `WeaponModels.Build`.
- **A prop:** add one line in `PropFactory`.
- **An item:** call `SpawnCatalog.RegisterItem`.
- **A map:** add a `MapDef` in `MapCatalog`.

## Known limitations

- **Not yet run in Unity.** It compiles cleanly against Unity reference assemblies (runtime, editor and
  editor-define builds), the synthesised audio was tested offline, and the icons were rendered offline.
  The physics feel, animation poses and balancing have not been playtested.
- The Unity 6–only code paths (`linearVelocity`, `PhysicsMaterial`, `Physics.simulationMode`) are behind
  `#if UNITY_6000_0_OR_NEWER` / `UNITY_2022_2_OR_NEWER` and were checked by hand only.
- The UI is IMGUI (`OnGUI`). It works on PC and mobile, but it is not uGUI/UI Toolkit.
- Built-in render pipeline only (URP/HDRP would need the custom shaders ported).
- Viewmodel animations are procedural (code-driven keyframes), not authored animation clips.

## Credits

- **Quibbo/DrPhantom** — Owner-Developer
- **AlexKiller** — Developer
- **You** — playing the game

Inspired by GoreBox (F2Games).
