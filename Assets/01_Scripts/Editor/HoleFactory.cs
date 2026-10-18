using System.Collections.Generic;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Huecos del piso de arriba con aspecto de suelo destrozado: el mapa los marca como celdas cuadradas sin losa
    /// ('O'), y aquí se rellena el contorno del cuadrado con un borde roto (malla irregular, hundida hacia dentro)
    /// para que la abertura quede redondeada y dentada. El borde tiene colisión de cajas (se puede pisar hasta el
    /// filo) y abajo, en el piso inferior, quedan cascotes caídos sin colisión.
    /// </summary>
    internal static class HoleFactory
    {
        public const string HolesName = "Holes";
        public const string DebrisName = "HoleDebris";

        /// <summary>Puntos del contorno de la abertura (más = borde más detallado).</summary>
        private const int Segments = 40;
        /// <summary>Tamaño de la rejilla con que se aproxima la colisión del borde.</summary>
        private const float ColliderStep = 0.25f;
        /// <summary>Radio de la abertura respecto a la mitad del lado del cuadrado (mínimo y máximo).</summary>
        private const float MinRadius = 0.88f, MaxRadius = 1f;

        /// <summary>Rehace los bordes rotos de los huecos de un piso y los cascotes del piso de abajo.</summary>
        public static void Build(Transform floorRoot, Transform debrisParent, Level.LevelMap map, float thickness,
            Material material)
        {
            Transform holes = BuilderUtil.EnsureChild(floorRoot, HolesName).transform;
            for (int i = holes.childCount - 1; i >= 0; i--) Object.DestroyImmediate(holes.GetChild(i).gameObject);
            Transform debris = BuilderUtil.EnsureChild(debrisParent, $"{DebrisName}{map.FloorIndex}").transform;
            for (int i = debris.childCount - 1; i >= 0; i--) Object.DestroyImmediate(debris.GetChild(i).gameObject);

            float belowY = Core.Floors.BaseY(map.FloorIndex - 1);
            foreach (Rect hole in HoleRects(map))
            {
                int seed = Mathf.RoundToInt(hole.x * 31f + hole.y * 17f);
                var random = new System.Random(seed);
                List<Vector2> inner = Opening(hole, random);
                string name = $"Hole_{hole.center.x:0.##}_{hole.center.y:0.##}";

                var rim = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                rim.transform.SetParent(holes, false);
                rim.transform.localPosition = new Vector3(0f, map.BaseY, 0f);
                rim.GetComponent<MeshFilter>().sharedMesh = RimMesh(hole, inner, thickness, random, name);
                rim.GetComponent<MeshRenderer>().sharedMaterial = material;
                AddRimColliders(rim, hole, inner, thickness);

                AddDebris(debris, hole, belowY, material, random);
            }
        }

        /// <summary>Rectángulos (x, z en el plano) de cada grupo de celdas 'O' conectadas.</summary>
        public static List<Rect> HoleRects(Level.LevelMap map)
        {
            var result = new List<Rect>();
            var seen = new HashSet<(int, int)>();
            foreach (var (col, row) in map.FindAll(Level.LevelMap.Hole))
            {
                if (seen.Contains((col, row))) continue;
                int minC = col, maxC = col, minR = row, maxR = row;
                var queue = new Queue<(int, int)>();
                queue.Enqueue((col, row));
                seen.Add((col, row));
                while (queue.Count > 0)
                {
                    var (c, r) = queue.Dequeue();
                    minC = Mathf.Min(minC, c); maxC = Mathf.Max(maxC, c);
                    minR = Mathf.Min(minR, r); maxR = Mathf.Max(maxR, r);
                    foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        var next = (c + dc, r + dr);
                        if (seen.Contains(next) || map.At(next.Item1, next.Item2) != Level.LevelMap.Hole) continue;
                        seen.Add(next);
                        queue.Enqueue(next);
                    }
                }
                float cs = map.CellSize;
                float xMin = (minC - 0.5f) * cs, xMax = (maxC + 0.5f) * cs;
                float zMin = -(maxR + 0.5f) * cs, zMax = -(minR - 0.5f) * cs;
                result.Add(Rect.MinMaxRect(xMin, zMin, xMax, zMax));
            }
            return result;
        }

        /// <summary>Distancia del centro al borde del rectángulo en la dirección dada.</summary>
        private static float SquareDistance(Rect hole, Vector2 dir)
        {
            float hx = hole.width * 0.5f, hz = hole.height * 0.5f;
            float tx = Mathf.Abs(dir.x) > 1e-5f ? hx / Mathf.Abs(dir.x) : float.MaxValue;
            float tz = Mathf.Abs(dir.y) > 1e-5f ? hz / Mathf.Abs(dir.y) : float.MaxValue;
            return Mathf.Min(tx, tz);
        }

        private static Vector2 Dir(int i)
        {
            float a = i * Mathf.PI * 2f / Segments;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        /// <summary>Contorno irregular de la abertura: casi un círculo, con mordiscos y picos de suelo roto.</summary>
        private static List<Vector2> Opening(Rect hole, System.Random random)
        {
            float radius = Mathf.Min(hole.width, hole.height) * 0.5f;
            var points = new List<Vector2>(Segments);
            for (int i = 0; i < Segments; i++)
            {
                Vector2 dir = Dir(i);
                float jitter = Mathf.Lerp(MinRadius, MaxRadius, (float)random.NextDouble());
                // Algunos picos de losa que entran hacia el hueco.
                if (random.NextDouble() < 0.15) jitter = MinRadius;
                float r = Mathf.Min(radius * jitter, SquareDistance(hole, dir) * 0.97f);
                points.Add(hole.center + dir * r);
            }
            return points;
        }

        private static Mesh RimMesh(Rect hole, List<Vector2> inner, float thickness, System.Random random, string name)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Hundimiento del filo y altura de la rotura, por punto.
            var sag = new float[Segments];
            var bottom = new float[Segments];
            for (int i = 0; i < Segments; i++)
            {
                sag[i] = (float)random.NextDouble() * 0.08f;
                bottom[i] = -thickness + ((float)random.NextDouble() - 0.5f) * 0.12f;
            }

            for (int i = 0; i < Segments; i++)
            {
                int j = (i + 1) % Segments;
                Vector2 o0 = hole.center + Dir(i) * SquareDistance(hole, Dir(i));
                Vector2 o1 = hole.center + Dir(j) * SquareDistance(hole, Dir(j));
                Vector2 i0 = inner[i], i1 = inner[j];

                // Cara de arriba: del borde del cuadrado (a ras del suelo) al filo roto (algo hundido).
                AddQuad(vertices, uvs, triangles,
                    new Vector3(o0.x, 0f, o0.y), new Vector3(i0.x, -sag[i], i0.y),
                    new Vector3(i1.x, -sag[j], i1.y), new Vector3(o1.x, 0f, o1.y), doubleSided: false);
                // Cara de la rotura, vista desde dentro del hueco.
                AddQuad(vertices, uvs, triangles,
                    new Vector3(i0.x, -sag[i], i0.y), new Vector3(i0.x, bottom[i], i0.y),
                    new Vector3(i1.x, bottom[j], i1.y), new Vector3(i1.x, -sag[j], i1.y), doubleSided: true);
                // Cara de abajo, por si se ve desde el piso inferior.
                AddQuad(vertices, uvs, triangles,
                    new Vector3(o1.x, -thickness, o1.y), new Vector3(i1.x, bottom[j], i1.y),
                    new Vector3(i0.x, bottom[i], i0.y), new Vector3(o0.x, -thickness, o0.y), doubleSided: false);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Cuadrilátero a-b-c-d en sentido horario visto desde su cara visible (vértices propios: aristas duras).</summary>
        private static void AddQuad(List<Vector3> v, List<Vector2> uv, List<int> t,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool doubleSided)
        {
            void Side(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                int start = v.Count;
                foreach (Vector3 p in new[] { p0, p1, p2, p3 })
                {
                    v.Add(p);
                    // Proyección plana: horizontal para el suelo, con la altura para las caras de la rotura.
                    uv.Add(new Vector2((p.x + p.y) * 0.5f, (p.z + p.y) * 0.5f));
                }
                t.Add(start); t.Add(start + 1); t.Add(start + 2);
                t.Add(start); t.Add(start + 2); t.Add(start + 3);
            }
            Side(a, b, c, d);
            if (doubleSided) Side(d, c, b, a);
        }

        /// <summary>
        /// Colisión del borde con cajas (como el resto del suelo, para que GroundProbe lo reconozca):
        /// rejilla fina sobre el cuadrado, solo donde queda losa fuera de la abertura, juntando tramos por filas.
        /// </summary>
        private static void AddRimColliders(GameObject rim, Rect hole, List<Vector2> inner, float thickness)
        {
            int cols = Mathf.CeilToInt(hole.width / ColliderStep);
            int rows = Mathf.CeilToInt(hole.height / ColliderStep);
            float stepX = hole.width / cols, stepZ = hole.height / rows;
            for (int r = 0; r < rows; r++)
            {
                float z = hole.yMin + (r + 0.5f) * stepZ;
                int c = 0;
                while (c < cols)
                {
                    float x = hole.xMin + (c + 0.5f) * stepX;
                    if (Inside(inner, new Vector2(x, z))) { c++; continue; }
                    int start = c;
                    while (c < cols && !Inside(inner, new Vector2(hole.xMin + (c + 0.5f) * stepX, z))) c++;
                    var box = rim.AddComponent<BoxCollider>();
                    float x0 = hole.xMin + start * stepX, x1 = hole.xMin + c * stepX;
                    box.center = new Vector3((x0 + x1) * 0.5f, -thickness * 0.5f, z);
                    box.size = new Vector3(x1 - x0, thickness, stepZ);
                }
            }
        }

        private static bool Inside(List<Vector2> polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Cascotes del suelo roto caídos en el piso de abajo. Sin colisión: no estorban a nadie.</summary>
        private static void AddDebris(Transform parent, Rect hole, float groundY, Material material, System.Random random)
        {
            float radius = Mathf.Min(hole.width, hole.height) * 0.5f;
            int count = 7 + random.Next(4);
            for (int i = 0; i < count; i++)
            {
                GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.name = $"Debris_{hole.center.x:0.##}_{hole.center.y:0.##}_{i}";
                Object.DestroyImmediate(chunk.GetComponent<Collider>());
                chunk.transform.SetParent(parent, false);

                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = Mathf.Sqrt((float)random.NextDouble()) * radius * 1.1f;
                var size = new Vector3(
                    Mathf.Lerp(0.15f, 0.55f, (float)random.NextDouble()),
                    Mathf.Lerp(0.06f, 0.18f, (float)random.NextDouble()),
                    Mathf.Lerp(0.15f, 0.5f, (float)random.NextDouble()));
                Vector2 p = hole.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                chunk.transform.localPosition = new Vector3(p.x, groundY + size.y * 0.4f, p.y);
                chunk.transform.localRotation = Quaternion.Euler(
                    Mathf.Lerp(-15f, 15f, (float)random.NextDouble()),
                    (float)random.NextDouble() * 360f,
                    Mathf.Lerp(-15f, 15f, (float)random.NextDouble()));
                chunk.transform.localScale = size;
                chunk.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }
    }
}
