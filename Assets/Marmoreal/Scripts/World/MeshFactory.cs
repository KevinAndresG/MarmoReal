using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Marmoreal
{
    /// <summary>
    /// Generadores de meshes procedurales. Se usan desde el builder del editor UNA vez y el
    /// resultado se guarda como .asset (Assets/Marmoreal/Meshes) para poder editarlo/reemplazarlo.
    /// </summary>
    public static class MeshFactory
    {
        // ------------------------------------------------------------------ Caja redondeada
        public static Mesh RoundedBox(Vector3 size, float radius, string name = "RoundedBox")
        {
            Vector3 h = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f);
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();

            void Face(Vector3 n, Vector3 u, Vector3 v)
            {
                float hn = Vector3.Dot(h, Abs(n)), hu = Vector3.Dot(h, Abs(u)), hv = Vector3.Dot(h, Abs(v));
                float[] cu = AxisSamples(hu, radius), cv = AxisSamples(hv, radius);
                int start = verts.Count;
                Vector3 inner = h - Vector3.one * radius;
                for (int j = 0; j < cv.Length; j++)
                for (int i = 0; i < cu.Length; i++)
                {
                    Vector3 p = n * hn + u * cu[i] + v * cv[j];
                    Vector3 c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 d = p - c;
                    Vector3 nn = d.sqrMagnitude < 1e-8f ? n : d.normalized;
                    verts.Add(c + nn * radius);
                    norms.Add(nn);
                    uvs.Add(new Vector2((cu[i] + hu) * 0.5f, (cv[j] + hv) * 0.5f));
                }
                int w = cu.Length;
                for (int j = 0; j < cv.Length - 1; j++)
                for (int i = 0; i < w - 1; i++)
                {
                    int a = start + j * w + i, b = a + 1, c2 = a + w, d2 = c2 + 1;
                    tris.Add(a); tris.Add(c2); tris.Add(b);
                    tris.Add(b); tris.Add(c2); tris.Add(d2);
                }
            }

            Face(Vector3.up, Vector3.right, Vector3.forward);
            Face(Vector3.down, Vector3.forward, Vector3.right);
            Face(Vector3.right, Vector3.forward, Vector3.up);
            Face(Vector3.left, Vector3.up, Vector3.forward);
            Face(Vector3.forward, Vector3.up, Vector3.right);
            Face(Vector3.back, Vector3.right, Vector3.up);

            return Build(name, verts, norms, uvs, tris);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static float[] AxisSamples(float h, float r)
        {
            float c = h - r;
            var l = new List<float> { -h, -c - r * 0.62f, -c - r * 0.25f, -c };
            int mid = Mathf.Clamp(Mathf.RoundToInt(c * 2f / 0.5f), 1, 6);
            for (int k = 1; k < mid; k++) l.Add(-c + 2f * c * k / mid);
            l.Add(c); l.Add(c + r * 0.25f); l.Add(c + r * 0.62f); l.Add(h);
            return l.ToArray();
        }

        // ------------------------------------------------------------------ Torno (lathe)
        /// <param name="profile">(radio, altura) de abajo a arriba. Empezar/terminar en radio 0 para cerrar.</param>
        /// <param name="radiusMod">f(altura, ángulo) → multiplicador de radio (estrías, torsión). Opcional.</param>
        public static Mesh Lathe(Vector2[] profile, int segments, string name = "Lathe", Func<float, float, float> radiusMod = null, float angleOffset = 0f, bool flat = false, float uvScale = 1f)
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            float total = 0; var acc = new float[profile.Length];
            for (int i = 1; i < profile.Length; i++) { total += Vector2.Distance(profile[i], profile[i - 1]); acc[i] = total; }
            int cols = segments + 1;
            for (int i = 0; i < profile.Length; i++)
            {
                for (int s = 0; s <= segments; s++)
                {
                    float a = angleOffset + s / (float)segments * Mathf.PI * 2f;
                    float r = profile[i].x * (radiusMod?.Invoke(profile[i].y, a) ?? 1f);
                    verts.Add(new Vector3(Mathf.Cos(a) * r, profile[i].y, Mathf.Sin(a) * r));
                    uvs.Add(new Vector2(s / (float)segments * uvScale * Mathf.Max(1f, profile[i].x * 2f), acc[i] * uvScale * 0.5f));
                }
            }
            for (int i = 0; i < profile.Length - 1; i++)
            for (int s = 0; s < segments; s++)
            {
                int a = i * cols + s, b = a + 1, c = a + cols, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
            var m = Build(name, verts, null, uvs, tris);
            if (flat) return Flatten(m);
            FixSeam(m, cols, profile.Length);
            return m;
        }

        static void FixSeam(Mesh m, int cols, int rows)
        {
            var n = m.normals;
            var v = m.vertices;
            for (int i = 0; i < rows; i++)
            {
                int a = i * cols, b = a + cols - 1;
                Vector3 avg = (n[a] + n[b]).normalized;
                n[a] = avg; n[b] = avg;
            }
            // Polos: normales verticales donde el radio es 0
            for (int i = 0; i < rows; i++)
            {
                int a = i * cols;
                if (new Vector2(v[a].x, v[a].z).sqrMagnitude < 1e-8f)
                {
                    float dir = i == 0 ? -1f : 1f;
                    for (int s = 0; s < cols; s++) n[a + s] = new Vector3(0, dir, 0);
                }
            }
            m.normals = n;
            m.RecalculateTangents();
        }

        public static Mesh Sphere(float radius, int segments = 48, string name = "Sphere")
        {
            int rings = segments / 2;
            var prof = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float t = -Mathf.PI / 2 + Mathf.PI * i / rings;
                prof[i] = new Vector2(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius);
            }
            prof[0].x = 0; prof[rings].x = 0;
            return Lathe(prof, segments, name);
        }

        public static Mesh Torus(float R, float r, int seg = 64, int sides = 16, string name = "Torus")
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                Vector3 c = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int j = 0; j <= sides; j++)
                {
                    float b = j / (float)sides * Mathf.PI * 2;
                    Vector3 n = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    verts.Add(c * R + n * r); norms.Add(n);
                    uvs.Add(new Vector2(i / (float)seg * 8f, j / (float)sides));
                }
            }
            int w = sides + 1;
            for (int i = 0; i < seg; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * w + j, b = a + 1, c2 = a + w, d = c2 + 1;
                tris.Add(a); tris.Add(b); tris.Add(c2);
                tris.Add(b); tris.Add(d); tris.Add(c2);
            }
            return Build(name, verts, norms, uvs, tris);
        }

        /// <summary>Fragmento facetado (octaedro alargado) para el polvo de mármol.</summary>
        public static Mesh Shard(string name = "Shard")
        {
            Vector3[] p = { new(0, 1.6f, 0), new(1, 0, 0), new(0, 0, 1), new(-1, 0, 0), new(0, 0, -1), new(0, -1.2f, 0) };
            int[] t = { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4, 5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1 };
            var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
            for (int i = 0; i < t.Length; i++) { verts.Add(p[t[i]] * 0.5f); tris.Add(i); uvs.Add(new Vector2(p[t[i]].x, p[t[i]].z) * 0.5f + Vector2.one * 0.5f); }
            var m = Build(name, verts, null, uvs, tris);
            return m;
        }

        // ------------------------------------------------------------------ Suelo-cuenco
        public static Mesh ArenaFloor(int segments = 160, float tile = 4f, string name = "ArenaFloor")
        {
            var radii = new List<float>();
            for (float r = 0; r < ArenaShape.FlatRadius; r += 1.25f) radii.Add(r);
            for (float r = ArenaShape.FlatRadius; r < ArenaShape.BowlRadius; r += 0.5f) radii.Add(r);
            for (float r = ArenaShape.BowlRadius; r <= ArenaShape.TerraceRadius + 0.01f; r += 1f) radii.Add(r);

            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            int cols = segments + 1;
            foreach (float r in radii)
            {
                float h = ArenaShape.Height(r);
                float dh = (ArenaShape.Height(r + 0.01f) - ArenaShape.Height(Mathf.Max(0, r - 0.01f))) / 0.02f;
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    Vector3 p = dir * r + Vector3.up * h;
                    verts.Add(p);
                    norms.Add((Vector3.up - dir * dh).normalized);
                    uvs.Add(new Vector2(p.x, p.z) / tile);
                }
            }
            // Zócalo exterior (cae hacia abajo)
            float rr = ArenaShape.TerraceRadius;
            for (int k = 0; k < 2; k++)
            {
                float y = k == 0 ? ArenaShape.RimHeight : -6f;
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    verts.Add(dir * rr + Vector3.up * y);
                    norms.Add(dir);
                    uvs.Add(new Vector2(s / (float)segments * 60f, y / tile));
                }
            }
            int rows = radii.Count + 2;
            for (int i = 0; i < rows - 1; i++)
            {
                if (i == radii.Count - 1) continue; // salto entre terraza y zócalo (normales distintas)
                for (int s = 0; s < segments; s++)
                {
                    int a = i * cols + s, b = a + 1, c = a + cols, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }
            return Build(name, verts, norms, uvs, tris);
        }

        // ------------------------------------------------------------------ Utilidades
        public static Mesh Combine(string name, params (Mesh mesh, Matrix4x4 m)[] parts)
        {
            var ci = new CombineInstance[parts.Length];
            for (int i = 0; i < parts.Length; i++) ci[i] = new CombineInstance { mesh = parts[i].mesh, transform = parts[i].m };
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(ci, true, true);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        public static Matrix4x4 TRS(Vector3 p, Vector3 euler, Vector3 s) => Matrix4x4.TRS(p, Quaternion.Euler(euler), s);
        public static Matrix4x4 TRS(Vector3 p, Vector3 euler, float s = 1f) => Matrix4x4.TRS(p, Quaternion.Euler(euler), Vector3.one * s);

        public static Mesh Flatten(Mesh src)
        {
            var v = src.vertices; var uv = src.uv; var t = src.triangles;
            var nv = new Vector3[t.Length]; var nuv = new Vector2[t.Length]; var nt = new int[t.Length];
            for (int i = 0; i < t.Length; i++) { nv[i] = v[t[i]]; nuv[i] = uv.Length > 0 ? uv[t[i]] : Vector2.zero; nt[i] = i; }
            var m = new Mesh { name = src.name, indexFormat = t.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.vertices = nv; m.uv = nuv; m.triangles = nt;
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            if (n != null) m.SetNormals(n); else m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }
}
