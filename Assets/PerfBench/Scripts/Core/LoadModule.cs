using UnityEngine;

namespace PerfBench
{
    /// <summary>Dados compartilhados entre módulos.</summary>
    public sealed class BenchmarkContext
    {
        public BenchmarkRunner runner;
        public RunOptions options;
        public int seed;
        public Camera camera;
        public Light mainLight;
        public Transform player;
        public Material stressLit;
        public Material overdraw;
        public Material particle;
        public WorldSimulation world;
    }

    /// <summary>
    /// Uma alavanca de carga isolada. Cada módulo pode ser desligado por deeplink
    /// (off=&lt;Id&gt;) para medir sua contribuição na calibração.
    /// </summary>
    public abstract class LoadModule : MonoBehaviour
    {
        public abstract string Id { get; }
        public virtual int ActiveObjectCount => 0;

        protected BenchmarkContext Ctx { get; private set; }
        protected bool Ready { get; private set; }

        public void Initialize(BenchmarkContext ctx)
        {
            Ctx = ctx;
            OnInitialize();
            Ready = true;
        }

        protected virtual void OnInitialize() { }
        public abstract void ApplyProfile(ScenarioProfile p);

        protected bool Off(string id) => Ctx != null && Ctx.options.IsDisabled(id);

        protected Material MakeMaterial(Color c)
        {
            var m = new Material(Ctx.stressLit);
            m.SetColor("_BaseColor", c);
            return m;
        }

        protected GameObject MakeRenderer(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go;
        }
    }
}
