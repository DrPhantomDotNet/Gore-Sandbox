using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SolarEclipse
{
    public enum WoundKind { GunshotEntry, GunshotExit, Graze, Laceration, Stab, Blunt, Burn, Crush, Amputation, Avulsion }

    /// <summary>One injury on a body part, with enough detail for the wound inspector.</summary>
    public sealed class Wound
    {
        public WoundKind Kind;
        public PartType Part;
        public string Location;
        public string Weapon;
        public float Caliber;
        public float Time;
        public Vector3 LocalPoint, LocalNormal;
        public float BleedRate;          // ml/s now
        public bool Arterial;
        public bool Fracture;
        public bool Penetrating;
        public bool ThroughAndThrough;
        public readonly List<OrganSpec> Organs = new List<OrganSpec>();
        public float Pain;
        public float Damage;

        public string KindName
        {
            get
            {
                switch (Kind)
                {
                    case WoundKind.GunshotEntry: return Caliber > 0 ? $"Gunshot wound (entry, {CaliberName})" : "Penetrating wound";
                    case WoundKind.GunshotExit: return "Gunshot wound (exit)";
                    case WoundKind.Graze: return "Gunshot graze";
                    case WoundKind.Laceration: return "Laceration";
                    case WoundKind.Stab: return "Stab wound";
                    case WoundKind.Blunt: return "Blunt force trauma";
                    case WoundKind.Burn: return "Blast injury / burns";
                    case WoundKind.Crush: return "Crush injury";
                    case WoundKind.Amputation: return "Traumatic amputation";
                    default: return "Avulsion";
                }
            }
        }

        string CaliberName => Caliber >= 18f ? "12 gauge" : Caliber >= 8.5f ? "9mm" : Caliber >= 8f ? "00 buckshot" : Caliber >= 5f ? "5.56mm" : Caliber.ToString("0.0") + "mm";

        public string Severity
        {
            get
            {
                if (Arterial && BleedRate > 20f) return "CRITICAL";
                if (Organs.Exists(o => o.Lethality > 0.5f)) return "CRITICAL";
                if (BleedRate > 8f || Fracture || Organs.Count > 0) return "SEVERE";
                if (BleedRate > 2f || Damage > 25f) return "MODERATE";
                return "MINOR";
            }
        }

        public Color SeverityColor
        {
            get
            {
                switch (Severity)
                {
                    case "CRITICAL": return new Color(1f, 0.2f, 0.2f);
                    case "SEVERE": return new Color(1f, 0.55f, 0.15f);
                    case "MODERATE": return new Color(1f, 0.85f, 0.3f);
                    default: return new Color(0.6f, 0.9f, 0.5f);
                }
            }
        }

        public string BleedingText
        {
            get
            {
                if (BleedRate < 0.2f) return "Not bleeding / clotted";
                string kind = Arterial ? "arterial (bright, pulsatile)" : "venous (dark, steady)";
                string amount = BleedRate > 30f ? "massive" : BleedRate > 12f ? "heavy" : BleedRate > 4f ? "moderate" : "light";
                return $"{amount} {kind} — {BleedRate:0.0} ml/s";
            }
        }

        /// <summary>Medical-style narrative of the injury.</summary>
        /// <summary>GS2: the clinical name of this injury.</summary>
        public string MedicalTerm
        {
            get
            {
                switch (Kind)
                {
                    case WoundKind.GunshotEntry: return ThroughAndThrough ? "perforating GSW, entry" : "penetrating GSW";
                    case WoundKind.GunshotExit: return "GSW exit wound";
                    case WoundKind.Graze: return "tangential GSW (abrasion)";
                    case WoundKind.Laceration: return Damage > 45f ? "deep incised wound" : "incised wound";
                    case WoundKind.Stab: return "penetrating stab wound";
                    case WoundKind.Blunt: return Fracture ? "contusion with fracture" : "contusion / haematoma";
                    case WoundKind.Burn: return Damage > 40f ? "full-thickness burn" : "partial-thickness burn";
                    case WoundKind.Crush: return "crush injury";
                    case WoundKind.Amputation: return "traumatic amputation";
                    default: return "avulsion / degloving";
                }
            }
        }

        /// <summary>GS2: which tissue layers the injury went through, from what we know about it.</summary>
        public string Layers
        {
            get
            {
                var l = new List<string> { "skin" };
                if (Damage > 6f || Penetrating) l.Add("fat");
                if (Damage > 14f || Penetrating || Kind == WoundKind.Amputation) l.Add("muscle");
                if (Fracture || Kind == WoundKind.Amputation) l.Add("bone");
                if (Organs.Count > 0) l.Add("organs");
                if (Kind == WoundKind.Blunt && !Fracture) return "skin, soft tissue (closed)";
                return string.Join(", ", l);
            }
        }

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append(KindName).Append(" to the ").Append(Location).Append('.');
            sb.Append(" [").Append(MedicalTerm).Append("] Layers: ").Append(Layers).Append('.'); // GS2
            if (ThroughAndThrough) sb.Append(" Through-and-through; exit wound present.");
            else if (Kind == WoundKind.GunshotEntry && Penetrating) sb.Append(" Projectile retained.");
            if (Fracture) sb.Append(" Underlying fracture of the ").Append(BoneName).Append('.');
            foreach (var o in Organs) sb.Append(' ').Append(OrganInjury(o));
            return sb.ToString();
        }

        string BoneName
        {
            get
            {
                switch (Part)
                {
                    case PartType.Head: return "skull";
                    case PartType.Neck: return "cervical vertebrae";
                    case PartType.Chest: return "ribs";
                    case PartType.Abdomen: return "lower ribs";
                    case PartType.Pelvis: return "pelvic ring";
                    case PartType.UpperArmL: case PartType.UpperArmR: return "humerus";
                    case PartType.ForearmL: case PartType.ForearmR: return "radius and ulna";
                    case PartType.HandL: case PartType.HandR: return "metacarpals";
                    case PartType.ThighL: case PartType.ThighR: return "femur";
                    case PartType.ShinL: case PartType.ShinR: return "tibia and fibula";
                    default: return "tarsal bones";
                }
            }
        }

        static string OrganInjury(OrganSpec o)
        {
            switch (o.Kind)
            {
                case OrganKind.Brain: return "Penetrating brain injury — non-survivable.";
                case OrganKind.Eye: return "Globe rupture of the " + o.Name + ".";
                case OrganKind.Jaw: return "Mandibular fracture.";
                case OrganKind.Trachea: return "Tracheal laceration — airway compromised, aspirating blood.";
                case OrganKind.Carotid: return "Laceration of the " + o.Name + " — exsanguinating haemorrhage.";
                case OrganKind.CervicalSpine: return "Cervical spinal cord injury.";
                case OrganKind.Heart: return "Cardiac laceration — tamponade / cardiac arrest imminent.";
                case OrganKind.Aorta: return "Laceration of the " + o.Name + ".";
                case OrganKind.Lung: return "Penetrating injury of the " + o.Name + " — haemopneumothorax.";
                case OrganKind.ThoracicSpine: return "Thoracic spinal cord transection — paralysis below the injury.";
                case OrganKind.LumbarSpine: return "Lumbar spinal cord / cauda equina injury — lower limb paralysis.";
                case OrganKind.Liver: return "Hepatic laceration with heavy bleeding.";
                case OrganKind.Spleen: return "Splenic rupture.";
                case OrganKind.Stomach: return "Gastric perforation.";
                case OrganKind.Kidney: return "Renal laceration (" + o.Name + ").";
                case OrganKind.Intestines: return "Bowel perforation (" + o.Name + ").";
                case OrganKind.Bladder: return "Bladder rupture.";
                case OrganKind.FemoralArtery: return "Femoral artery transection — life-threatening.";
                case OrganKind.BrachialArtery: return "Brachial artery laceration.";
                case OrganKind.RadialArtery: return "Radial artery laceration.";
                case OrganKind.IliacArtery: return "Laceration of the " + o.Name + ".";
                case OrganKind.Bone: return "Comminuted fracture of the " + o.Name + ".";
                default: return "Injury to the " + o.Name + ".";
            }
        }
    }
}
