using UnityEngine;

namespace SolarEclipse
{
    public sealed class ChangelogScreen : UIScreen
    {
        const string Text =
@"<b><size=30>v0.6.1 — EARLY ACCESS</size></b>
<color=#ff9a40>Gore Sandbox is now in Early Access!</color>
• Join the community: new DISCORD button on the main menu (feedback, bug reports, update news)
• EARLY ACCESS badge on the main menu: expect bugs and frequent updates, and please report anything weird
• Updates are now also published to the game's itch.io page

<b><size=30>v0.6.0 — THE PLAYER UPDATE</size></b>
<color=#ff9a40>Smoother, steadier phone controls. Access: Player.</color>
<b>MOBILE CONTROLS</b>
• Fixed: walking with the move stick no longer turns the camera (and no longer shoots by itself)
• Fixed: you can now aim while walking (pushing the stick all the way no longer cancels aiming)
• AIM is now tap to toggle on phones, so your thumb is free to shoot (hold mode in Settings > Controls)
• The FIRE button gets bigger while aiming, and there's a second FIRE button on the left for your other thumb
• Drag on a FIRE button to adjust your aim while shooting
• Slower, steadier look while aiming, smoothed finger jumps and a small dead zone on the move stick
• The move stick follows your thumb if you drag past its edge
• Buttons stay clear of notches and camera holes
<b>PERFORMANCE</b>
• Fixed: phones were stuck at 30 FPS. They now aim for 60
• Lighter default graphics on phones (first launch) so the game stays smooth
• The screen no longer dims or locks while you play
<b>OTHER</b>
• Access is now Player
• Reset Defaults in Settings keeps your name and setup answers

<b><size=30>v0.5.0 — THE ARSENAL UPDATE</size></b>
<color=#ff9a40>20 new things to play with, and a proper phone version.</color>
<b>NEW GUNS</b>
• Pump Shotgun: 10 pellets, pump action, shell-by-shell reload, huge knockback up close
• Sniper Rifle: bolt action, real scope with 5x zoom and sway (hold breath with Sprint)
• .357 Revolver: 6 heavy shots, cylinder and speedloader reload
• Minigun: spins up, 300-round box, slows you down
• Harpoon Gun: impales Goredolls and pins them to walls
<b>EXPLOSIVES & MELEE</b>
• Rocket Launcher with smoke trail and backblast
• Frag Grenades: throw or toss underhand, hold to cook the fuse
• C4: stick it to anything (even Goredolls) and detonate it all at once
• Katana: fast slashing combos and an overhead cleave that cuts clean through
• Sledgehammer: wind it up, then send Goredolls flying
<b>SANDBOX TOOLS</b>
• Rope Tool: tie anything together or hang Goredolls from the ceiling
• Gravity Gun: pull, hold and launch props and Goredolls
• Bullet Time (B or K): slow everything down to a quarter speed
• Photo Mode (P): free camera, filters, FOV and tilt, saves photos
<b>WORLD</b>
• Weather: rain, thunderstorms with lightning, fog and snow (pick it in map select)
• New map: Rooftops, skyscraper tops connected by planks and bridges, high above the city
• Kill streaks: DOUBLE KILL, RAMPAGE, GODLIKE and more, with a banner and sound
<b>GOREDOLLS</b>
• Armed Goredolls: Gunman and Soldier carry guns and shoot back
• Helmets and body armor that stop bullets with sparks, and break off
• Goredoll Customizer: design your own Goredoll (skin, hair, clothes, height, glasses, armor, weapon) and spawn it
<b>MOBILE</b>
• Fixed broken and invisible text on phones, and bigger, readable UI on small screens
• Android APK downloads with every update, and the game offers new versions on your phone
• Mobile dev tools: open the dev panel without a keyboard, plus fun toys (gyro aim, shake to ragdoll, gravity, giant mode, neon blood and more)
• Touch buttons for chat, bullet time and photo mode

<b><size=30>v0.4.0 — THE HORDE UPDATE</size></b>
<color=#ff9a40>Zombies, an endless arena and your own maps.</color>
<b>ZOMBIES</b>
• 5 zombie types in the spawn menu: Walker, Runner, Crawler, Brute and Screamer
• No fear, no emotions: they chase you and Goredolls on sight (unless AI ignores you is on), groan, snarl and bite
• They eat downed Goredolls. Anyone bitten rises 3 seconds after dying as a pale green zombie
• Only 3 headshots (or losing their head) put a zombie down for good. Body shots just knock them over
• The Screamer's shriek calls every zombie nearby

<b>ARENA (HORDE MODE)</b>
• New map: Arena. Endless rounds of zombies or hostile Goredolls, picked at random each round
• Round 1 has 1 enemy and every round doubles: 1, 2, 4, 8, 16...
• Guns and ammo refill every round, your best round is saved
• Achievements: Survivor, Horde Breaker, Double Tap, Patient Zero

<b>MAP EDITOR</b>
• Main menu > MAP EDITOR: build your own maps with floors, walls, ramps, stairs, pillars, props, interactables, lights, player and Goredoll spawns
• 13 materials and colours, grid snapping, undo/redo, duplicate, map settings (time, fog, ground, Goredolls)
• Test-play any time, then press Esc to keep editing
• Your maps show up in map select (tagged Custom) and can be hosted in multiplayer: players who join download the map automatically

<b><size=30>v0.3.2 — SIGHTS, SETUP & THE BACKROOMS</size></b>
<b>NEW: FIRST-LAUNCH SETUP</b>
• A quick setup when you first start the game: gore level, voice chat and your microphone, your age (only stored on your device) and the chat filter
<b>GORE LEVELS</b>
• Six gore levels: None, Minimum, Low, Medium, High and Unreal (your old setting is converted automatically)
<b>IRON SIGHTS</b>
• Aiming (right click) now looks down real iron sights on the Glock, UZI, AR-15, AR-17 and Admin Gun, with glowing sight dots
<b>THE BACKROOMS</b>
• The Backrooms is now a normal map, unlocked for everyone, and tagged Procedurally Generated: a new endless yellow maze every time you play
• Buzzing, flickering lights, dead panels, pillars, big halls and dark corners. Works in multiplayer (everyone gets the same layout)

<b><size=30>v0.3.1 — AUTO UPDATES</size></b>
• The game now updates itself: when a new version is out, it downloads, installs and restarts into it automatically
• You see the new version's notes and a progress bar while it installs
• Works without an admin password (as long as the game folder isn't in Program Files)
• Can be turned off in Settings > Interface > Automatic updates

<b><size=30>v0.3.0 — ????</size></b>
<color=#ff9a40>Something is waiting in the void.</color>
<b>NEW MAP: ????</b>
• A boss fight in a dark, endless void. Orbie will ask if you're sure... you need blood on your hands (10 Goredoll kills) to enter
• Survive the clock with only a flashlight. Something is hunting you, and your sanity won't last forever
• New achievements: Scaredy Cat, Now it's angry, I'm the owner of this realm now
• Singleplayer only

<b>CHAT</b>
• Chat now works in singleplayer too (T or Enter): type to test, or run commands like /help, /god, /spawn
• /commands also work in multiplayer chat (clients only get the safe ones)
• Profanity filter (Settings > Interface), on by default
• Optional age setting, only stored on your device: under 13 always hides profanity
• Show/hide chat, and a CHAT button on mobile

<b>FIXES</b>
• Kicking now shows your leg every time, not only when jumping
• You can no longer see your own body in multiplayer, and other players' bodies no longer fill your screen when you walk into them

<b><size=30>v0.2.0 — THE SCHOOL NETWORK UPDATE</size></b>
<color=#ff9a40>Play multiplayer without an admin password.</color>
• New: School / work network mode (bottom of the Multiplayer screen). The game opens no network ports, so there's no firewall or admin prompt
• In that mode everything goes through free relay servers over secure web traffic (port 443, like a website), so it works on networks that block games
• Every server now has an 8-letter SERVER CODE (Esc > Players). JOIN BY CODE works on any network, also for private servers
• If a direct connection fails, the game falls back to the relay automatically
• School-mode and normal players can play together
• Network options: web proxy and your own relay server, plus live relay status
• Servers can be Private (code only)

<b><size=30>v0.1.1 — MOBILE FIX</size></b>
• Fixed broken text on phones and tablets (the game now uses its own built-in font on mobile)
• Fixed broken or blank weapon, prop and Goredoll icons on some phone GPUs
• Symbols that phones can't display were replaced (kill feed, ammo, checkmarks)
• New setting: Interface > Use built-in game font (PC), in case text looks wrong there too

<b><size=30>v0.1.0 — THE MULTIPLAYER UPDATE</size></b>
<color=#ff9a40>Play together. No paid servers, no accounts.</color>

<b>MULTIPLAYER</b>
• New PLAY MULTIPLAYER button on the main menu
• HOST a server in one click: pick map, time of day, Goredolls, max players (2-16), password, PvP and loadout
• Hosted servers pop up in the list automatically: instantly on your LAN, and online within seconds
• Peer-to-peer: the host's game is the server. NAT punch-through (STUN) lets most home connections join without port forwarding
• DIRECT IP connect for anything else
• Everything is synchronised: players, Goredolls (pose, vitals, wounds, blood, voice, death), dismemberment and knife slices, props, spawn gun grabs/rotation/freeze/scale/delete, gunfire, tracers, explosions, nextbots, doors and buttons, game rules and time scale
• Players see each other with their weapon in hand, walking, crouching, aiming and dying
• Goredolls and nextbots react to every player, not just the host
• Join mid-game: you get the current world including severed limbs and broken crates

<b>HOST POWERS</b> (Esc > PLAYERS)
• Kick and ban players (bans remember the player and their IP; unban any time)
• Creative mode is OFF for players unless the host grants it: noclip, Spawn Gun, spawn menu, Admin Gun and console cheats need Creative
• Give or take Creative per player, or give it to everyone who joins
• PvP on/off, teleport to players, see everyone's ping
• Only the host changes game rules; they apply to everyone

<b>VOICE CHAT</b>
• Push-to-talk (G) or open mic with adjustable sensitivity
• Pick the exact microphone in Settings > Voice so it never uses the wrong one
• Test your mic and hear yourself, with a live level meter
• 3D proximity voice (or radio mode), per-player mute, mic gain and voice volume
• Speaking indicators over heads, in the HUD and in the player list

<b>CHAT & HUD</b>
• Text chat (T or Enter), join/leave/kick messages, kill feed with who killed what
• Name tags and health bars over players, Tab shows everyone

<b><size=26>v0.0.2</size></b>
• Rewritten active ragdoll balance: Goredolls stand firmly, walk without wobbling and reliably get back up
• Footsteps no longer count as hard impacts (no more random tripping)
• Cloud build setup (GitHub Actions) to build the game without installing Unity

<b><size=30>v0.0.1 — DEV</size></b>
<color=#ff9a40>The first playable build.</color>

<b>GOREDOLLS</b>
• Active ragdoll NPCs that balance, walk, run, limp, crawl, stumble and get back up
• AI brain with sight, hearing, memory, personality, fear and anger
• Flee, hide, surrender, beg, fight back, investigate, wander, play dead
• Full physiology: blood volume, heart rate, breathing, consciousness, shock
• Organs: brain, heart, lungs, liver, stomach, kidneys, spleen, intestines, spine
• Faces with blinking eyes that track you, expressions and a moving mouth

<b>GORE</b>
• Per-limb health, fractures, arterial bleeding, through-and-through wounds
• Dismemberment at joints, procedural knife slicing, gibbing, organ spill
• Blood sprays, pools, drips, trails and wall splatter
• Spinal shots paralyse. Neck shots make them choke.
• Inspect wounds (E) for a medical report

<b>WEAPONS</b>
• Fists, Spawn Gun, Glock 19, Knife, UZI, AR-15, AR-17, Admin Gun
• Reload / inspect / fire animations, tracers, muzzle flash, shell casings
• Kick and dropkick with any item

<b>SANDBOX</b>
• Spawn Gun: spawn, grab, move, rotate, freeze, scale, throw, delete
• Noclip (V) that respects the Boundaries
• Game settings: god mode, no fall damage, AI ignore and much more
• Developer panel (F5) with console output and commands

<b>MAPS</b>
• Flatgrass Lab, Crimson Arena, Downtown, Warehouse 13 and something else...";

        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_open");

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.6f * a);
            var r = UI.Centered(900, Mathf.Min(900, UI.VH - 80));
            UI.Panel(r, "Changelog", a);
            var view = new Rect(r.x + 30, r.y + 84, r.width - 60, r.height - 180);
            float h = UI.TextHeight(Text, 22, view.width - 20) + 40;
            float s = UI.BeginScroll("changelog", view, h);
            UI.Label(new Rect(0, -s, view.width - 20, h), Text, 22, UI.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, false, true);
            UI.EndScroll("changelog", view, h);
            if (UI.Button(new Rect(r.xMax - 270, r.yMax - 82, 240, 60), "CLOSE", ButtonKind.Primary)) OnBack();
        }

        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }
    }
}
