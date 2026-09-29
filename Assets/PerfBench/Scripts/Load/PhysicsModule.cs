using System.Collections.Generic;
using UnityEngine;

namespace PerfBench
{
    /// <summary>
    /// Física (PhysX): N esferas num contêiner fechado ao lado da pista, recebendo
    /// impulsos determinísticos a cada 0,5 s. Sleep desligado para que o custo de
    /// contatos seja contínuo. A física roda em passo fixo (Time.fixedDeltaTime).
    /// </summary>
    public class PhysicsModule : LoadModule
    {
        public override string Id => "physics";

        [SerializeField] Vector3 containerCenter = new Vector3(-9.5f, 0f, 28f);
        [SerializeField] Vector3 containerSize = new Vector3(5f, 6f, 10f);
        [SerializeField] float bodyRadius = 0.22f;
        [SerializeField] int kickEveryFixedSteps = 25;

        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        Transform container;
        Material material;
        Mesh mesh;
        PhysicsMaterial physMat;
        int active;
        long fixedStep;

        public override int ActiveObjectCount => active;
        public long FixedSteps => fixedStep;

        protected override void OnInitialize()
        {
            container = new GameObject("PhysicsContainer").transform;
            container.SetParent(transform, false);
            container.position = containerCenter;
            material = MakeMaterial(new Color(1f, 0.45f, 0.1f));
            mesh = MeshFactory.Sphere(8);
            physMat = new PhysicsMaterial("PB_Bouncy")
            {
                bounciness = 0.6f,
                dynamicFriction = 0.3f,
                staticFriction = 0.3f,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };

            var s = containerSize;
            Wall(new Vector3(0, -0.25f, 0), new Vector3(s.x, 0.5f, s.z), true);          // chão (visível)
            Wall(new Vector3(0, s.y + 0.25f, 0), new Vector3(s.x, 0.5f, s.z), false);    // teto
            Wall(new Vector3(-s.x * 0.5f - 0.25f, s.y * 0.5f, 0), new Vector3(0.5f, s.y, s.z), false);
            Wall(new Vector3(s.x * 0.5f + 0.25f, s.y * 0.5f, 0), new Vector3(0.5f, s.y, s.z), false);
            Wall(new Vector3(0, s.y * 0.5f, -s.z * 0.5f - 0.25f), new Vector3(s.x, s.y, 0.5f), false);
            Wall(new Vector3(0, s.y * 0.5f, s.z * 0.5f + 0.25f), new Vector3(s.x, s.y, 0.5f), false);
        }

        void Wall(Vector3 localPos, Vector3 size, bool visible)
        {
            GameObject go;
            if (visible) go = MakeRenderer("Floor", MeshFactory.Cube(), material, container);
            else { go = new GameObject("Wall"); go.transform.SetParent(container, false); }
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.AddComponent<BoxCollider>().sharedMaterial = physMat;
        }

        public override void ApplyProfile(ScenarioProfile p)
        {
            int target = Mathf.Max(0, p.physicsBodies);
            while (bodies.Count < target)
            {
                var go = MakeRenderer("Body", mesh, material, container);
                go.transform.localScale = Vector3.one * bodyRadius * 2f;
                go.AddComponent<SphereCollider>().sharedMaterial = physMat;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 1f;
                rb.sleepThreshold = 0f;
                go.SetActive(false);
                bodies.Add(rb);
            }

            // Posição inicial determinística em grade
            float step = bodyRadius * 2.4f;
            int nx = Mathf.Max(1, Mathf.FloorToInt((containerSize.x - step) / step));
            int nz = Mathf.Max(1, Mathf.FloorToInt((containerSize.z - step) / step));
            for (int i = 0; i < bodies.Count; i++)
            {
                bool on = i < target;
                var rb = bodies[i];
                if (on && !rb.gameObject.activeSelf)
                {
                    int ix = i % nx, iz = (i / nx) % nz, iy = i / (nx * nz);
                    rb.transform.localPosition = new Vector3(
                        -containerSize.x * 0.5f + step * (ix + 1),
                        step * (iy + 1),
                        -containerSize.z * 0.5f + step * (iz + 1));
                    rb.gameObject.SetActive(true);
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                else if (!on && rb.gameObject.activeSelf) rb.gameObject.SetActive(false);
            }
            active = target;
        }

        void FixedUpdate()
        {
            if (!Ready || active == 0) return;
            fixedStep++;
            if (fixedStep % kickEveryFixedSteps != 0) return;
            uint stepSeed = (uint)(fixedStep / kickEveryFixedSteps) * 2654435761u ^ (uint)Ctx.seed;
            for (int i = 0; i < active; i++)
            {
                uint h = DeterministicRandom.Hash((uint)i ^ stepSeed);
                var kick = new Vector3((DeterministicRandom.Hash01(h) - 0.5f) * 4f,
                                       6f + DeterministicRandom.Hash01(h + 1u) * 4f,
                                       (DeterministicRandom.Hash01(h + 2u) - 0.5f) * 4f);
                bodies[i].AddForce(kick, ForceMode.VelocityChange);
            }
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (mesh != null) Destroy(mesh);
            if (physMat != null) Destroy(physMat);
        }
    }
}
