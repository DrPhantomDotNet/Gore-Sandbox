using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Bullseye target on a hinge: scores the hit ring, flips down, resets.</summary>
    public sealed class ShootingTarget : MonoBehaviour, IDamageable
    {
        static int totalScore;
        Transform board;
        float downUntil;
        public bool IsAlive => true;

        public static void Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Shooting Target");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var stand = new MeshBuilder { Color = new Color(0.3f, 0.3f, 0.3f), Gloss = 0.4f };
            stand.Box(new Vector3(0, 0.6f, 0), new Vector3(0.08f, 1.2f, 0.08f));
            stand.Box(new Vector3(0, 0.03f, 0), new Vector3(0.6f, 0.06f, 0.6f));
            go.AddComponent<MeshFilter>().sharedMesh = stand.ToMesh("TargetStand");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var boardGo = new GameObject("Board");
            boardGo.transform.SetParent(go.transform, false);
            boardGo.transform.localPosition = new Vector3(0, 1.2f, 0);
            var b = new MeshBuilder();
            Color[] rings = { Color.white, new Color(0.1f, 0.1f, 0.1f), new Color(0.15f, 0.35f, 0.9f), new Color(0.9f, 0.15f, 0.12f), new Color(1f, 0.85f, 0.15f) };
            for (int i = 0; i < rings.Length; i++)
            {
                b.SetColor(rings[i], 0.2f);
                float r = 0.42f - i * 0.085f;
                b.Cylinder(new Vector3(0, 0.45f, -0.02f - i * 0.004f), new Vector3(0, 0.45f, 0.02f + i * 0.004f), r, r, 24);
            }
            boardGo.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("TargetBoard");
            boardGo.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            var bc = boardGo.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.45f, 0); bc.size = new Vector3(0.85f, 0.85f, 0.06f);
            boardGo.AddComponent<Surface>().Type = SurfaceType.Wood;
            var t = boardGo.AddComponent<ShootingTarget>();
            t.board = boardGo.transform;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (NetGuard.Intercept(this, info)) return;
            if (Time.time < downUntil) return;
            var local = transform.InverseTransformPoint(info.Point) - new Vector3(0, 0.45f, 0);
            float d = new Vector2(local.x, local.y).magnitude;
            int score = d < 0.08f ? 10 : d < 0.165f ? 8 : d < 0.25f ? 6 : d < 0.335f ? 4 : 2;
            totalScore += score;
            downUntil = Time.time + 2.5f;
            AudioManager.Play(score == 10 ? "ding" : "wood_impact", info.Point, 1f);
            GameEvents.ShowToast((score == 10 ? "BULLSEYE! " : "Target hit: ") + score + " pts  (total " + totalScore + ")");
        }

        void Update()
        {
            if (board == null) return;
            float target = Time.time < downUntil ? -85f : 0f;
            board.localRotation = Quaternion.Slerp(board.localRotation, Quaternion.Euler(target, 0, 0), Time.deltaTime * (target < 0 ? 18f : 4f));
        }
    }
}
