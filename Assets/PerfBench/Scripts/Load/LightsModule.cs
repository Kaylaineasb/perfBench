using System.Collections.Generic;
using UnityEngine;

namespace PerfBench
{
    /// <summary>
    /// Luzes pontuais em tempo real (sem sombra) e spots COM sombra, se movendo.
    /// Requer Forward+ no URP Renderer para muitas luzes por pixel.
    /// Atenção: no mobile o URP limita as luzes visíveis (tipicamente 32 por câmera).
    /// </summary>
    public class LightsModule : LoadModule
    {
        public override string Id => "lights";

        readonly List<Light> points = new List<Light>();
        readonly List<Light> spots = new List<Light>();
        int activePoints, activeSpots;

        public override int ActiveObjectCount => activePoints + activeSpots;

        public override void ApplyProfile(ScenarioProfile p)
        {
            activePoints = Ensure(points, p.pointLights, LightType.Point);
            activeSpots = Ensure(spots, p.shadowedSpotLights, LightType.Spot);
            bool shadows = !Off("shadows");
            foreach (var s in spots) s.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        int Ensure(List<Light> list, int count, LightType type)
        {
            count = Mathf.Max(0, count);
            while (list.Count < count)
            {
                var go = new GameObject(type + "Light");
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = type;
                if (type == LightType.Point) { l.range = 9f; l.intensity = 3f; l.shadows = LightShadows.None; }
                else { l.range = 30f; l.intensity = 8f; l.spotAngle = 50f; l.color = new Color(1f, 0.95f, 0.85f); }
                list.Add(l);
            }
            for (int i = 0; i < list.Count; i++) list[i].gameObject.SetActive(i < count);
            return count;
        }

        void Update()
        {
            if (!Ready) return;
            float t = (float)Ctx.runner.Elapsed;
            uint seed = (uint)Ctx.seed;
            for (int i = 0; i < activePoints; i++)
            {
                uint h = DeterministicRandom.Hash((uint)i * 2891336453u ^ seed);
                float r0 = DeterministicRandom.Hash01(h), r1 = DeterministicRandom.Hash01(h + 1u),
                      r2 = DeterministicRandom.Hash01(h + 2u), r3 = DeterministicRandom.Hash01(h + 3u),
                      r4 = DeterministicRandom.Hash01(h + 4u);
                float zc = 8f + r0 * 70f;
                points[i].transform.position = new Vector3(
                    Mathf.Sin(t * (0.7f + r2) + r3 * 6.2831853f) * 6f,
                    1.2f + r4 * 5f,
                    zc + Mathf.Sin(t * (0.5f + r1)) * 6f);
                points[i].color = Color.HSVToRGB(Mathf.Repeat(r0 + t * 0.05f, 1f), 0.9f, 1f);
            }
            for (int k = 0; k < activeSpots; k++)
            {
                var pos = new Vector3((k & 1) == 0 ? -5f : 5f, 9f, 10f + k * 12f);
                var target = new Vector3(Mathf.Sin(t + k) * 3f, 0f, pos.z + 6f);
                spots[k].transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos));
            }
        }
    }
}
