using System.Collections.Generic;
using UnityEngine;

namespace PerfBench
{
    /// <summary>
    /// Fillrate: N quads transparentes cobrindo a tela inteira (overdraw), cada um com
    /// um loop de ruído no fragment shader. Custo ∝ pixels × camadas × iterações.
    /// Ficam na layer TransparentFX (a câmera virtual não os vê).
    /// </summary>
    public class OverdrawModule : LoadModule
    {
        public override string Id => "overdraw";

        const int TransparentFxLayer = 1;
        readonly List<Transform> quads = new List<Transform>();
        int layers;
        static readonly int OverdrawAluId = Shader.PropertyToID("_PB_OverdrawAlu");

        public override int ActiveObjectCount => layers;

        public override void ApplyProfile(ScenarioProfile p)
        {
            layers = Mathf.Max(0, p.overdrawLayers);
            Shader.SetGlobalFloat(OverdrawAluId, p.overdrawAluIterations);
            if (Ctx.overdraw == null || Ctx.camera == null) { layers = 0; return; }

            while (quads.Count < layers)
            {
                var go = MakeRenderer("OverdrawLayer", MeshFactory.Quad(), Ctx.overdraw, Ctx.camera.transform);
                go.layer = TransparentFxLayer;
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                quads.Add(go.transform);
            }
            for (int i = 0; i < quads.Count; i++) quads[i].gameObject.SetActive(i < layers);
        }

        void LateUpdate()
        {
            if (!Ready || layers == 0) return;
            var cam = Ctx.camera;
            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            for (int i = 0; i < layers; i++)
            {
                float dist = cam.nearClipPlane + 0.05f + i * 0.02f;
                float h = 2f * dist * tanHalf * 1.05f;
                quads[i].localPosition = new Vector3(0f, 0f, dist);
                quads[i].localRotation = Quaternion.identity;
                quads[i].localScale = new Vector3(h * cam.aspect, h, 1f);
            }
        }
    }
}
