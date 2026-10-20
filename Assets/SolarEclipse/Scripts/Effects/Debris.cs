using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Short-lived physics clutter (fragments, casings, magazines, gibs) with a global cap.</summary>
    public static class Debris
    {
        struct Item { public GameObject Go; public float Expire; public int Group; }
        static readonly List<Item> items = new List<Item>();
        static readonly int[] counts = new int[4];
        public const int GroupGeneric = 0, GroupCasing = 1, GroupGib = 2, GroupMag = 3;
        public static int Count => items.Count;

        public static void Register(GameObject go, float lifetime, int group = GroupGeneric)
        {
            items.Add(new Item { Go = go, Expire = Time.time + lifetime, Group = group });
            counts[group]++;
            int cap = group == GroupCasing ? Settings.Data.maxCasings : group == GroupGib ? Settings.Data.maxGibs : 80;
            if (counts[group] > cap)
                for (int i = 0; i < items.Count; i++)
                    if (items[i].Group == group) { Kill(i); break; }
        }

        static void Kill(int i)
        {
            var it = items[i];
            counts[it.Group]--;
            items.RemoveAt(i);
            if (it.Go != null) Object.Destroy(it.Go);
        }

        public static void Tick()
        {
            float now = Time.time;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                if (it.Go == null) { counts[it.Group]--; items.RemoveAt(i); continue; }
                float left = it.Expire - now;
                if (left < 1f)
                {
                    it.Go.transform.localScale *= 0.92f;
                    if (left <= 0f) Kill(i);
                }
            }
        }

        public static void ClearAll()
        {
            for (int i = items.Count - 1; i >= 0; i--) Kill(i);
        }
    }
}
