using System.Collections.Generic;
using System.IO;
using SolarEclipse.Mods;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SolarEclipse.EditorTools
{
    /// <summary>
    /// SolarEclipse > Dev Hub: play the game, make weapons / stories / screens without writing game code, and see
    /// everything you've made. Content lives in Assets/Resources/GoreContent (the game loads it from there).
    /// </summary>
    public sealed class GoreDevHub : EditorWindow
    {
        Vector2 scroll;
        int weaponTemplate;
        string newName = "My Thing";
        string screenName = "MyScreen";

        [MenuItem("SolarEclipse/Dev Hub", priority = 1)]
        public static void Open() { var w = GetWindow<GoreDevHub>("Gore Dev Hub"); w.minSize = new Vector2(380, 420); }

        [MenuItem("SolarEclipse/Create/Weapon", priority = 40)] static void MenuWeapon() => Select(CreateWeapon("New Weapon", 0));
        [MenuItem("SolarEclipse/Create/Story (example)", priority = 41)] static void MenuStory() => Select(CreateStory("New Story"));
        [MenuItem("SolarEclipse/Create/Screen script", priority = 42)] static void MenuScreen() => CreateScreenScript("MyScreen");
        [MenuItem("SolarEclipse/Open Developer Guide", priority = 60)] static void MenuGuide() => OpenGuide();

        static void Select(Object o) { if (o == null) return; Selection.activeObject = o; EditorGUIUtility.PingObject(o); }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };

            GUILayout.Label("GORE SANDBOX  v" + GameInfo.Version, title);
            EditorGUILayout.HelpBox("The game builds its world from code when you press Play, so the scene looks empty in Edit mode. That's normal. Make content below; it's picked up automatically.", MessageType.None);

            // ---------------- play
            GUILayout.Label("Play", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(EditorApplication.isPlaying ? "Stop" : "Play the game", GUILayout.Height(28)))
                {
                    if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                    else
                    {
                        ProjectSetup.EnsureSceneAndSettings();
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { EditorSceneManager.OpenScene(ProjectSetup.ScenePath); EditorApplication.isPlaying = true; }
                    }
                }
                if (GUILayout.Button("Developer guide", GUILayout.Height(28))) OpenGuide();
            }
            if (EditorApplication.isPlaying)
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open EXTRAS")) UIRoot.Push(new ExtrasScreen());
                    if (GUILayout.Button("Open Modding Hub")) UIRoot.Push(new ModsScreen());
                    if (GUILayout.Button("Dev panel (F5)")) DevPanel.Toggle();
                }

            // ---------------- weapons
            EditorGUILayout.Space(8);
            GUILayout.Label("Weapons", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("A weapon asset = parts (the 3D model), special points (bullet source, hold points, sights), trigger and stats. Tip: build it in the game's Weapon Editor (3D preview), press SHARE CODE, and paste the code into the asset.", MessageType.None);
            newName = EditorGUILayout.TextField("Name", newName);
            using (new EditorGUILayout.HorizontalScope())
            {
                weaponTemplate = EditorGUILayout.Popup("Start from", weaponTemplate, WeaponDesign.Templates);
                if (GUILayout.Button("Create weapon", GUILayout.Width(120))) Select(CreateWeapon(newName, weaponTemplate));
            }
            ListAssets<GoreWeaponAsset>(a => a.displayName + (a.includeInGame ? "" : "  (off)"));

            // ---------------- stories
            EditorGUILayout.Space(8);
            GUILayout.Label("Stories", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("A story asset = a map, a cast and a list of steps (dialogue, choices, phone texts, cutscenes, objectives, fights, endings). It shows up in PLAY STORY > MORE STORIES and in EXTRAS. The new one comes with a small example to change.", MessageType.None);
            if (GUILayout.Button("Create story (with example steps)")) Select(CreateStory(newName));
            ListAssets<GoreStoryAsset>(a => (a.story != null ? a.story.title : a.name) + (a.includeInGame ? "" : "  (off)"));

            // ---------------- screens
            EditorGUILayout.Space(8);
            GUILayout.Label("Screens (custom menus / GUI)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Creates a ready-to-edit C# screen with buttons, text and a slider. It appears in the main menu's EXTRAS. Edit Draw() to change it; see the guide for every UI call.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                screenName = EditorGUILayout.TextField("Class name", screenName);
                if (GUILayout.Button("Create screen", GUILayout.Width(120))) CreateScreenScript(screenName);
            }
            foreach (var t in TypeCache.GetTypesWithAttribute<GoreMenuButtonAttribute>())
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("  " + t.Name, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Open script", GUILayout.Width(100)))
                    {
                        var guids = AssetDatabase.FindAssets(t.Name + " t:MonoScript");
                        if (guids.Length > 0) AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guids[0])));
                    }
                    using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                        if (GUILayout.Button("Show", GUILayout.Width(60))) UIRoot.Push((UIScreen)System.Activator.CreateInstance(t));
                }

            // ---------------- releasing
            EditorGUILayout.Space(8);
            GUILayout.Label("Releasing", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("To ship your changes to players: bump Version in Scripts/Core/GameInfo.cs, add a changelog entry (Scripts/UI/Screens/ChangelogScreen.cs) and push to GitHub. The cloud build makes the APK and the auto-updater delivers it.", MessageType.None);
            if (GUILayout.Button("Open GameInfo.cs")) OpenScript("Assets/SolarEclipse/Scripts/Core/GameInfo.cs");
            EditorGUILayout.EndScrollView();
        }

        static void ListAssets<T>(System.Func<T, string> label) where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 0) { EditorGUILayout.LabelField("   (none yet)", EditorStyles.miniLabel); return; }
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var a = AssetDatabase.LoadAssetAtPath<T>(path);
                if (a == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool loaded = path.Replace('\\', '/').Contains("/Resources/GoreContent/");
                    GUILayout.Label((loaded ? "  " : "  [not in Resources/GoreContent]  ") + label(a), GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Edit", GUILayout.Width(60))) Select(a);
                }
            }
        }

        // ------------------------------------------------------------------ creating

        static string Folder(string sub)
        {
            var dir = GoreEditorUtil.Root + "/" + sub;
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(GoreEditorUtil.Root)) AssetDatabase.CreateFolder("Assets/Resources", "GoreContent");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(GoreEditorUtil.Root, sub);
            return dir;
        }

        static string SafeFile(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s ?? "") if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-') sb.Append(c);
            var r = sb.ToString().Trim();
            return r.Length == 0 ? "New" : r;
        }

        public static GoreWeaponAsset CreateWeapon(string name, int template)
        {
            var a = CreateInstance<GoreWeaponAsset>();
            a.displayName = string.IsNullOrWhiteSpace(name) ? "My Weapon" : name.Trim();
            a.design = WeaponDesign.Template(template);
            var path = AssetDatabase.GenerateUniqueAssetPath(Folder("Weapons") + "/" + SafeFile(a.displayName) + ".asset");
            AssetDatabase.CreateAsset(a, path);
            AssetDatabase.SaveAssets();
            return a;
        }

        /// <summary>A new story with a small working example on Flatgrass Lab: talk, choose, fight or walk away.</summary>
        public static GoreStoryAsset CreateStory(string name)
        {
            var a = CreateInstance<GoreStoryAsset>();
            var w = new StoryWriter(string.IsNullOrWhiteSpace(name) ? "My Story" : name.Trim(), "flatgrass", (int)TimeOfDay.Sunset).Weapons("fists", "knife", "glock");
            w.Actor("guide", "The Guide", ActorRole.Talker, new Vector3(0, 0, -4), 180f);
            w.Actor("bad1", "Bad Guy", ActorRole.Gunman, new Vector3(-6, 0.05f, 14), 180f, false, null, 5);
            w.Actor("bad2", "Bad Guy", ActorRole.Brawler, new Vector3(6, 0.05f, 14), 180f, false, null, 6);
            w.Shot(new Vector3(-12, 6, -18), new Vector3(0, 1.5f, -4), new Vector3(-3, 2.5f, -10), new Vector3(0, 1.6f, -4), 4f, "SOMEWHERE, SOMETIME")
             .Banner("MY STORY", "Change these steps in the Inspector")
             .Talk("guide", "Talk to The Guide")
             .Say("The Guide", "Hey. Two guys past the walls are looking for trouble.")
             .Choice("The Guide", "Will you deal with them?", "YES", "fight", "NO", "leave")
             .Label("fight")
             .Give("ak47")
             .Say("The Guide", "Take this. Good luck.")
             .Spawn("bad1").Spawn("bad2")
             .Music("fight")
             .Kill("enemies", "Deal with the bad guys")
             .Music("")
             .Chat("The Guide", "Nice work. Come back for your reward... someday.")
             .Pass("You did it.")
             .Label("leave")
             .Say("The Guide", "Fine. I'll find someone else.")
             .TheEnd("The Quiet Life", "You walked away and nothing bad ever happened. Probably.");
            a.story = w.Pack;
            a.story.description = "An example story. Edit it in the Inspector.";
            var path = AssetDatabase.GenerateUniqueAssetPath(Folder("Stories") + "/" + SafeFile(a.story.title) + ".asset");
            AssetDatabase.CreateAsset(a, path);
            AssetDatabase.SaveAssets();
            return a;
        }

        public static void CreateScreenScript(string className)
        {
            var cls = SafeFile(className).Replace(" ", "").Replace("-", "_");
            if (cls.Length == 0 || char.IsDigit(cls[0])) cls = "My" + cls;
            const string dir = "Assets/SolarEclipse/Scripts/Custom";
            Directory.CreateDirectory(dir);
            var path = dir + "/" + cls + ".cs";
            if (File.Exists(path)) { EditorUtility.DisplayDialog("Screen", path + " already exists.", "OK"); OpenScript(path); return; }
            File.WriteAllText(path, ScreenTemplate.Replace("__CLASS__", cls).Replace("__LABEL__", cls.ToUpperInvariant()));
            AssetDatabase.ImportAsset(path);
            OpenScript(path);
            Debug.Log("[Gore Dev Hub] Created " + path + ". Press Play: it's in the main menu under EXTRAS.");
        }

        static void OpenScript(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (s != null) AssetDatabase.OpenAsset(s); else EditorUtility.RevealInFinder(path);
        }

        static void OpenGuide()
        {
            const string p = "Assets/SolarEclipse/DEVELOPING.md";
            var a = AssetDatabase.LoadAssetAtPath<TextAsset>(p);
            if (a != null) { Selection.activeObject = a; AssetDatabase.OpenAsset(a); }
            else EditorUtility.DisplayDialog("Guide", "Missing " + p, "OK");
        }

        const string ScreenTemplate = @"using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// A custom screen. It shows up in the main menu under EXTRAS (that's what [GoreMenuButton] does).
    /// Everything is drawn in Draw() every frame on a canvas 1080 units high (UI.VW = width, UI.VH = height).
    /// Open it from code anywhere with: UIRoot.Push(new __CLASS__());
    /// </summary>
    [GoreMenuButton(""__LABEL__"", ""star"", ""My custom screen"")]
    public sealed class __CLASS__ : UIScreen
    {
        float volume = 0.5f;
        bool option = true;
        int choice;
        string text = """";
        int clicks;

        public override bool IsModal => true;            // blocks the screens under it
        public override void OnOpen() => AudioManager.Ui(""ui_open"");

        public override void Draw()
        {
            float a = Intro(0, 0.25f);                    // 0 -> 1 fade-in
            UI.Dim(0.7f * a);                             // darken the game behind
            var r = UI.Centered(900, 700);                // a 900 x 700 box in the middle
            UI.Panel(r, ""__LABEL__"", a);                  // window with a title

            float x = r.x + 40, y = r.y + 90, w = r.width - 80;
            UI.Label(new Rect(x, y, w, 40), ""Hello! Edit Assets/SolarEclipse/Scripts/Custom/__CLASS__.cs"", 24, Color.white);
            y += 60;
            UI.Slider(new Rect(x, y, w, 60), ""A slider"", ref volume, 0f, 1f, ""0.00"");
            y += 72;
            UI.Toggle(new Rect(x, y, w, 60), ""A switch"", ref option);
            y += 72;
            UI.Selector(new Rect(x, y, w, 60), ""A choice"", ref choice, new[] { ""Red"", ""Green"", ""Blue"" });
            y += 72;
            text = UI.TextField(new Rect(x, y, w, 60), text, ""__CLASS___text"", false, ""Type here..."");
            y += 80;
            if (UI.Button(new Rect(x, y, 300, 70), ""CLICK ME"", ButtonKind.Primary, IconFactory.Get(""star""), 26))
            {
                clicks++;
                AudioManager.Ui(""ui_success"");
                GameEvents.ShowToast(""Clicked "" + clicks + "" times"");
            }
            UI.Label(new Rect(x + 320, y, w - 320, 70), ""Clicks: "" + clicks, 26, UI.Accent2);

            if (UI.Button(new Rect(r.x + 40, r.yMax - 90, 220, 64), ""BACK"", ButtonKind.Normal, IconFactory.Get(""arrow_left""), 22)) OnBack();
        }
    }
}
";
    }
}
