using System.Collections.Generic;
using UnityEngine;

namespace Overdrive.Level
{
    public enum LedColor
    {
        Violet,
        Crimson,
        Teal,
        Magenta,
        ColdBlue,
    }

    public enum LedLayout
    {
        /// <summary>Por el suelo o pegada a algo: una curva suave que pasa por todos los puntos.</summary>
        Lying,
        /// <summary>Colgando de punto a punto (de pared a pared), con caída entre cada par.</summary>
        Hanging,
    }

    /// <summary>
    /// Un cable de LEDs de un solo color. Los puntos están en coordenadas de celda del mapa:
    /// x = columna, y = altura sobre el suelo de su piso (m), z = fila. Admite medias celdas.
    /// </summary>
    public readonly struct LedCable
    {
        public readonly int Floor;
        public readonly LedColor Color;
        public readonly LedLayout Layout;
        public readonly Vector3[] Points;
        /// <summary>Cuánto cae el cable en el centro de cada tramo colgante (m).</summary>
        public readonly float Sag;
        /// <summary>Tira con un contacto roto: parpadea (la tira y sus luces).</summary>
        public readonly bool Flicker;
        /// <summary>Cuánto se curva hacia un lado cada tramo colgante (m): un cable flojo no cae recto.</summary>
        public readonly float Bow;

        public LedCable(int floor, LedColor color, LedLayout layout, Vector3[] points, float sag = 0f, bool flicker = false, float bow = 0.35f)
        {
            Floor = floor; Color = color; Layout = layout; Points = points; Sag = sag; Flicker = flicker; Bow = bow;
        }

        public static Vector3 P(float col, float height, float row) => new Vector3(col, height, row);
    }

    /// <summary>Geometría de los cables (lógica pura, en metros del mundo).</summary>
    public static class LedPath
    {
        /// <summary>Curva Catmull-Rom que pasa por todos los puntos, con <paramref name="subdivisions"/> tramos entre cada par.</summary>
        public static Vector3[] Smooth(IReadOnlyList<Vector3> points, int subdivisions)
        {
            var result = new List<Vector3>();
            if (points.Count < 2) { result.AddRange(points); return result.ToArray(); }
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 p0 = points[Mathf.Max(i - 1, 0)];
                Vector3 p1 = points[i];
                Vector3 p2 = points[i + 1];
                Vector3 p3 = points[Mathf.Min(i + 2, points.Count - 1)];
                for (int s = 0; s < subdivisions; s++)
                {
                    float t = s / (float)subdivisions;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
                                      (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
                }
            }
            result.Add(points[points.Count - 1]);
            return result.ToArray();
        }

        /// <summary>
        /// Cable colgando entre cada par de puntos: parábola que cae <paramref name="sag"/> metros en el centro y se curva
        /// <paramref name="bow"/> metros hacia un lado (alternando el lado en cada tramo, como un cable flojo).
        /// </summary>
        public static Vector3[] Hang(IReadOnlyList<Vector3> anchors, float sag, int samplesPerSpan, float bow = 0f)
        {
            var result = new List<Vector3>();
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                Vector3 a = anchors[i], b = anchors[i + 1];
                Vector3 span = b - a;
                span.y = 0f;
                Vector3 side = span.sqrMagnitude > 0f ? Vector3.Cross(Vector3.up, span.normalized) : Vector3.zero;
                float sign = i % 2 == 0 ? 1f : -1f;
                for (int s = 0; s < samplesPerSpan; s++)
                {
                    float t = s / (float)samplesPerSpan;
                    float bulge = 4f * t * (1f - t);
                    Vector3 p = Vector3.Lerp(a, b, t) + side * (bow * sign * bulge);
                    p.y -= sag * bulge;
                    result.Add(p);
                }
            }
            if (anchors.Count > 0) result.Add(anchors[anchors.Count - 1]);
            return result.ToArray();
        }

        public static float Length(IReadOnlyList<Vector3> path)
        {
            float length = 0f;
            for (int i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
            return length;
        }

        /// <summary>Puntos repartidos a lo largo del recorrido, separados ~<paramref name="spacing"/> m (al menos uno).</summary>
        public static Vector3[] EvenlySpaced(IReadOnlyList<Vector3> path, float spacing)
        {
            float length = Length(path);
            int count = Mathf.Max(1, Mathf.RoundToInt(length / spacing));
            var result = new Vector3[count];
            for (int k = 0; k < count; k++) result[k] = PointAt(path, (k + 0.5f) / count * length);
            return result;
        }

        public static Vector3 PointAt(IReadOnlyList<Vector3> path, float distance)
        {
            for (int i = 1; i < path.Count; i++)
            {
                float segment = Vector3.Distance(path[i - 1], path[i]);
                if (distance <= segment || i == path.Count - 1)
                    return Vector3.Lerp(path[i - 1], path[i], segment > 0f ? Mathf.Clamp01(distance / segment) : 0f);
                distance -= segment;
            }
            return path.Count > 0 ? path[0] : Vector3.zero;
        }
    }

    /// <summary>Parpadeo de tira rota. Misma fórmula que FlickerLevel en el shader Overdrive/LedLine.</summary>
    public static class LedFlicker
    {
        public const float LowLevel = 0.15f;

        public static float Level(float time, float seed)
        {
            float x = (Mathf.Floor(time * 10f) + seed * 13f) * 91.345f;
            float n = Frac(Mathf.Sin(x) * 47453.5453f);
            return n < 0.18f ? LowLevel : 1f;
        }

        private static float Frac(float v) => v - Mathf.Floor(v);
    }
}
