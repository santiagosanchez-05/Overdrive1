using System.Collections.Generic;
using System.Linq;
using Overdrive.Level;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>Verificación de los cables de LEDs: geometría, colocación realista, luces reales y parpadeo.</summary>
    internal static partial class Verifier
    {
        private static void VerifyLedCables(Checker c, LevelLayout layout)
        {
            VerifyLedLogic(c);
            VerifyLedPlacement(c, layout);
            VerifyLedScene(c, layout);
        }

        private static void VerifyLedLogic(Checker c)
        {
            var pts = new[] { new Vector3(0f, 0f, 0f), new Vector3(3f, 0f, 1f), new Vector3(6f, 0f, -1f) };
            Vector3[] smooth = LedPath.Smooth(pts, 8);
            c.Check(smooth.Length == 17, $"La curva tiene 8 tramos por segmento ({smooth.Length} puntos)");
            c.Near(smooth[0], pts[0], "La curva empieza en el primer punto");
            c.Near(smooth[8], pts[1], "La curva pasa por los puntos intermedios");
            c.Near(smooth[16], pts[2], "La curva termina en el último punto");

            Vector3[] hang = LedPath.Hang(new[] { new Vector3(0f, 2f, 0f), new Vector3(4f, 2f, 0f) }, 0.5f, 12);
            c.Near(hang.Min(p => p.y), 1.5f, "Un cable colgante cae lo indicado en el centro", 0.01f);
            c.Near(hang[0].y, 2f, "Cuelga desde la altura del anclaje");
            Vector3[] bowed = LedPath.Hang(new[] { new Vector3(0f, 2f, 0f), new Vector3(4f, 2f, 0f), new Vector3(8f, 2f, 0f) }, 0.5f, 12, 0.3f);
            c.Near(Mathf.Abs(bowed[6].z), 0.3f, "Un cable flojo se curva hacia un lado en el centro del tramo", 0.01f);
            c.Near(bowed[18].z, -bowed[6].z, "Y el tramo siguiente se curva hacia el otro lado", 0.01f);
            c.Near(bowed[12], new Vector3(4f, 2f, 0f), "La curva pasa por los anclajes");

            Vector3[] line = { Vector3.zero, new Vector3(9f, 0f, 0f) };
            Vector3[] spaced = LedPath.EvenlySpaced(line, 3f);
            c.Check(spaced.Length == 3 && Mathf.Approximately(spaced[0].x, 1.5f) && Mathf.Approximately(spaced[2].x, 7.5f),
                "Las luces se reparten a lo largo del cable");

            var levels = Enumerable.Range(0, 2000).Select(i => LedFlicker.Level(i * 0.05f, 2f)).ToList();
            c.Check(levels.All(l => l == 1f || l == LedFlicker.LowLevel), "El parpadeo solo encendido o casi apagado");
            float on = levels.Count(l => l == 1f) / (float)levels.Count;
            c.Check(on > 0.7f && on < 0.97f, $"Una tira rota está casi siempre encendida y a ratos cae ({on:P0} encendida)");
            c.Check(LedFlicker.Level(12.34f, 3f) == LedFlicker.Level(12.34f, 3f), "El parpadeo es determinista (tira y luces a la vez)");
        }

        /// <summary>Colocación realista: las guirnaldas cuelgan de paredes; las tiras van por suelo pisable; nada atraviesa paredes.</summary>
        private static void VerifyLedPlacement(Checker c, LevelLayout layout)
        {
            c.Check(layout.LedCables.Length >= 8, $"Hay cables por todo el nivel ({layout.LedCables.Length})");
            c.Check(layout.LedCables.Select(x => x.Floor).Distinct().Count() == layout.Floors.Length, "Hay cables en todos los pisos");
            c.Check(layout.LedCables.Any(x => x.Layout == LedLayout.Hanging) && layout.LedCables.Any(x => x.Layout == LedLayout.Lying),
                "Hay guirnaldas colgando y tiras por el suelo");
            c.Check(layout.LedCables.Any(x => x.Flicker), "Hay al menos una tira rota que parpadea");

            for (int i = 0; i < layout.LedCables.Length; i++)
            {
                LedCable cable = layout.LedCables[i];
                LevelMap map = layout.Floors[cable.Floor];
                string name = SceneFactory.LedCableName(i, cable);

                if (cable.Layout == LedLayout.Hanging)
                {
                    foreach (Vector3 p in cable.Points)
                    {
                        int col = Mathf.RoundToInt(p.x), row = Mathf.RoundToInt(p.z);
                        bool anchored = map.IsWall(Mathf.RoundToInt(p.x + 0.4f), row) || map.IsWall(Mathf.RoundToInt(p.x - 0.4f), row) ||
                                        map.IsWall(col, Mathf.RoundToInt(p.z + 0.4f)) || map.IsWall(col, Mathf.RoundToInt(p.z - 0.4f));
                        c.Check(anchored, $"{name}: el anclaje ({p.x},{p.z}) está pegado a una pared");
                    }
                }

                // Ningún punto del recorrido dentro de una pared, ni por encima de las paredes, ni (si va al suelo) sobre un hueco.
                Vector3[] path = SceneFactory.LedWorldPath(layout, cable);
                bool insideWall = false, offFloor = false, tooHigh = false;
                foreach (Vector3 w in path)
                {
                    int col = Mathf.RoundToInt(w.x / map.CellSize), row = Mathf.RoundToInt(-w.z / map.CellSize);
                    if (map.IsWall(col, row)) insideWall = true;
                    if (cable.Layout == LedLayout.Lying && !map.IsWalkable(col, row)) offFloor = true;
                    if (w.y - map.BaseY > SceneFactory.WallHeight - 0.1f) tooHigh = true;
                }
                c.Check(!insideWall, $"{name}: no atraviesa paredes");
                c.Check(!offFloor, $"{name}: la tira va sobre suelo (no sobre huecos ni fuera del piso)");
                c.Check(!tooHigh, $"{name}: queda por debajo del techo de la sala");
            }
        }

        private static void VerifyLedScene(Checker c, LevelLayout layout)
        {
            GameObject level = BuilderUtil.FindRoot(SceneManager.GetActiveScene(), SceneFactory.LevelRoot);
            if (level == null) return;
            c.Check(!level.GetComponentsInChildren<Transform>(true).Any(t => t.name == SceneFactory.OldLedsName),
                "Ya no quedan los cables anteriores encima de las paredes");

            int totalLights = 0;
            for (int i = 0; i < layout.LedCables.Length; i++)
            {
                LedCable cable = layout.LedCables[i];
                string name = SceneFactory.LedCableName(i, cable);
                Transform t = SceneFactory.FloorParent(level.transform, cable.Floor)?.Find(SceneFactory.LedCablesName)?.Find(name);
                c.NotNull(t, $"Falta el cable {name}");
                if (t == null) continue;

                var line = t.GetComponent<LineRenderer>();
                Vector3[] path = SceneFactory.LedWorldPath(layout, cable);
                c.Check(line != null && line.positionCount == path.Length && line.useWorldSpace, $"{name}: LineRenderer con su recorrido");
                if (line != null)
                {
                    Material m = line.sharedMaterial;
                    c.Check(m == AssetFactory.Mat(AssetFactory.LedMaterial(cable.Color, cable.Flicker)) && m.shader.name == AssetFactory.ShaderLedLine,
                        $"{name}: usa {AssetFactory.LedMaterial(cable.Color, cable.Flicker)} (Overdrive/LedLine)");
                    c.Check(m != null && m.GetColor("_Color") == AssetFactory.LedRgb(cable.Color), $"{name}: de un solo color ({cable.Color})");
                }
                c.Check(t.GetComponentsInChildren<Collider>(true).Length == 0, $"{name}: sin colisión");

                Light[] lights = t.GetComponentsInChildren<Light>(true);
                int expectedLights = LedPath.EvenlySpaced(path, SceneFactory.LedLightSpacing).Length;
                c.Check(lights.Length == expectedLights, $"{name}: {expectedLights} luces reales ({lights.Length})");
                totalLights += lights.Length;
                foreach (Light l in lights)
                {
                    c.Check(l.type == LightType.Point && l.shadows == LightShadows.None && l.color == AssetFactory.LedRgb(cable.Color) && l.range > 2f,
                        $"{name}/{l.name}: luz puntual de su color, sin sombras");
                    float near = DistanceToPath(path, l.transform.position);
                    c.Check(near < 0.25f, $"{name}/{l.name}: está junto al cable ({near:F2} m)");
                }

                var flicker = t.GetComponent<FlickerLights>();
                if (cable.Flicker)
                {
                    c.Check(flicker != null && flicker.Lights.Length == lights.Length && Mathf.Approximately(flicker.Seed, AssetFactory.LedSeed(cable.Color)),
                        $"{name}: sus luces parpadean con la tira");
                    if (flicker != null)
                    {
                        float low = Enumerable.Range(0, 400).Select(k => k * 0.1f).First(time => LedFlicker.Level(time, flicker.Seed) < 1f);
                        flicker.Apply(low);
                        c.Check(lights.All(l => Mathf.Approximately(l.intensity, flicker.BaseIntensity * LedFlicker.LowLevel)),
                            "Simulación: cuando la tira rota cae, sus luces bajan a la vez");
                        flicker.Apply(low + 0.1f * Enumerable.Range(1, 50).First(k => LedFlicker.Level(low + 0.1f * k, flicker.Seed) == 1f));
                        c.Check(lights.All(l => Mathf.Approximately(l.intensity, flicker.BaseIntensity)), "Y vuelven cuando la tira vuelve");
                    }
                }
                else
                {
                    c.Check(flicker == null, $"{name}: no parpadea");
                }
            }
            c.Check(totalLights >= 15, $"Los cables iluminan el entorno con luces reales ({totalLights})");
        }

        /// <summary>Distancia de un punto al recorrido (a sus tramos, no solo a sus vértices).</summary>
        private static float DistanceToPath(IReadOnlyList<Vector3> path, Vector3 point)
        {
            float best = float.MaxValue;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 a = path[i - 1], ab = path[i] - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector3.Distance(point, a + ab * t));
            }
            return best;
        }
    }
}
