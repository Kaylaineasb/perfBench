using System;
using System.Collections.Generic;
using UnityEngine;

namespace PerfBench
{
    /// <summary>
    /// O "jogo": pista de 3 faixas, obstáculos procedurais com seed e bot automático.
    ///
    /// Determinismo: nada aqui depende de Time.deltaTime.
    ///  - A distância percorrida vem da linha do tempo (função do tempo real).
    ///  - As fileiras de obstáculos são geradas em ordem, pelo RNG com seed, no domínio
    ///    da distância; o espaçamento de cada fileira vem do cenário que cobre aquela distância.
    ///  - A decisão do bot (trocar de faixa / pular) é calculada junto com a fileira,
    ///    e a pose do personagem é uma função da distância.
    /// Resultado: no instante t, o mundo é idêntico em qualquer aparelho, com qualquer FPS.
    /// O hash das fileiras geradas é gravado no summary.json para comprovar isso.
    /// </summary>
    public class WorldSimulation : LoadModule
    {
        public override string Id => "world";

        [Header("Pista")]
        [SerializeField] float laneWidth = 2f;
        [SerializeField] float viewAhead = 160f;
        [SerializeField] float viewBehind = 8f;
        [SerializeField] int groundTiles = 12;
        [SerializeField] float groundTileLength = 20f;
        [SerializeField] int obstaclePoolSize = 64;

        [Header("Personagem")]
        [SerializeField] float playerBaseY = 0.5f;
        [SerializeField] float jumpHeight = 1.8f;

        [Header("Câmera (retrato)")]
        [SerializeField] Vector3 cameraOffset = new Vector3(0f, 6f, -9f);
        [SerializeField] float cameraPitch = 20f;
        [SerializeField] float cameraFov = 70f;
        [SerializeField] float cameraFollowX = 0.35f;

        const float LowHeight = 0.8f, TallHeight = 2.6f, ObstacleWidth = 1.6f, ObstacleDepth = 1.2f;

        struct Row
        {
            public double s;                 // distância ao longo da pista
            public byte blocked, low;        // bits por faixa (0,1,2)
            public sbyte laneBefore, laneAfter;
            public bool jump;
            public double changeStart, changeEnd, jumpStart, jumpEnd;
        }

        readonly List<Row> rows = new List<Row>(4096);
        readonly List<uint> cumulativeHash = new List<uint>(4096);
        DeterministicRandom rng;
        uint hash = 2166136261u;
        int firstActiveRow;

        float[] lanes;
        Transform[] pool;
        MeshRenderer[] poolRenderers;
        bool[] poolActive;
        int usedLastFrame;
        Transform[] groundT, dashes;
        Material lowMat, tallMat, groundMat, dashMat, playerMat;

        public int RowsGenerated => rows.Count;
        public float PlayerX { get; private set; }
        public override int ActiveObjectCount =>
            usedLastFrame + (groundT?.Length ?? 0) + (dashes?.Length ?? 0) + 1;

        protected override void OnInitialize()
        {
            lanes = new[] { -laneWidth, 0f, laneWidth };
            rng = new DeterministicRandom(unchecked((ulong)(uint)Ctx.seed));

            lowMat = MakeMaterial(new Color(1f, 0.72f, 0.1f));
            tallMat = MakeMaterial(new Color(0.9f, 0.18f, 0.15f));
            groundMat = MakeMaterial(new Color(0.35f, 0.36f, 0.4f));
            dashMat = MakeMaterial(new Color(0.95f, 0.95f, 0.95f));
            playerMat = MakeMaterial(new Color(0.2f, 0.55f, 1f));

            var cube = MeshFactory.Cube();

            if (Ctx.player != null)
            {
                Ctx.player.localScale = Vector3.one;
                var r = Ctx.player.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = playerMat;
            }
            if (Ctx.camera != null)
            {
                Ctx.camera.fieldOfView = cameraFov;
                Ctx.camera.farClipPlane = 260f;
            }

            var root = new GameObject("World").transform;
            root.SetParent(transform, false);

            float trackWidth = laneWidth * 3f + 2f;
            groundT = new Transform[groundTiles];
            dashes = new Transform[groundTiles * 2];
            for (int i = 0; i < groundTiles; i++)
            {
                groundT[i] = MakeRenderer("Ground", cube, groundMat, root).transform;
                groundT[i].localScale = new Vector3(trackWidth, 0.2f, groundTileLength);
                for (int k = 0; k < 2; k++)
                {
                    var d = MakeRenderer("LaneDash", cube, dashMat, root).transform;
                    d.localScale = new Vector3(0.12f, 0.03f, groundTileLength * 0.45f);
                    dashes[i * 2 + k] = d;
                }
            }

            pool = new Transform[obstaclePoolSize];
            poolRenderers = new MeshRenderer[obstaclePoolSize];
            poolActive = new bool[obstaclePoolSize];
            for (int i = 0; i < obstaclePoolSize; i++)
            {
                var go = MakeRenderer("Obstacle", cube, lowMat, root);
                go.SetActive(false);
                pool[i] = go.transform;
                poolRenderers[i] = go.GetComponent<MeshRenderer>();
            }
        }

        public override void ApplyProfile(ScenarioProfile p) { /* velocidade e espaçamento vêm da linha do tempo */ }

        void Update()
        {
            if (!Ready) return;
            lanes[0] = -laneWidth;
            lanes[2] = laneWidth;
            double d = Ctx.runner.Distance;
            EnsureGenerated(d + viewAhead);
            while (firstActiveRow < rows.Count - 1 && rows[firstActiveRow].s < d - viewBehind) firstActiveRow++;

            // Personagem
            float x = PlayerXAt(d, out float lateral);
            float y = PlayerYAt(d, out float jumpPhase);
            PlayerX = x;
            if (Ctx.player != null)
                Ctx.player.SetPositionAndRotation(new Vector3(x, y, 0f),
                    Quaternion.Euler(jumpPhase * 360f, 0f, -lateral * 25f));

            if (Ctx.camera != null)
                Ctx.camera.transform.SetPositionAndRotation(
                    new Vector3(x * cameraFollowX, 0f, 0f) + cameraOffset,
                    Quaternion.Euler(cameraPitch, 0f, 0f));

            // Obstáculos visíveis
            int used = 0;
            for (int r = firstActiveRow; r < rows.Count && used < pool.Length; r++)
            {
                var row = rows[r];
                double z = row.s - d;
                if (z > viewAhead) break;
                if (z < -viewBehind) continue;
                for (int lane = 0; lane < 3 && used < pool.Length; lane++)
                {
                    if ((row.blocked & (1 << lane)) == 0) continue;
                    bool low = (row.low & (1 << lane)) != 0;
                    float h = low ? LowHeight : TallHeight;
                    var t = pool[used];
                    t.position = new Vector3(lanes[lane], h * 0.5f, (float)z);
                    t.localScale = new Vector3(ObstacleWidth, h, ObstacleDepth);
                    var mat = low ? lowMat : tallMat;
                    if (poolRenderers[used].sharedMaterial != mat) poolRenderers[used].sharedMaterial = mat;
                    if (!poolActive[used]) { t.gameObject.SetActive(true); poolActive[used] = true; }
                    used++;
                }
            }
            for (int i = used; i < pool.Length; i++)
                if (poolActive[i]) { pool[i].gameObject.SetActive(false); poolActive[i] = false; }
            usedLastFrame = used;

            // Chão rolando em direção à câmera
            double span = groundTiles * groundTileLength;
            for (int i = 0; i < groundTiles; i++)
            {
                double z = (i * groundTileLength - d) % span;
                if (z < 0) z += span;
                float zc = (float)(z - groundTileLength * 1.5);
                groundT[i].position = new Vector3(0f, -0.1f, zc);
                dashes[i * 2].position = new Vector3(-laneWidth * 0.5f, 0.02f, zc);
                dashes[i * 2 + 1].position = new Vector3(laneWidth * 0.5f, 0.02f, zc);
            }
        }

        // ---------- Geração determinística ----------

        void EnsureGenerated(double upTo)
        {
            while (rows.Count == 0 || rows[rows.Count - 1].s < upTo) GenerateRow();
        }

        void GenerateRow()
        {
            var tl = Ctx.runner.Timeline;
            bool first = rows.Count == 0;
            double prevS = first ? 0 : rows[rows.Count - 1].s;
            int prevLane = first ? 1 : rows[rows.Count - 1].laneAfter;
            double gap = first
                ? Ctx.runner.Config.firstRowDistance
                : tl.stages[tl.StageIndexAtDistance(prevS)].profile.obstacleSpacing;
            if (gap < 4) gap = 4;

            var row = new Row { s = prevS + gap, laneBefore = (sbyte)prevLane };

            if (rng.NextFloat() < 0.55f) row.blocked = (byte)(1 << rng.Range(0, 3));
            else row.blocked = (byte)(7 & ~(1 << rng.Range(0, 3)));
            for (int lane = 0; lane < 3; lane++)
                if ((row.blocked & (1 << lane)) != 0 && rng.NextFloat() < 0.35f)
                    row.low |= (byte)(1 << lane);

            // Decisão do bot
            int laneAfter = prevLane;
            bool jump = false;
            if ((row.blocked & (1 << prevLane)) != 0)
            {
                bool lowHere = (row.low & (1 << prevLane)) != 0;
                int c0 = -1, c1 = -1;
                for (int dl = -1; dl <= 1; dl += 2)
                {
                    int l = prevLane + dl;
                    if (l < 0 || l > 2 || (row.blocked & (1 << l)) != 0) continue;
                    if (c0 < 0) c0 = l; else c1 = l;
                }
                float coin = rng.NextFloat();
                if (lowHere && (c0 < 0 || coin < 0.5f)) jump = true;
                else if (c0 >= 0) laneAfter = (c1 >= 0 && rng.NextFloat() < 0.5f) ? c1 : c0;
                else { row.low |= (byte)(1 << prevLane); jump = true; } // única saída a 2 faixas: vira pulo
            }
            row.laneAfter = (sbyte)laneAfter;
            row.jump = jump;

            double window = Math.Min(gap * 0.35, 5.0);
            row.changeStart = row.s - window;
            row.changeEnd = row.s - Math.Min(1.5, window * 0.3);
            double j = Math.Clamp(gap * 0.45, 1.5, 4.0);
            row.jumpStart = row.s - j;
            row.jumpEnd = row.s + j;

            hash = Fnv(hash, row.blocked);
            hash = Fnv(hash, row.low);
            hash = Fnv(hash, (byte)laneAfter);
            hash = Fnv(hash, (byte)(jump ? 1 : 0));
            rows.Add(row);
            cumulativeHash.Add(hash);
        }

        static uint Fnv(uint h, byte b) { unchecked { return (h ^ b) * 16777619u; } }

        /// <summary>Hash de todas as fileiras antes da distância d (igual em todos os aparelhos).</summary>
        public uint WorldHashUpTo(double d, out int rowCount)
        {
            if (!Ready) { rowCount = 0; return 0; }
            EnsureGenerated(d);
            int lo = 0, hi = rows.Count - 1, found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (rows[mid].s < d) { found = mid; lo = mid + 1; } else hi = mid - 1;
            }
            rowCount = found + 1;
            return found >= 0 ? cumulativeHash[found] : 2166136261u;
        }

        // ---------- Pose do personagem ----------

        float PlayerXAt(double d, out float lateral)
        {
            lateral = 0f;
            for (int r = firstActiveRow; r < rows.Count; r++)
            {
                var row = rows[r];
                if (d < row.changeStart) return lanes[row.laneBefore];
                if (d <= row.changeEnd)
                {
                    float u = (float)((d - row.changeStart) / (row.changeEnd - row.changeStart));
                    lateral = (row.laneAfter - row.laneBefore) * 4f * u * (1f - u);
                    return Mathf.Lerp(lanes[row.laneBefore], lanes[row.laneAfter], u * u * (3f - 2f * u));
                }
            }
            return rows.Count > 0 ? lanes[rows[rows.Count - 1].laneAfter] : 0f;
        }

        float PlayerYAt(double d, out float phase)
        {
            phase = 0f;
            for (int r = firstActiveRow; r < rows.Count; r++)
            {
                var row = rows[r];
                if (d < row.jumpStart) break;
                if (row.jump && d <= row.jumpEnd)
                {
                    float u = (float)((d - row.jumpStart) / (row.jumpEnd - row.jumpStart));
                    phase = u;
                    return playerBaseY + jumpHeight * 4f * u * (1f - u);
                }
            }
            return playerBaseY;
        }
    }
}
