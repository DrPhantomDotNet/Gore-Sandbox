using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SolarEclipse.EditorTools
{
    /// <summary>
    /// Inspector drawing for story steps: a one-line summary (click to open), then only the fields that step type
    /// uses, with friendly names. While the game is playing, "use my position / my view" buttons copy where you are
    /// standing or looking into the step (changes made in Play mode stay, because stories are assets).
    /// </summary>
    [CustomPropertyDrawer(typeof(StoryStep))]
    public sealed class StoryStepDrawer : PropertyDrawer
    {
        static readonly Dictionary<StepType, (string field, string label, string tip)[]> Fields = new Dictionary<StepType, (string, string, string)[]>
        {
            { StepType.Say, new[] { ("who", "Speaker", "Name shown on the dialogue box (use an actor's name to colour it)."), ("text", "Line", "") } },
            { StepType.Choice, new[] { ("who", "Speaker", ""), ("text", "Question", ""), ("options", "Answers", "Up to 4 buttons."), ("jumps", "Go to label", "One per answer: the label to jump to (empty = carry on).") } },
            { StepType.Chat, new[] { ("who", "Sender", ""), ("text", "Message", "Shown as a phone text in the corner.") } },
            { StepType.Objective, new[] { ("text", "Objective", "") } },
            { StepType.Banner, new[] { ("who", "Big title", ""), ("text", "Subtitle", "") } },
            { StepType.Cutscene, new[] { ("pos", "Camera start", ""), ("look", "Looks at (start)", ""), ("pos2", "Camera end", ""), ("look2", "Looks at (end)", ""), ("time", "Seconds", ""), ("text", "Caption", "Text at the bottom of the screen.") } },
            { StepType.Wait, new[] { ("time", "Seconds", "") } },
            { StepType.GoTo, new[] { ("pos", "Marker position", ""), ("radius", "Radius (m)", ""), ("text", "Objective", "") } },
            { StepType.Talk, new[] { ("target", "Actor id", "The actor to talk to (INTERACT / E)."), ("text", "Objective", "Empty = \"Talk to <name>\".") } },
            { StepType.Spawn, new[] { ("target", "Actor id", "") } },
            { StepType.Kill, new[] { ("target", "Actor id", "An actor id, or \"enemies\" for every hostile actor that's alive."), ("text", "Objective", "") } },
            { StepType.Remove, new[] { ("target", "Actor id", "") } },
            { StepType.Give, new[] { ("target", "Weapon id", "e.g. glock, ak47, katana (see Weapons/WeaponDef.cs and Weapons/ArsenalPack.cs).") } },
            { StepType.Music, new[] { ("target", "Track", "calm, tense, fight - or empty to stop.") } },
            { StepType.Jump, new[] { ("target", "Label", "") } },
            { StepType.SetFlag, new[] { ("target", "Flag", "A word to remember (checked later with If Flag).") } },
            { StepType.IfFlag, new[] { ("target", "Flag", ""), ("text", "Then go to label", "") } },
            { StepType.Fade, new[] { ("time", "Seconds of black", ""), ("text", "Caption", "") } },
            { StepType.End, new[] { ("target", "Ending", "pass = MISSION PASSED, fail = MISSION FAILED, end = THE END screen."), ("who", "Title / ending name", ""), ("text", "Text", "") } },
            { StepType.Unlock, new[] { ("target", "Achievement id", "") } },
            { StepType.Heal, new (string, string, string)[0] },
        };

        const float Pad = 2f;
        static float Line => EditorGUIUtility.singleLineHeight;

        static StepType TypeOf(SerializedProperty p)
        {
            int t = p.FindPropertyRelative("type").intValue;
            return System.Enum.IsDefined(typeof(StepType), t) ? (StepType)t : StepType.Wait;
        }

        static string Summary(SerializedProperty p)
        {
            var t = TypeOf(p);
            string who = p.FindPropertyRelative("who").stringValue, text = p.FindPropertyRelative("text").stringValue, target = p.FindPropertyRelative("target").stringValue;
            string label = p.FindPropertyRelative("label").stringValue;
            string s;
            switch (t)
            {
                case StepType.Say: case StepType.Choice: case StepType.Chat: s = who + ": " + text; break;
                case StepType.Objective: case StepType.GoTo: case StepType.Fade: s = text; break;
                case StepType.Banner: s = who + " / " + text; break;
                case StepType.Cutscene: s = p.FindPropertyRelative("time").floatValue.ToString("0.#") + "s " + text; break;
                case StepType.Wait: s = p.FindPropertyRelative("time").floatValue.ToString("0.##") + "s"; break;
                case StepType.IfFlag: s = target + " -> " + text; break;
                case StepType.End: s = target + " " + who; break;
                default: s = target; break;
            }
            if (s != null && s.Length > 60) s = s.Substring(0, 60) + "...";
            return (string.IsNullOrEmpty(label) ? "" : "[" + label + "]  ") + t + "   " + s;
        }

        static bool HasButtons(StepType t) => t == StepType.Cutscene || t == StepType.GoTo;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = Line + Pad;
            if (!property.isExpanded) return h;
            h += (Line + Pad) * 2; // type + label
            if (Fields.TryGetValue(TypeOf(property), out var fs))
                foreach (var f in fs)
                {
                    var sp = property.FindPropertyRelative(f.field);
                    h += EditorGUI.GetPropertyHeight(sp, true) + Pad;
                }
            if (HasButtons(TypeOf(property))) h += Line + Pad * 2;
            return h + 4;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var r = new Rect(position.x, position.y, position.width, Line);
            property.isExpanded = EditorGUI.Foldout(r, property.isExpanded, Summary(property), true);
            if (!property.isExpanded) { EditorGUI.EndProperty(); return; }
            EditorGUI.indentLevel++;
            r.y += Line + Pad;
            var typeProp = property.FindPropertyRelative("type");
            var t = TypeOf(property);
            var nt = (StepType)EditorGUI.EnumPopup(r, new GUIContent("Type"), t);
            if (nt != t) typeProp.intValue = (int)nt;
            r.y += Line + Pad;
            EditorGUI.PropertyField(r, property.FindPropertyRelative("label"), new GUIContent("Label", "Optional name of this step, so Choice / Jump / If Flag can go to it."));
            r.y += Line + Pad;
            if (Fields.TryGetValue(nt, out var fs))
                foreach (var f in fs)
                {
                    var sp = property.FindPropertyRelative(f.field);
                    float h = EditorGUI.GetPropertyHeight(sp, true);
                    EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(f.label, f.tip), true);
                    r.y += h + Pad;
                }
            if (HasButtons(nt))
            {
                r.y += Pad;
                var p = PlayerController.Instance;
                bool live = Application.isPlaying && p != null && p.Cam != null;
                using (new EditorGUI.DisabledScope(!live))
                {
                    var b = new Rect(r.x + 15 * EditorGUI.indentLevel, r.y, (r.width - 15 * EditorGUI.indentLevel - 6) / 2f, Line);
                    if (nt == StepType.GoTo)
                    {
                        if (GUI.Button(b, new GUIContent("Use my position", "Play the game, walk there, click this."))) property.FindPropertyRelative("pos").vector3Value = p.Position;
                    }
                    else
                    {
                        if (GUI.Button(b, new GUIContent("Start = my view", "Play the game, look at the shot, click this.")))
                        {
                            property.FindPropertyRelative("pos").vector3Value = p.Cam.transform.position;
                            property.FindPropertyRelative("look").vector3Value = p.Cam.transform.position + p.Cam.transform.forward * 10f;
                        }
                        b.x += b.width + 6;
                        if (GUI.Button(b, new GUIContent("End = my view")))
                        {
                            property.FindPropertyRelative("pos2").vector3Value = p.Cam.transform.position;
                            property.FindPropertyRelative("look2").vector3Value = p.Cam.transform.position + p.Cam.transform.forward * 10f;
                        }
                    }
                }
            }
            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }
    }

    /// <summary>Story actors: role as a dropdown, boss health only for bosses, "use my position" in Play mode.</summary>
    [CustomPropertyDrawer(typeof(StoryActorDef))]
    public sealed class StoryActorDrawer : PropertyDrawer
    {
        const float Pad = 2f;
        static float Line => EditorGUIUtility.singleLineHeight;
        static readonly string[] Names = { "id", "name", "role", "look", "seed", "pos", "yaw", "atStart", "health" };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return Line + Pad;
            float h = Line + Pad;
            foreach (var n in Names)
            {
                if (n == "health" && property.FindPropertyRelative("role").intValue != (int)ActorRole.Boss) continue;
                h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(n), true) + Pad;
            }
            return h + Line + Pad * 3;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var r = new Rect(position.x, position.y, position.width, Line);
            var role = (ActorRole)Mathf.Clamp(property.FindPropertyRelative("role").intValue, 0, (int)ActorRole.Boss);
            property.isExpanded = EditorGUI.Foldout(r, property.isExpanded, property.FindPropertyRelative("id").stringValue + "  -  " + property.FindPropertyRelative("name").stringValue + "  (" + role + ")", true);
            if (!property.isExpanded) { EditorGUI.EndProperty(); return; }
            EditorGUI.indentLevel++;
            r.y += Line + Pad;
            foreach (var n in Names)
            {
                var sp = property.FindPropertyRelative(n);
                if (n == "health" && role != ActorRole.Boss) continue;
                float h = EditorGUI.GetPropertyHeight(sp, true);
                var rr = new Rect(r.x, r.y, r.width, h);
                switch (n)
                {
                    case "id": EditorGUI.PropertyField(rr, sp, new GUIContent("Id", "Short unique word used by steps (Talk / Kill / Spawn).")); break;
                    case "role":
                        var nr = (ActorRole)EditorGUI.EnumPopup(rr, new GUIContent("Role", "Talker: can't be hurt. Civilian: normal person. Gunman/Soldier/Brawler/Zombie: enemies. Boss: enemy with a health bar."), role);
                        sp.intValue = (int)nr;
                        break;
                    case "look": EditorGUI.PropertyField(rr, sp, new GUIContent("Look code", "Empty = made from the seed. Paste a code from the in-game Goredoll customizer, or @marcus for Big Marcus.")); break;
                    case "pos": EditorGUI.PropertyField(rr, sp, new GUIContent("Position (feet)")); break;
                    case "yaw": EditorGUI.PropertyField(rr, sp, new GUIContent("Facing (degrees)")); break;
                    case "atStart": EditorGUI.PropertyField(rr, sp, new GUIContent("Spawn at start", "Off: appears with a Spawn step.")); break;
                    case "health": EditorGUI.PropertyField(rr, sp, new GUIContent("Boss health")); break;
                    default: EditorGUI.PropertyField(rr, sp, true); break;
                }
                r.y += h + Pad;
            }
            var p = PlayerController.Instance;
            using (new EditorGUI.DisabledScope(!(Application.isPlaying && p != null)))
            {
                var b = new Rect(r.x + 15 * EditorGUI.indentLevel, r.y + Pad, r.width - 15 * EditorGUI.indentLevel, Line);
                if (GUI.Button(b, new GUIContent("Put here (my position + facing)", "Play the game, stand where they should be, click this.")) && p != null)
                {
                    property.FindPropertyRelative("pos").vector3Value = p.Position;
                    var f = p.Cam != null ? p.Cam.transform.forward : p.transform.forward;
                    property.FindPropertyRelative("yaw").floatValue = Mathf.Repeat(Mathf.Atan2(-f.x, -f.z) * Mathf.Rad2Deg, 360f); // facing you
                }
            }
            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }
    }
}
