using System.Collections.Generic;
using SolarEclipse.Mods;
using UnityEditor;
using UnityEngine;

namespace SolarEclipse.EditorTools
{
    /// <summary>Weapon asset Inspector: templates, import from an in-game share code, check the limits, test in game.</summary>
    [CustomEditor(typeof(GoreWeaponAsset))]
    public sealed class GoreWeaponAssetEditor : Editor
    {
        int template;
        string code = "", message;
        MessageType messageType;

        public override void OnInspectorGUI()
        {
            var w = (GoreWeaponAsset)target;
            GoreEditorUtil.ResourcesWarning(w);

            EditorGUILayout.LabelField("Quick start", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                template = EditorGUILayout.Popup("Template", template, WeaponDesign.Templates);
                if (GUILayout.Button("Load", GUILayout.Width(70)) && EditorUtility.DisplayDialog("Load template", "Replace this weapon's parts and stats with the " + WeaponDesign.Templates[template] + " template?", "Replace", "Cancel"))
                {
                    Undo.RecordObject(w, "Load weapon template");
                    w.design = WeaponDesign.Template(template);
                    EditorUtility.SetDirty(w);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                code = EditorGUILayout.TextField(new GUIContent("Share code", "Build a weapon in the game's Weapon Editor (with the 3D preview), press SHARE CODE, paste it here."), code);
                if (GUILayout.Button("Import", GUILayout.Width(70))) Import(w);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Check limits")) Check(w);
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    if (GUILayout.Button(new GUIContent(Application.isPlaying ? "TEST IN GAME" : "TEST IN GAME (press Play first)", "Equips it on a test map right now.")))
                        ModWeapons.Test(w.ToPackage());
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageType);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        void Import(GoreWeaponAsset w)
        {
            var p = ModCodec.FromCode(code, out var err);
            if (p == null) { message = err ?? "That isn't a share code."; messageType = MessageType.Error; return; }
            if (p.Kind != ModKind.Weapon) { message = "That code is a " + p.Kind + " mod, not a Weapon Editor weapon."; messageType = MessageType.Error; return; }
            var e = WeaponRules.Parse(p.data, out var d, null);
            if (d == null) { message = e ?? "Couldn't read the weapon."; messageType = MessageType.Error; return; }
            Undo.RecordObject(w, "Import weapon");
            w.design = d;
            w.displayName = p.name;
            w.description = p.description;
            EditorUtility.SetDirty(w);
            message = "Imported '" + p.name + "'."; messageType = MessageType.Info;
            code = "";
        }

        void Check(GoreWeaponAsset w)
        {
            var notes = new List<string>();
            var err = WeaponRules.Parse(JsonUtility.ToJson(w.design), out _, notes);
            if (err != null) { message = err; messageType = MessageType.Error; }
            else if (notes.Count > 0) { message = "Works, but these get limited in game: " + string.Join(", ", notes.ToArray()); messageType = MessageType.Warning; }
            else { message = "All good: within every limit."; messageType = MessageType.Info; }
        }
    }

    /// <summary>Story asset Inspector: add steps by type, check the story, play it.</summary>
    [CustomEditor(typeof(GoreStoryAsset))]
    public sealed class GoreStoryAssetEditor : Editor
    {
        StepType addType = StepType.Say;
        string message;
        MessageType messageType;
        int mapIndex = -1;
        static string[] mapIds, mapNames;

        public override void OnInspectorGUI()
        {
            var s = (GoreStoryAsset)target;
            if (s.story == null) s.story = new StoryPack();
            GoreEditorUtil.ResourcesWarning(s);
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("includeInGame"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("order"));
            var story = serializedObject.FindProperty("story");
            EditorGUILayout.PropertyField(story.FindPropertyRelative("title"));
            EditorGUILayout.PropertyField(story.FindPropertyRelative("description"));

            // Map as a dropdown of the real maps.
            var mapProp = story.FindPropertyRelative("map");
            if (mapIds == null)
            {
                try
                {
                    var maps = MapCatalog.All;
                    mapIds = new string[maps.Count]; mapNames = new string[maps.Count];
                    for (int i = 0; i < maps.Count; i++) { mapIds[i] = maps[i].Id; mapNames[i] = maps[i].Name + "  (" + maps[i].Id + ")"; }
                }
                catch (System.Exception) { mapIds = new string[0]; }
            }
            if (mapIds.Length == 0) EditorGUILayout.PropertyField(mapProp, new GUIContent("Map id"));
            else
            {
                mapIndex = System.Array.IndexOf(mapIds, mapProp.stringValue);
                int ni = EditorGUILayout.Popup("Map", Mathf.Max(0, mapIndex), mapNames);
                if (ni != mapIndex) mapProp.stringValue = mapIds[ni];
            }
            var timeProp = story.FindPropertyRelative("time");
            var times = new[] { "Map default", "Day", "Sunset", "Night", "Blood Moon" };
            timeProp.intValue = EditorGUILayout.Popup("Time of day", timeProp.intValue + 1, times) - 1;
            EditorGUILayout.PropertyField(story.FindPropertyRelative("weapons"), new GUIContent("Starting weapons", "Weapon ids, e.g. fists, knife, glock, ak47."), true);
            EditorGUILayout.PropertyField(story.FindPropertyRelative("actors"), new GUIContent("Actors (cast)"), true);
            EditorGUILayout.PropertyField(story.FindPropertyRelative("steps"), new GUIContent("Steps (played top to bottom)"), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                addType = (StepType)EditorGUILayout.EnumPopup("Add step", addType);
                if (GUILayout.Button("Add", GUILayout.Width(60)))
                {
                    var steps = story.FindPropertyRelative("steps");
                    steps.arraySize++;
                    var el = steps.GetArrayElementAtIndex(steps.arraySize - 1);
                    el.FindPropertyRelative("type").intValue = (int)addType;
                    el.FindPropertyRelative("label").stringValue = "";
                    el.FindPropertyRelative("who").stringValue = "";
                    el.FindPropertyRelative("text").stringValue = "";
                    el.FindPropertyRelative("target").stringValue = addType == StepType.Kill ? "enemies" : addType == StepType.End ? "pass" : "";
                    el.FindPropertyRelative("options").arraySize = addType == StepType.Choice ? 2 : 0;
                    el.FindPropertyRelative("jumps").arraySize = addType == StepType.Choice ? 2 : 0;
                    el.FindPropertyRelative("time").floatValue = 3f;
                    el.FindPropertyRelative("radius").floatValue = 3f;
                    el.isExpanded = true;
                }
            }
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Check story")) Check(s);
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    if (GUILayout.Button(Application.isPlaying ? "PLAY THIS STORY" : "PLAY THIS STORY (press Play first)"))
                    {
                        var pack = s.ToPack(out var err);
                        if (err != null) { message = err; messageType = MessageType.Error; }
                        else StoryRunner.Play(new StoryContext { Pack = pack });
                    }
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageType);
            EditorGUILayout.HelpBox("Tip: press Play, open this story, and use the \"Put here\" / \"Use my position\" / \"my view\" buttons to place actors, markers and camera shots where you are standing. Changes made while playing are kept.", MessageType.None);
        }

        void Check(GoreStoryAsset s)
        {
            var pack = s.ToPack(out var err);
            if (err != null) { message = err; messageType = MessageType.Error; return; }
            var problems = new List<string>();
            var labels = new HashSet<string>();
            foreach (var st in pack.steps) if (!string.IsNullOrEmpty(st.label)) labels.Add(st.label);
            for (int i = 0; i < pack.steps.Count; i++)
            {
                var st = pack.steps[i];
                string where = "Step " + i + " (" + st.Type + "): ";
                switch (st.Type)
                {
                    case StepType.Talk: case StepType.Spawn: case StepType.Remove:
                        if (pack.Actor(st.target) == null) problems.Add(where + "no actor with id '" + st.target + "'");
                        break;
                    case StepType.Kill:
                        if (st.target != "enemies" && pack.Actor(st.target) == null) problems.Add(where + "no actor with id '" + st.target + "'");
                        break;
                    case StepType.Jump:
                        if (!labels.Contains(st.target)) problems.Add(where + "no step with label '" + st.target + "'");
                        break;
                    case StepType.IfFlag:
                        if (!labels.Contains(st.text)) problems.Add(where + "no step with label '" + st.text + "'");
                        break;
                    case StepType.Choice:
                        if (st.options.Length == 0) problems.Add(where + "no answers");
                        foreach (var j in st.jumps) if (!string.IsNullOrEmpty(j) && !labels.Contains(j)) problems.Add(where + "no step with label '" + j + "'");
                        break;
                    case StepType.Give:
                        if (!StoryRules.WeaponAllowed(st.target)) problems.Add(where + "unknown weapon '" + st.target + "'");
                        break;
                }
            }
            if (!pack.steps.Exists(x => x.Type == StepType.End)) problems.Add("No End step: the story ends with MISSION PASSED after the last step.");
            if (problems.Count == 0) { message = "Looks good: " + pack.steps.Count + " steps, " + pack.actors.Count + " actors."; messageType = MessageType.Info; }
            else { message = string.Join("\n", problems.ToArray()); messageType = MessageType.Warning; }
        }
    }

    static class GoreEditorUtil
    {
        public const string Root = "Assets/Resources/GoreContent";

        public static void ResourcesWarning(Object o)
        {
            var path = AssetDatabase.GetAssetPath(o);
            if (!string.IsNullOrEmpty(path) && !path.Replace('\\', '/').Contains("/Resources/GoreContent/"))
                EditorGUILayout.HelpBox("This asset isn't inside a Resources/GoreContent folder, so the game won't load it. Move it to " + Root + ".", MessageType.Warning);
        }
    }
}
