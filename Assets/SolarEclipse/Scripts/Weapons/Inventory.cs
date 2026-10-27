using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Nine hotbar slots. Slot 1 = Fists (equipped at start), slot 2 = Spawn Gun. 1–9 / scroll wheel switches.
    /// </summary>
    public sealed class Inventory : MonoBehaviour
    {
        public const int SlotCount = 9;
        public readonly Weapon[] Slots = new Weapon[SlotCount];
        public int CurrentIndex { get; private set; } = -1;
        public Weapon Current => CurrentIndex >= 0 ? Slots[CurrentIndex] : null;
        public float LastSwitchTime { get; private set; } = -10f;
        PlayerController player;
        int pending = -1;
        float switchT;

        public bool ConsumesKick => Current != null && Current.ConsumesKick;

        public void Init(PlayerController p)
        {
            player = p;
            Give("fists", 0, false);
            Give("spawngun", 1, false);
            Select(0, true);
        }

        /// <summary>Puts a weapon in a slot (first free slot when index is -1). Returns the slot index.</summary>
        public int Give(string id, int index = -1, bool equip = true)
        {
            var def = WeaponCatalog.Get(id);
            if (def == null) return -1;
            for (int i = 0; i < SlotCount; i++)
                if (Slots[i] != null && Slots[i].Def.Id == id)
                {
                    if (Slots[i] is Firearm fa) fa.Refill();
                    else if (Slots[i] is IRefillable rf) rf.Refill();
                    if (equip) Select(i);
                    return i;
                }
            if (index < 0) for (int i = 0; i < SlotCount; i++) if (Slots[i] == null) { index = i; break; }
            if (index < 0) index = Mathf.Max(2, CurrentIndex);
            if (Slots[index] != null) Remove(index);
            var w = Weapon.Create(def);
            w.Attach(player);
            Slots[index] = w;
            if (equip) Select(index);
            return index;
        }

        /// <summary>Puts an already-created (custom, non-catalog) weapon into a slot. Used by special maps.</summary>
        public int GiveCustom(Weapon w, int index = -1, bool equip = true)
        {
            if (index < 0) for (int i = 0; i < SlotCount; i++) if (Slots[i] == null) { index = i; break; }
            if (index < 0) index = SlotCount - 1;
            if (Slots[index] != null)
            {
                if (index == CurrentIndex) { Slots[index].Holster(); CurrentIndex = -1; }
                Slots[index].Destroy();
                Slots[index] = null;
            }
            w.Attach(player);
            Slots[index] = w;
            if (equip) { pending = -1; Select(index, true); }
            return index;
        }

        /// <summary>Removes every item (no fallback to fists). Used by special maps.</summary>
        public void Strip()
        {
            Current?.Holster();
            CurrentIndex = -1;
            pending = -1;
            for (int i = 0; i < SlotCount; i++) { Slots[i]?.Destroy(); Slots[i] = null; }
        }

        public bool Has(string id)
        {
            foreach (var s in Slots) if (s != null && s.Def.Id == id) return true;
            return false;
        }

        public void Remove(int index)
        {
            var w = Slots[index];
            if (w == null) return;
            if (index == CurrentIndex) { w.Holster(); CurrentIndex = -1; }
            w.Destroy();
            Slots[index] = null;
            if (CurrentIndex < 0) Select(0, true);
        }

        /// <summary>The 9-slot "Full Arsenal". There are more weapons than slots: the rest come from the spawn menu.</summary>
        public static readonly string[] Arsenal = { "fists", "spawngun", "glock", "shotgun", "ar15", "sniper", "rpg", "frag", "katana" };

        public void GiveAll()
        {
            foreach (var id in Arsenal) if (WeaponCatalog.Get(id) != null && !Has(id)) Give(id, -1, false);
        }

        public void Select(int index, bool instant = false)
        {
            if (index < 0 || index >= SlotCount || Slots[index] == null) { AudioManager.Ui("ui_error", 0.3f); return; }
            if (index == CurrentIndex && pending < 0) return;
            if (Current != null && Current.Busy && !instant && Current is Firearm) { }
            if (instant || Current == null)
            {
                Current?.Holster();
                CurrentIndex = index;
                Slots[index].Equip();
                pending = -1;
            }
            else
            {
                pending = index;
                switchT = 0f;
            }
            LastSwitchTime = Time.time;
            AudioManager.Ui("ui_tab", 0.25f);
            string clip = Slots[index].Def.Kind == WeaponKind.Knife || Slots[index].Def.Kind == WeaponKind.Melee ? "equip_knife" : Slots[index].Def.Kind == WeaponKind.Fists ? "cloth" : "equip_gun";
            AudioManager.Play(clip, player.EyePosition, 0.5f, Random.Range(0.95f, 1.05f), 0f);
        }

        public void Tick(InputFrame f, float dt)
        {
            if (f.Slot >= 0) Select(f.Slot);
            else if (Mathf.Abs(f.Scroll) > 0.01f && (Current == null || !Current.ConsumesScroll))
            {
                int dir = f.Scroll > 0 ? -1 : 1;
                int start = pending >= 0 ? pending : CurrentIndex;
                for (int k = 1; k <= SlotCount; k++)
                {
                    int i = ((start + dir * k) % SlotCount + SlotCount) % SlotCount;
                    if (Slots[i] != null) { Select(i); break; }
                }
            }

            if (pending >= 0)
            {
                // Quick lower of the current weapon, then raise the new one.
                switchT += dt / 0.12f;
                if (Current != null)
                {
                    Player.Viewmodel.TargetPos = Current.Def.HipPos + Vector3.down * 0.2f * switchT;
                }
                if (switchT >= 1f)
                {
                    Current?.Holster();
                    CurrentIndex = pending;
                    pending = -1;
                    Current.Equip();
                }
                return;
            }
            Current?.Tick(f, dt);
        }

        PlayerController Player => player;

        void OnDestroy()
        {
            foreach (var s in Slots) s?.Destroy();
        }
    }
}
