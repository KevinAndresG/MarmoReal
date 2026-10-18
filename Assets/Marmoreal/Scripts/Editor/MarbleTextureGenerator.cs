using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Marmoreal.EditorTools
{
    /// <summary>
    /// Genera texturas de mármol procedurales y TILEABLES (albedo, máscara HDRP y normal).
    /// Vetas = seno deformado por ruido fractal periódico (domain warping).
    /// </summary>
    public static class MarbleTextureGenerator
    {
        public struct Spec
        {
            public string name;
            public Color baseA, baseB, vein, vein2;
            public int veinU, veinV, fineU, fineV;
            public float warp, fineWarp, sharp, veinStrength, fineStrength, speckle, normalStrength;
            public int seed;
        }

        // ------------------------------------------------------------------ Ruido periódico
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

        static float VNoise(float x, float y, int p, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            int xa = Mod(x0, p), xb = Mod(x0 + 1, p), ya = Mod(y0, p), yb = Mod(y0 + 1, p);
            float u = fx * fx * fx * (fx * (fx * 6 - 15) + 10);
            float v = fy * fy * fy * (fy * (fy * 6 - 15) + 10);
            float a = Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), u);
            float b = Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), u);
            return Mathf.Lerp(a, b, v);
        }

        /// <summary>fBm periódico en [0,1)² → [0,1].</summary>
        static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            int p = basePeriod;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * VNoise(u * p, v * p, p, seed + i * 101);
                norm += amp; amp *= 0.5f; p *= 2;
            }
            return sum / norm;
        }

        // ------------------------------------------------------------------ Mármol
        public static void Marble(Spec s, int size, float[] height, Color[] albedo, float[] smooth, int ox = 0, int oy = 0, int stride = -1, int region = -1)
        {
            if (stride < 0) stride = size;
            if (region < 0) region = size;
            Parallel.For(0, region, j =>
            {
                for (int i = 0; i < region; i++)
                {
                    float u = i / (float)region, v = j / (float)region;
                    float w1 = Fbm(u, v, 3, 6, s.seed + 7) - 0.5f;
                    float w2 = Fbm(u, v, 4, 5, s.seed + 19) - 0.5f;
                    float phase = 2 * Mathf.PI * (s.veinU * u + s.veinV * v) + s.warp * w1 * 2 * Mathf.PI + s.warp * 0.5f * w2 * 2 * Mathf.PI;
                    float vein = Mathf.Pow(1f - Mathf.Abs(Mathf.Sin(phase)), s.sharp);
                    float fw = Fbm(u, v, 5, 5, s.seed + 31) - 0.5f;
                    float phase2 = 2 * Mathf.PI * (s.fineU * u + s.fineV * v) + s.fineWarp * fw * 2 * Mathf.PI;
                    float fine = Mathf.Pow(1f - Mathf.Abs(Mathf.Sin(phase2)), s.sharp * 2.6f) * s.fineStrength;
                    float cloud = Fbm(u, v, 4, 6, s.seed + 53);
                    float speck = (Hash(i + ox, j + oy, s.seed + 77) - 0.5f) * s.speckle;

                    Color c = Color.Lerp(s.baseA, s.baseB, Mathf.SmoothStep(0.2f, 0.8f, cloud));
                    c = Color.Lerp(c, s.vein, Mathf.Clamp01(vein * s.veinStrength));
                    c = Color.Lerp(c, s.vein2, Mathf.Clamp01(fine));
                    c.r += speck; c.g += speck; c.b += speck;

                    int idx = (j + oy) * stride + (i + ox);
                    albedo[idx] = c;
                    height[idx] = cloud * 0.35f - vein * s.veinStrength * 0.5f - fine * 0.3f;
                    smooth[idx] = 1f - vein * 0.22f - fine * 0.12f;
                }
            });
        }

        // ------------------------------------------------------------------ Guardado
        public static void Save(string folder, string name, int size, Color[] albedo, float[] height, float[] smooth, float normalStrength,
            out Texture2D albedoTex, out Texture2D maskTex, out Texture2D normalTex)
        {
            Directory.CreateDirectory(folder);
            var a = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            a.SetPixels(albedo); a.Apply();
            var m = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var n = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var mp = new Color[size * size];
            var np = new Color[size * size];
            Parallel.For(0, size, j =>
            {
                for (int i = 0; i < size; i++)
                {
                    int idx = j * size + i;
                    float hl = height[j * size + (i - 1 + size) % size], hr = height[j * size + (i + 1) % size];
                    float hd = height[((j - 1 + size) % size) * size + i], hu = height[((j + 1) % size) * size + i];
                    Vector3 nn = new Vector3((hl - hr) * normalStrength, (hd - hu) * normalStrength, 1f).normalized;
                    np[idx] = new Color(nn.x * 0.5f + 0.5f, nn.y * 0.5f + 0.5f, nn.z * 0.5f + 0.5f, 1f);
                    // HDRP Mask: R metálico, G oclusión, B máscara de detalle, A suavidad
                    mp[idx] = new Color(0f, Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(height[idx] + 0.6f)), 0f, Mathf.Clamp01(smooth[idx]));
                }
            });
            m.SetPixels(mp); m.Apply();
            n.SetPixels(np); n.Apply();

            string pa = $"{folder}/T_{name}_Albedo.png", pm = $"{folder}/T_{name}_Mask.png", pn = $"{folder}/T_{name}_Normal.png";
            File.WriteAllBytes(pa, a.EncodeToPNG());
            File.WriteAllBytes(pm, m.EncodeToPNG());
            File.WriteAllBytes(pn, n.EncodeToPNG());
            Object.DestroyImmediate(a); Object.DestroyImmediate(m); Object.DestroyImmediate(n);

            AssetDatabase.ImportAsset(pa); AssetDatabase.ImportAsset(pm); AssetDatabase.ImportAsset(pn);
            Configure(pa, true, false);
            Configure(pm, false, false);
            Configure(pn, false, true);
            albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pa);
            maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pm);
            normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pn);
        }

        static void Configure(string path, bool srgb, bool normal)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = srgb;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 8;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }

        /// <summary>Textura de una sola variedad de mármol (tileable).</summary>
        public static void Generate(string folder, Spec s, int size, out Texture2D albedo, out Texture2D mask, out Texture2D normal)
        {
            var h = new float[size * size]; var c = new Color[size * size]; var sm = new float[size * size];
            Marble(s, size, h, c, sm);
            Save(folder, s.name, size, c, h, sm, s.normalStrength, out albedo, out mask, out normal);
        }

        /// <summary>Suelo de baldosas 2×2 alternando dos mármoles, con juntas de latón.</summary>
        public static void GenerateFloor(string folder, string name, Spec a, Spec b, int size, Color grout, out Texture2D albedo, out Texture2D mask, out Texture2D normal)
        {
            int half = size / 2;
            var h = new float[size * size]; var c = new Color[size * size]; var sm = new float[size * size];
            for (int ty = 0; ty < 2; ty++)
            for (int tx = 0; tx < 2; tx++)
            {
                var s = ((tx + ty) % 2 == 0) ? a : b;
                s.seed += tx * 13 + ty * 29;
                Marble(s, size, h, c, sm, tx * half, ty * half, size, half);
            }
            float gw = size / 256f * 2.2f;
            Parallel.For(0, size, j =>
            {
                for (int i = 0; i < size; i++)
                {
                    float dx = Mathf.Min(i % half, half - i % half);
                    float dy = Mathf.Min(j % half, half - j % half);
                    float d = Mathf.Min(dx, dy);
                    if (d < gw)
                    {
                        int idx = j * size + i;
                        float k = d / gw;
                        c[idx] = Color.Lerp(grout, c[idx], k * k);
                        h[idx] -= (1f - k) * 0.6f;
                        sm[idx] = Mathf.Lerp(0.55f, sm[idx], k);
                    }
                }
            });
            Save(folder, name, size, c, h, sm, a.normalStrength, out albedo, out mask, out normal);
        }
    }
}
