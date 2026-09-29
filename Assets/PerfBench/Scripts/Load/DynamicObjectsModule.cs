using System.Collections.Generic;
using UnityEngine;

namespace PerfBench
{
    /// <summary>
    /// Objetos individuais SEM batching: cada um usa MaterialPropertyBlock (cor piscando),
    /// o que o tira do SRP Batcher e gera 1 draw call + atualização de estado por objeto.
    /// Também voam/orbitam (escrita de Transform na thread principal).
    /// </summary>
    public class DynamicObjectsModule : LoadModule
    {
        public override string Id => "objects";

        readonly List<Transform> objs = new List<Transform>();
        readonly List<MeshRenderer> rends = new List<MeshRenderer>();
        MaterialPropertyBlock mpb;
        Material material;
        Mesh sphere;
        int active;
        bool blink;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public override int ActiveObjectCount => active;

        protected override void OnInitialize()
        {
            mpb = new MaterialPropertyBlock();
            material = MakeMaterial(Color.white);
            sphere = MeshFactory.Sphere(12);
        }

        public override void ApplyProfile(ScenarioProfile p)
        {
            int target = Mathf.Max(0, p.dynamicObjectCount);
            var cube = MeshFactory.Cube();
            while (objs.Count < target)
            {
                int i = objs.Count;
                var go = MakeRenderer("DynObj", (i & 1) == 0 ? cube : sphere, material, transform);
                objs.Add(go.transform);
                rends.Add(go.GetComponent<MeshRenderer>());
            }
            for (int i = 0; i < objs.Count; i++)
                objs[i].gameObject.SetActive(i < target);
            active = target;
            blink = p.dynamicObjectsBlink;
        }

        void Update()
        {
            if (!Ready || active == 0) return;
            float t = (float)Ctx.runner.Elapsed;
            uint seed = (uint)Ctx.seed;
            for (int i = 0; i < active; i++)
            {
                uint h = DeterministicRandom.Hash((uint)i * 747796405u ^ seed);
                float r0 = DeterministicRandom.Hash01(h), r1 = DeterministicRandom.Hash01(h + 1u),
                      r2 = DeterministicRandom.Hash01(h + 2u), r3 = DeterministicRandom.Hash01(h + 3u),
                      r4 = DeterministicRandom.Hash01(h + 4u);

                float angle = t * (0.4f + r0 * 1.2f) + r1 * 6.2831853f;
                float radius = Mathf.Lerp(6f, 20f, r2);
                float zc = Mathf.Lerp(15f, 120f, r4);
                var pos = new Vector3(Mathf.Cos(angle) * radius,
                                      3f + r3 * 12f + Mathf.Sin(t * (1f + r4) + i) * 2f,
                                      zc + Mathf.Sin(angle) * radius * 0.6f);
                objs[i].SetPositionAndRotation(pos, Quaternion.Euler(t * 90f * (r0 + 0.2f), t * 60f * (r1 + 0.2f), 0f));
                objs[i].localScale = Vector3.one * Mathf.Lerp(0.5f, 1.4f, r2);

                float v = blink && Mathf.Repeat(t * (2f + r2 * 6f) + r3, 1f) < 0.5f ? 1f : 0.3f;
                mpb.SetColor(BaseColorId, Color.HSVToRGB(Mathf.Repeat(r0 + t * 0.25f, 1f), 0.8f, v));
                rends[i].SetPropertyBlock(mpb);
            }
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (sphere != null) Destroy(sphere);
        }
    }
}
