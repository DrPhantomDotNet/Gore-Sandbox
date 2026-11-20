using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 medical kit (wound inspector, second row). Commands are also doll commands 27-31 so clients ask
    /// the host: 27 morphine, 28 chest seal, 29 splint, 30 transfusion, 31 defibrillator.
    /// </summary>
    public static class Medical
    {
        public static void Do(Goredoll d, byte cmd, PartType part)
        {
            if (d == null) return;
            if (d.NetProxy && NetState.IsClient) NetManager.I.RequestDollCommand(d, cmd, part);
            else Apply(d, cmd, part);
            Feedback(d, cmd);
        }

        public static bool CanDefib(Goredoll d)
        {
            var v = d.Vitals;
            if (v.State != LifeState.Dead) return v.HeartRate < 20f && v.State == LifeState.Unconscious;
            return Time.time - v.TimeOfDeath < 60f && !v.BrainDestroyed && !v.Decapitated && v.BloodLossFraction < 0.38f && !v.HeartDestroyed;
        }

        /// <summary>Runs on the host / in singleplayer.</summary>
        public static void Apply(Goredoll d, byte cmd, PartType part)
        {
            if (d == null) return;
            var v = d.Vitals;
            switch (cmd)
            {
                case 27: v.Pain = Mathf.Max(0f, v.Pain - 60f); v.Adrenaline *= 0.5f; v.Consciousness = Mathf.Min(v.Consciousness, 0.85f); break;
                case 28: v.LungDamage = Mathf.Max(0f, v.LungDamage - 0.8f); v.AirwayBlood *= 0.5f; break;
                case 29:
                    if (d.Parts.TryGetValue(part, out var p) && p != null) { p.Fractured = false; foreach (var w in p.Wounds) w.Fracture = false; }
                    v.Pain = Mathf.Max(0f, v.Pain - 15f);
                    break;
                case 30: v.BloodLost = Mathf.Max(0f, v.BloodLost - 500f); break;
                case 31:
                    if (!CanDefib(d)) break;
                    if (v.State == LifeState.Dead)
                    {
                        // Back from the edge: weak pulse, barely conscious, everything else still wrong.
                        v.State = LifeState.Unconscious; v.CauseOfDeath = ""; v.TimeOfDeath = -1f;
                        v.HeartRate = 45f; v.Systolic = 70f; v.RespRate = 8f; v.Consciousness = 0.2f; v.SpO2 = Mathf.Max(v.SpO2, 70f);
                    }
                    else { v.HeartRate = 70f; v.Consciousness = Mathf.Max(v.Consciousness, 0.35f); }
                    foreach (var bp in d.Parts.Values) if (bp != null && bp.Body != null && !bp.Severed) bp.Body.AddForce(Vector3.up * bp.Body.mass * 1.5f, ForceMode.Impulse);
                    break;
            }
        }

        static void Feedback(Goredoll d, byte cmd)
        {
            var pos = d.Position;
            switch (cmd)
            {
                case 27: AudioManager.Play("cloth", pos, 0.5f); GameEvents.ShowToast("Morphine given"); break;
                case 28: AudioManager.Play("cloth", pos, 0.7f); GameEvents.ShowToast("Chest seal applied"); break;
                case 29: AudioManager.Play("cloth", pos, 0.8f); GameEvents.ShowToast("Splint applied"); break;
                case 30: AudioManager.Play("cloth", pos, 0.5f); GameEvents.ShowToast("Blood transfused"); break;
                case 31: AudioManager.Play("zap", pos, 1f); FX.Sparks(pos + Vector3.up * 0.3f, Vector3.up, 16, 3f); GameEvents.ShowToast("CLEAR! Shock delivered"); Stats.Add("defibs"); break;
            }
        }
    }
}
