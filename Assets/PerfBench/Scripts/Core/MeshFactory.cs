using System.Collections.Generic;
using UnityEngine;

namespace PerfBench
{
    /// <summary>Malhas geradas em código (sem depender de assets embutidos).</summary>
    public static class MeshFactory
    {
        static Mesh cube, quad;

        /// <summary>Esfera UV de diâmetro 1. Triângulos ≈ 2 × segments².</summary>
        public static Mesh Sphere(int segments)
        {
            int lon = Mathf.Clamp(segments, 3, 128);
            int lat = Mathf.Clamp(segments, 2, 128);
            var verts = new List<Vector3>((lat + 1) * (lon + 1));
            var normals = new List<Vector3>(verts.Capacity);
            var tris = new List<int>(lat * lon * 6);

            for (int y = 0; y <= lat; y++)
            {
                float theta = (float)y / lat * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float phi = (float)x / lon * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    verts.Add(n * 0.5f);
                    normals.Add(n);
                }
            }
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int i0 = y * (lon + 1) + x, i1 = i0 + 1, i2 = i0 + lon + 1, i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i1); tris.Add(i3);
                    tris.Add(i0); tris.Add(i3); tris.Add(i2);
                }

            var m = new Mesh { name = "PB_Sphere_" + segments };
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Cubo unitário centrado na origem (24 vértices, normais por face).</summary>
        public static Mesh Cube()
        {
            if (cube != null) return cube;
            var v = new List<Vector3>(24);
            var n = new List<Vector3>(24);
            var t = new List<int>(36);
            void Face(Vector3 normal, Vector3 up)
            {
                Vector3 right = Vector3.Cross(up, -normal) * 0.5f;
                Vector3 u = up * 0.5f, c = normal * 0.5f;
                int b = v.Count;
                v.Add(c - right - u); v.Add(c - right + u); v.Add(c + right + u); v.Add(c + right - u);
                for (int i = 0; i < 4; i++) n.Add(normal);
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
                t.Add(b); t.Add(b + 2); t.Add(b + 3);
            }
            Face(Vector3.right, Vector3.up);
            Face(Vector3.left, Vector3.up);
            Face(Vector3.forward, Vector3.up);
            Face(Vector3.back, Vector3.up);
            Face(Vector3.up, Vector3.forward);
            Face(Vector3.down, Vector3.forward);

            cube = new Mesh { name = "PB_Cube" };
            cube.SetVertices(v);
            cube.SetNormals(n);
            cube.SetTriangles(t, 0);
            cube.RecalculateBounds();
            return cube;
        }

        /// <summary>Quad 1×1 no plano XY (voltado para -Z).</summary>
        public static Mesh Quad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "PB_Quad" };
            quad.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f) });
            quad.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            quad.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            quad.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            quad.RecalculateBounds();
            return quad;
        }
    }
}
