# Developing Gore Sandbox in Unity

Open the project in **Unity 6** (6000.0.23f1 or newer 6000.0.x). On first open it sets itself up and creates
`Assets/Scenes/Main.unity`. Then open **SolarEclipse > Dev Hub**: that window has everything below as buttons.

The game builds its world from code when you press **Play**, so the scene looks empty in Edit mode. That's normal:
press Play to see the game.

## Which way is easiest?

| You want to... | Easiest way | Code? |
| --- | --- | --- |
| A new gun / knife / bat | **Weapon asset** (Dev Hub > Create weapon), or build it in the game's Weapon Editor and paste the share code into the asset | No |
| A new story mission | **Story asset** (Dev Hub > Create story) and edit the steps in the Inspector | No |
| A new menu / screen / GUI | **Screen script** (Dev Hub > Create screen): a ready C# file with buttons, text and a slider; change the text and numbers | A little |
| Change how the game works (AI, gore, maps, physics) | Edit the C# in `Assets/SolarEclipse/Scripts` | Yes |

Weapons and stories are **assets**: you edit them in the Inspector like any Unity asset, nothing to code. They must
be inside `Assets/Resources/GoreContent` (the Dev Hub puts them there) or the game won't find them.

## Weapons (no code)

1. **SolarEclipse > Dev Hub > Create weapon** (pick a template: Pistol, Rifle, Shotgun, Blaster, Knife, Bat).
2. Select it. In the Inspector:
   - **Display Name / Description**: what players see.
   - **Design > Parts**: the 3D model is made of simple shapes (Box, Cylinder, Sphere, Capsule, Wedge, Tube) with a
     position, rotation, size, colour and material (Metal, Polymer, Wood, Glow). +Z is forward, units are metres.
   - **Mag Part / Slide Part**: which part drops out on reload / moves when firing (the part's number, -1 = none).
   - **Muzzle Pos** (bullet source), **Grip Pos** (right hand), **Support Pos** (left hand), **Sight Rear / Front**,
     **Eject Pos** (shell casings).
   - **Trigger**: 0 semi-auto, 1 full auto, 2 burst, 3 charge, 4 pump/bolt. **Projectile**: 0 bullet, 1 visible bolt,
     2 explosive.
   - Damage, fire rate (rpm), pellets, spread, range, recoil, magazine, reload, sound, melee settings...
3. **Check limits** shows anything that the fair limits (the anti-cheat caps) will reduce.
4. Press **Play**, then **TEST IN GAME** in the Inspector: it puts the weapon in your hands on a test map.

Easier way to make the model: in the game, **MODS > NEW WEAPON** has a 3D editor. Build it there, press
**SHARE CODE**, then paste the code into the weapon asset's **Share code** box and press **Import**.

Editor-made weapons are part of the game: they're in the spawn menu without the MODDED tag.

## Story missions (no code)

1. **Dev Hub > Create story**. It comes with a small working example on Flatgrass Lab.
2. In the Inspector: title, description, **Map**, **Time of day**, **Starting weapons**, then:
   - **Actors**: everyone in the story. Each has an **Id** (a short word the steps use), a name and a **Role**:
     Talker (can't be hurt, like Big Marcus), Civilian, Gunman, Soldier, Brawler, Zombie, Boss (big health bar).
     **Look code**: empty = random from the seed, `@marcus` for Big Marcus, or a code from the game's Goredoll
     customizer (spawn menu > CUSTOMIZE: it has a **LOOK CODE** button when you play in the Unity editor).
   - **Steps**: played from top to bottom. Click a step to open it; it only shows the fields it uses.

| Step | What it does |
| --- | --- |
| Say | Dialogue box: speaker + line |
| Choice | Question with up to 4 answers; each answer can jump to a **Label** |
| Chat | Phone text in the corner |
| Objective | Sets the objective (top-left) |
| Banner | Big title in the middle of the screen |
| Cutscene | Camera moves from one view to another, with a caption (letterbox) |
| Wait | Waits some seconds |
| GoTo | Player must reach a marker |
| Talk | Player must talk to an actor (INTERACT / E) |
| Spawn / Remove | Brings an actor in / takes them out |
| Kill | Player must kill an actor, or `enemies` (everyone hostile) |
| Give | Gives a weapon (`glock`, `ak47`, `katana`, `deagle`...) |
| Music | `calm`, `tense`, `fight`, or empty to stop |
| Jump | Goes to a label |
| Set Flag / If Flag | Remembers a choice and checks it later |
| Fade | Fades to black with a caption |
| End | `pass` = MISSION PASSED, `fail` = MISSION FAILED, `end` = THE END screen with an ending name |
| Heal | Full health |

3. **Placing things where you stand:** press **Play**, select the story, walk around in the game and click
   **Put here** (actors), **Use my position** (GoTo markers) or **Start/End = my view** (cutscene cameras).
   Changes made while playing are kept.
4. **Check story** finds missing labels, actors and weapons. **PLAY THIS STORY** (in Play mode) plays it.
5. Players find it in **PLAY STORY > MORE STORIES** and in the main menu's **EXTRAS**.

The built-in chapter 1 is written the same way in code: `Scripts/Story/Chapter1.cs` (good examples to copy from).

## Screens and custom GUI (a little code)

1. **Dev Hub > Create screen**, type a class name (e.g. `ShopScreen`).
2. It creates `Assets/SolarEclipse/Scripts/Custom/ShopScreen.cs` and opens it. Press **Play**: the main menu has an
   **EXTRAS** button with your screen in it.
3. Everything is drawn in `Draw()` every frame on a canvas 1080 units high (`UI.VW` wide). Useful calls:

```csharp
var r = UI.Centered(900, 700);                                   // a box in the middle of the screen
UI.Panel(r, "Title");                                            // window with a title
UI.Label(new Rect(x, y, w, 40), "Text", 24, Color.white);         // text
if (UI.Button(new Rect(x, y, 300, 70), "OK", ButtonKind.Primary)) { /* clicked */ }
UI.Slider(rect, "Volume", ref value, 0f, 1f);                     // slider
UI.Toggle(rect, "Option", ref on);                                // on/off switch
UI.Selector(rect, "Pick", ref index, new[] { "A", "B" });         // left/right choice
text = UI.TextField(rect, text, "unique_id", false, "hint");      // text box
UI.Tex(rect, IconFactory.Get("skull"), Color.white);              // an icon
UIRoot.Push(new OtherScreen());  UIRoot.Pop();                    // open / close screens
GameEvents.ShowToast("Hello");  AudioManager.Ui("ui_success");    // message / sound
```

Look at any screen in `Scripts/UI/Screens` for more examples (`StoryMenuScreen.cs` is a short one).
Remove the `[GoreMenuButton(...)]` line if you don't want it in EXTRAS and open it from your own code instead.

## Where things are

| Folder | What's inside |
| --- | --- |
| `Scripts/Core` | GameInfo (version!), settings, stats, achievements, auto-updater |
| `Scripts/Game` | GameManager (loading maps, menus), bootstrap, photo mode |
| `Scripts/World` | Maps (each map is a C# builder, e.g. `DocksMap.cs`), props, story mode chapter 1 mission 1 |
| `Scripts/Story` | Story engine, chapter 1 missions 2-4 |
| `Scripts/Goredoll` | The ragdoll people, AI, bosses |
| `Scripts/Weapons` | Built-in weapons (`WeaponDef.cs`, `ArsenalPack.cs`), guns, melee |
| `Scripts/Mods` | Modding Hub, Weapon Editor data, SandScript |
| `Scripts/UI` | The UI kit (`UIKit.cs`), HUD, every screen |
| `Scripts/Content` | The editor-made content loader (weapon/story assets, EXTRAS) |
| `Editor` | Dev Hub, Inspectors, project setup, cloud build |
| `Resources/GoreContent` | Your weapons and stories |

## Shipping your changes

1. Bump `Version` in `Scripts/Core/GameInfo.cs` (players only update when it goes up).
2. Add a changelog entry at the top of `Scripts/UI/Screens/ChangelogScreen.cs`.
3. Push to `main` on GitHub. The cloud build makes the Android APK and the auto-updater offers it to players.
   For Windows too: GitHub > Actions > Build Gore Sandbox > Run workflow > platforms: both.
