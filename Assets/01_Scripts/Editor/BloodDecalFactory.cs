using System.IO;
using UnityEditor;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Textura de las manchas de sangre del suelo: un atlas 2x2 con cuatro salpicaduras distintas
    /// (charco central irregular, gotas alrededor y salpicones alargados), generado por código.
    /// M_Blood la usa con recorte por alfa, así la mancha tiene forma de sangre y no de cuadrado.
    /// El BloodSpawner elige una de las cuatro al azar.
    /// </summary>
    internal static class BloodDecalFactory
    {
        public const int Cells = 2;
        private const int CellSize = 256;

        public static string TexturePath => AssetFactory.NumberedFolder("Textures", "05_Textures") + "/T_BloodSplats.png";

        public static void Ensure(Material blood)
        {
            if (blood == null) return;
            Texture2D atlas = EnsureTexture();
            blood.SetTexture("_BaseMap", atlas);
            blood.SetTexture("_MainTex", atlas);
            // Recorte por alfa: bordes nítidos, sin problemas de orden de transparencias.
            blood.SetFloat("_AlphaClip", 1f);
            blood.SetFloat("_Cutoff", 0.45f);
            blood.EnableKeyword("_ALPHATEST_ON");
            blood.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            blood.SetFloat("_Smoothness", 0.75f); // aspecto húmedo
            if (blood.IsKeywordEnabled("_EMISSION")) blood.SetTexture("_EmissionMap", atlas);
            EditorUtility.SetDirty(blood);
        }

        private static Texture2D EnsureTexture()
        {
            string path = TexturePath;
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            int size = CellSize * Cells;
            var px = new Color32[size * size];
            for (int cy = 0; cy < Cells; cy++)
            for (int cx = 0; cx < Cells; cx++)
                DrawSplat(px, size, cx * CellSize, cy * CellSize, 101 + cy * Cells + cx);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Una salpicadura dentro de su celda (con margen para que no sangre en las vecinas).</summary>
        private static void DrawSplat(Color32[] px, int size, int ox, int oy, int seed)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            var alpha = new float[CellSize * CellSize];
            float c = CellSize / 2f;

            // Charco central.
            Blob(alpha, c + (R() - 0.5f) * 20f, c + (R() - 0.5f) * 20f, CellSize * Mathf.Lerp(0.2f, 0.27f, R()), R() * 100f);
            // Lóbulos pegados al charco.
            for (int i = 0; i < 5; i++)
            {
                float ang = R() * Mathf.PI * 2f, dist = CellSize * Mathf.Lerp(0.12f, 0.24f, R());
                Blob(alpha, c + Mathf.Cos(ang) * dist, c + Mathf.Sin(ang) * dist, CellSize * Mathf.Lerp(0.06f, 0.12f, R()), R() * 100f);
            }
            // Salpicones alargados hacia fuera, con gota al final.
            int streaks = 5 + rng.Next(5);
            for (int i = 0; i < streaks; i++)
            {
                float ang = R() * Mathf.PI * 2f;
                float from = CellSize * 0.18f, to = CellSize * Mathf.Lerp(0.3f, 0.44f, R());
                float width = Mathf.Lerp(2.5f, 6f, R());
                for (float d = from; d < to; d += 1.5f)
                {
                    float t = (d - from) / (to - from);
                    Blob(alpha, c + Mathf.Cos(ang) * d, c + Mathf.Sin(ang) * d, width * (1f - t * 0.6f), 0f);
                }
                Blob(alpha, c + Mathf.Cos(ang) * to, c + Mathf.Sin(ang) * to, width * 1.3f, R() * 100f);
            }
            // Gotas sueltas.
            for (int i = 0; i < 26; i++)
            {
                float ang = R() * Mathf.PI * 2f, dist = CellSize * Mathf.Lerp(0.22f, 0.46f, R());
                Blob(alpha, c + Mathf.Cos(ang) * dist, c + Mathf.Sin(ang) * dist, Mathf.Lerp(1.5f, 5f, R() * R()), R() * 100f);
            }

            // Color: más oscuro en el centro del charco, algo más claro en los bordes finos.
            for (int y = 0; y < CellSize; y++)
            for (int x = 0; x < CellSize; x++)
            {
                float a = alpha[y * CellSize + x];
                float edge = Mathf.Clamp01(a * 1.6f);
                float shade = Mathf.Lerp(1f, 0.55f, edge) * (0.9f + 0.1f * Mathf.PerlinNoise(x * 0.05f + seed, y * 0.05f));
                byte v = (byte)(shade * 255f);
                px[(oy + y) * size + ox + x] = new Color32(v, v, v, (byte)(Mathf.Clamp01(a) * 255f));
            }
        }

        private static void Blob(float[] alpha, float cx, float cy, float r, float seed)
        {
            int margin = 4;
            int x0 = Mathf.Max(margin, (int)(cx - r * 1.4f)), x1 = Mathf.Min(CellSize - 1 - margin, (int)(cx + r * 1.4f));
            int y0 = Mathf.Max(margin, (int)(cy - r * 1.4f)), y1 = Mathf.Min(CellSize - 1 - margin, (int)(cy + r * 1.4f));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - cx, dy = y - cy;
                float ang = Mathf.Atan2(dy, dx);
                float edge = r * (0.78f
                    + 0.28f * Mathf.PerlinNoise(seed + Mathf.Cos(ang) * 1.4f, seed + Mathf.Sin(ang) * 1.4f)
                    + 0.14f * Mathf.PerlinNoise(seed + 30f + Mathf.Cos(ang) * 4f, seed + Mathf.Sin(ang) * 4f));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((edge - d) / Mathf.Max(1f, r * 0.25f) + 0.5f);
                int i = y * CellSize + x;
                if (a > alpha[i]) alpha[i] = a;
            }
        }
    }
}
