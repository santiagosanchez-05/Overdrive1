using System.Collections.Generic;
using System.Linq;
using Overdrive.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Ajustes del nivel 2 para personajes de casi 3 m de alto:
    /// se abre todo lo que hay encima del hueco de cada puerta (travesaño del marco y pared),
    /// y cada pared recortada recibe una <see cref="BulletWall"/> a altura completa para que no se dispare por encima.
    /// </summary>
    internal static class Level02Fixes
    {
        public const string BulletWallsName = "BulletWalls";

        /// <summary>Paredes que el constructor recorta en altura (ver Level02Environment.PrepareGeometry).</summary>
        public static readonly string[] CutWalls =
            { "HallWalls01", "HallWalls02", "HallWalls03", "HallWalls04", "DressingWalls", "StripWalls" };

        [MenuItem("TopDown/Nivel 02/Ajustar puertas y paredes contra balas")]
        public static void ApplyMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Level02Builder.ScenePath)
            {
                Debug.LogWarning("[Level02Fixes] Abre la escena Level02 primero.");
                return;
            }
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Level02Fixes] Hecho. Guarda la escena (Ctrl+S).");
        }

        public static void Apply(Scene scene)
        {
            Transform level = BuilderUtil.FindRoot(scene, "Level")?.transform;
            if (level == null)
            {
                Debug.LogWarning("[Level02Fixes] No se encontró el objeto Level.");
                return;
            }
            Physics.SyncTransforms();
            int restored = RestoreDoorLeaves(level);
            int headers = OpenWallsAboveDoors(level);
            int walls = AddBulletWalls(scene, level);
            Debug.Log($"[Level02Fixes] Puertas abiertas (marco y pared de encima): {headers} mallas. " +
                      $"Hojas de puerta restauradas: {restored}. Paredes contra balas: {walls}.");
        }

        // ---------- Pared encima de las puertas ----------

        /// <summary>Desde esta altura sobre el suelo de la puerta hacia arriba, se abre la pared que hay encima del hueco.</summary>
        public const float HeaderCutFrom = 1.5f;

        /// <summary>Hasta esta altura sobre el suelo de la puerta: más que los personajes (~2,9 m), sin llegar a los pisos de arriba.</summary>
        public const float HeaderCutTo = 3.4f;

        /// <summary>Grosor de pared que se abre a cada lado del hueco.</summary>
        private const float HeaderDepth = 0.7f;

        /// <summary>
        /// Las paredes del club van en mallas aparte del marco: encima de cada hueco queda un tramo de pared más bajo
        /// que los personajes. Se quitan las caras de pared (verticales o hacia abajo) dentro de una caja sobre cada
        /// hueco; las caras hacia arriba (suelos de pisos superiores) se conservan. Solo cambia el dibujo, no la colisión.
        /// </summary>
        private static int OpenWallsAboveDoors(Transform level)
        {
            Transform model = level.Find("Elysium");
            if (model == null) return 0;

            var boxes = new List<(string door, Plane[] planes, Bounds bounds)>();
            foreach (MeshFilter frame in model.GetComponentsInChildren<MeshFilter>(false))
            {
                if (!frame.name.Contains("DoorFrame") || frame.sharedMesh == null) continue;
                if (!DoorFactory.TryMeasureOpening(frame.transform, out Vector3 center, out Vector3 along, out float width)) continue;
                Vector3 across = Vector3.Cross(along, Vector3.up).normalized;
                float half = width * 0.5f - 0.02f;
                Vector3 bottom = center + Vector3.up * HeaderCutFrom;
                Vector3 top = center + Vector3.up * HeaderCutTo;
                // Planos con la normal hacia dentro de la caja.
                Plane[] planes =
                {
                    new Plane(-along, center + along * half), new Plane(along, center - along * half),
                    new Plane(-across, center + across * HeaderDepth), new Plane(across, center - across * HeaderDepth),
                    new Plane(Vector3.up, bottom), new Plane(Vector3.down, top),
                };
                var bounds = new Bounds((bottom + top) * 0.5f, Vector3.zero);
                bounds.Encapsulate(bottom + along * half + across * HeaderDepth);
                bounds.Encapsulate(bottom - along * half - across * HeaderDepth);
                bounds.Encapsulate(top + along * half + across * HeaderDepth);
                bounds.Encapsulate(top - along * half - across * HeaderDepth);
                boxes.Add((frame.name, planes, bounds));
            }

            // Vista cenital (Elysium) y primera persona (Roof: paredes enteras, apagado hasta pulsar V).
            int opened = OpenIn(model, boxes, "Opened_", includeInactive: false);
            Transform roof = level.Find(RoofFactory.RootName);
            if (roof != null) opened += OpenIn(roof, boxes, "FP_Opened_", includeInactive: true);
            return opened;
        }

        private static int OpenIn(Transform root, List<(string door, Plane[] planes, Bounds bounds)> boxes, string prefix, bool includeInactive)
        {
            int opened = 0;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(includeInactive))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Renderer>() == null || IsDoorLeaf(filter.name)) continue;
                // Los techos son piezas aparte: no se agujerean.
                if (Level02Environment.IsOverhead(filter.name)) continue;
                Bounds world = WorldBounds(filter);
                Mesh mesh = filter.sharedMesh;
                var doors = new List<string>();
                foreach (var (door, planes, bounds) in boxes)
                {
                    if (!world.Intersects(bounds)) continue;
                    Mesh cut = SubtractBox(filter.transform, mesh, planes);
                    if (cut == null) continue;
                    mesh = cut;
                    doors.Add(door);
                }
                if (doors.Count == 0) continue;
                mesh.name = prefix + filter.name;
                filter.sharedMesh = Level02Environment.StoreAsset(mesh, $"{Level02Builder.GeneratedPath}/{prefix}{filter.name}.asset");
                Debug.Log($"[Level02Fixes] {root.name}/{filter.name}: se abre la pared sobre {string.Join(", ", doors)}.");
                opened++;
            }
            return opened;
        }

        /// <summary>Caja en el mundo de la malla (también con el objeto apagado, cuando el Renderer no la da).</summary>
        private static Bounds WorldBounds(MeshFilter filter)
        {
            Bounds local = filter.sharedMesh.bounds;
            Matrix4x4 m = filter.transform.localToWorldMatrix;
            var world = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z);
                world.Encapsulate(m.MultiplyPoint3x4(corner));
            }
            return world;
        }

        /// <summary>Hoja de puerta del modelo (se abre y se cierra), no marco ni pared.</summary>
        private static bool IsDoorLeaf(string name) => name.Contains("Door") && !name.Contains("Frame");

        /// <summary>
        /// Una versión anterior de este ajuste recortó también las hojas de las puertas de los baños:
        /// vuelven a su malla original del modelo del club.
        /// </summary>
        private static int RestoreDoorLeaves(Transform level)
        {
            Transform model = level.Find("Elysium");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Level02Environment.ModelPath);
            if (model == null || source == null) return 0;
            int restored = 0;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!IsDoorLeaf(filter.name) || filter.sharedMesh == null || !filter.sharedMesh.name.StartsWith("Opened_")) continue;
                MeshFilter original = source.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == filter.name);
                if (original == null || original.sharedMesh == null) continue;
                filter.sharedMesh = original.sharedMesh;
                Debug.Log($"[Level02Fixes] {filter.name}: se restaura su malla original.");
                restored++;
            }
            return restored;
        }

        private struct Vert
        {
            public Vector3 Position, Normal;
            public Vector2 Uv;

            public static Vert Lerp(Vert a, Vert b, float t) => new Vert
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Normal = Vector3.Lerp(a.Normal, b.Normal, t).normalized,
                Uv = Vector2.Lerp(a.Uv, b.Uv, t),
            };
        }

        /// <summary>
        /// Malla sin las caras de pared que caen dentro de la caja (convexa, planos con normal hacia dentro).
        /// Los triángulos que la cruzan se recortan. Devuelve null si no hay nada dentro.
        /// </summary>
        private static Mesh SubtractBox(Transform transform, Mesh source, Plane[] planes)
        {
            Vector3[] positions = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;
            var output = new List<Vert>();
            var submeshes = new List<List<int>>();
            bool changed = false;

            float Distance(Plane p, Vert v) => p.GetDistanceToPoint(transform.TransformPoint(v.Position));

            List<Vert> Clip(List<Vert> poly, Plane plane, bool keepInside)
            {
                var result = new List<Vert>();
                for (int i = 0; i < poly.Count; i++)
                {
                    Vert a = poly[i], b = poly[(i + 1) % poly.Count];
                    float da = Distance(plane, a), db = Distance(plane, b);
                    if (!keepInside) { da = -da; db = -db; }
                    if (da >= 0f) result.Add(a);
                    if ((da >= 0f) != (db >= 0f)) result.Add(Vert.Lerp(a, b, da / (da - db)));
                }
                return result;
            }

            void Emit(List<Vert> poly, List<int> indices)
            {
                if (poly.Count < 3) return;
                int start = output.Count;
                output.AddRange(poly);
                for (int i = 1; i + 1 < poly.Count; i++)
                {
                    indices.Add(start);
                    indices.Add(start + i);
                    indices.Add(start + i + 1);
                }
            }

            for (int s = 0; s < source.subMeshCount; s++)
            {
                var indices = new List<int>();
                int[] tris = source.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var poly = new List<Vert>(3);
                    for (int j = 0; j < 3; j++)
                    {
                        int k = tris[i + j];
                        poly.Add(new Vert
                        {
                            Position = positions[k],
                            Normal = k < normals.Length ? normals[k] : Vector3.up,
                            Uv = k < uvs.Length ? uvs[k] : Vector2.zero,
                        });
                    }

                    // Las caras hacia arriba (suelos) se quedan enteras.
                    Vector3 w0 = transform.TransformPoint(poly[0].Position);
                    Vector3 face = Vector3.Cross(transform.TransformPoint(poly[1].Position) - w0, transform.TransformPoint(poly[2].Position) - w0);
                    bool upward = face.sqrMagnitude > 1e-12f && face.normalized.y > 0.5f;
                    if (upward)
                    {
                        Emit(poly, indices);
                        continue;
                    }

                    // Partes fuera de la caja: fuera del primer plano, o dentro de él y fuera del siguiente, etc.
                    List<Vert> remaining = poly;
                    foreach (Plane plane in planes)
                    {
                        Emit(Clip(remaining, plane, keepInside: false), indices);
                        remaining = Clip(remaining, plane, keepInside: true);
                        if (remaining.Count < 3) break;
                    }
                    if (remaining.Count >= 3) changed = true; // lo que queda dentro de la caja se descarta
                }
                submeshes.Add(indices);
            }
            if (!changed) return null;

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(output.Select(v => v.Position).ToList());
            mesh.SetNormals(output.Select(v => v.Normal).ToList());
            mesh.SetUVs(0, output.Select(v => v.Uv).ToList());
            mesh.subMeshCount = submeshes.Count;
            for (int s = 0; s < submeshes.Count; s++) mesh.SetTriangles(submeshes[s], s);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        // ---------- Paredes contra balas ----------

        /// <summary>
        /// Una pared invisible por cada pared recortada, con la malla a altura completa de su copia en "Roof"
        /// (los techos y paredes de primera persona). Va en la raíz de la escena, fuera de Level, para no entrar
        /// en el NavMesh.
        /// </summary>
        private static int AddBulletWalls(Scene scene, Transform level)
        {
            Transform roof = level.Find(RoofFactory.RootName);
            Transform model = level.Find("Elysium");
            if (roof == null || model == null)
            {
                Debug.LogWarning("[Level02Fixes] Faltan Level/Roof o Level/Elysium: no se añaden paredes contra balas.");
                return 0;
            }

            Transform root = BuilderUtil.EnsureRoot(scene, BulletWallsName).transform;
            for (int i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.localScale = Vector3.one;

            int added = 0;
            foreach (string name in CutWalls)
            {
                MeshFilter full = roof.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == name && f.sharedMesh != null);
                if (full == null)
                {
                    Debug.LogWarning($"[Level02Fixes] No hay copia completa de {name} en Roof.");
                    continue;
                }
                var go = new GameObject("BulletWall_" + name);
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(full.transform.position, full.transform.rotation);
                go.transform.localScale = full.transform.lossyScale;
                go.AddComponent<MeshCollider>().sharedMesh = full.sharedMesh;
                go.AddComponent<BulletWall>();
                go.isStatic = true;
                added++;
            }
            return added;
        }
    }
}
