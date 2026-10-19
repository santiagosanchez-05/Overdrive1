using System.Collections.Generic;
using System.Linq;
using Overdrive.Level;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Puertas batientes de dos hojas con el modelo DoubleDoor del kit de Kenney (lo añadió Jhael).
    /// Cada hoja del modelo se hornea en una malla propia con la bisagra en su borde exterior, para poder
    /// girarla y estirarla al ancho de cada hueco sin deformarla. Las puertas cuelgan de un objeto raíz
    /// fuera de Level y no entran en el NavMesh: los enemigos las cruzan abriéndolas.
    /// </summary>
    internal static class DoorFactory
    {
        public const string DoorsRootName = "SwingDoors";
        public const string LeftHingeName = "Hinge_L";
        public const string RightHingeName = "Hinge_R";
        public const string LeafName = "Leaf";
        /// <summary>Alto de las puertas de los mapas por celdas (nivel 1): más que los personajes (~2,4 m).</summary>
        public const float DoorHeight = 2.9f;
        public const float LeafThickness = 0.08f;

        public static string ModelPath => "Assets/kenney_furniture-kit/Models/FBX format/DoubleDoor.fbx";
        public static string MeshFolder => AssetFactory.NumberedFolder("Modelos", "02_Modelos") + "/Doors";
        public static string LeftMeshPath => MeshFolder + "/DoorLeaf_L.asset";
        public static string RightMeshPath => MeshFolder + "/DoorLeaf_R.asset";

        public static bool ModelAvailable => AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) != null;

        /// <summary>
        /// Hornea las dos hojas del modelo cerradas, con la bisagra en el origen, la hoja hacia +X (izquierda) o -X
        /// (derecha), el suelo en Y=0 y medidas normalizadas a 1 m de ancho y 1 m de alto (se estiran al colocarlas).
        /// </summary>
        public static (Mesh left, Mesh right, Material[] leftMats, Material[] rightMats) EnsureLeafMeshes()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            // Sin forzar el giro: la raíz del FBX trae su propio giro de importación (de Z arriba a Y arriba).
            var temp = (GameObject)Object.Instantiate(source);
            temp.transform.position = new Vector3(0f, -900f, 0f);
            try
            {
                Transform[] panels = temp.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Panel") && t.parent == temp.transform).ToArray();
                foreach (Transform p in panels) p.localRotation = Quaternion.identity; // cerradas
                // La hoja que queda a la izquierda (X negativa) en la puerta cerrada es la del lado izquierdo.
                var ordered = panels.OrderBy(p => Bounds(p).center.x - temp.transform.position.x).ToArray();
                var (left, leftMats) = Bake(ordered[0], temp.transform, true, LeftMeshPath);
                var (right, rightMats) = Bake(ordered[ordered.Length - 1], temp.transform, false, RightMeshPath);
                return (left, right, leftMats, rightMats);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        private static Bounds Bounds(Transform panel)
        {
            Renderer[] rs = panel.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private static (Mesh, Material[]) Bake(Transform panel, Transform root, bool left, string path)
        {
            var combines = new List<CombineInstance>();
            var materials = new List<Material>();
            // Espacio del mundo con el origen en la raíz: la puerta de pie (Y arriba), sin el giro de importación.
            Matrix4x4 toRoot = Matrix4x4.Translate(-root.position);
            foreach (MeshFilter mf in panel.GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<MeshRenderer>();
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                {
                    combines.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = toRoot * mf.transform.localToWorldMatrix });
                    materials.Add(r.sharedMaterials[Mathf.Min(s, r.sharedMaterials.Length - 1)]);
                }
            }
            var mesh = new Mesh { name = left ? "DoorLeaf_L" : "DoorLeaf_R" };
            mesh.CombineMeshes(combines.ToArray(), false, true);

            // Bisagra en el borde exterior: la hoja izquierda se extiende hacia +X y la derecha hacia -X.
            Bounds b = mesh.bounds;
            float hingeX = left ? b.min.x : b.max.x;
            float width = Mathf.Max(b.size.x, 0.01f), height = Mathf.Max(b.size.y, 0.01f), depth = Mathf.Max(b.size.z, 0.01f);
            Vector3[] v = mesh.vertices;
            for (int i = 0; i < v.Length; i++)
                v[i] = new Vector3((v[i].x - hingeX) / width, (v[i].y - b.min.y) / height, (v[i].z - b.center.z) / depth);
            mesh.vertices = v;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            BuilderUtil.EnsureFolder(MeshFolder);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                existing = mesh;
            }
            else
            {
                existing.Clear();
                existing.vertices = mesh.vertices;
                existing.normals = mesh.normals;
                existing.tangents = mesh.tangents;
                existing.uv = mesh.uv;
                existing.subMeshCount = mesh.subMeshCount;
                for (int s = 0; s < mesh.subMeshCount; s++) existing.SetTriangles(mesh.GetTriangles(s), s);
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
            }
            return (existing, materials.ToArray());
        }

        /// <summary>
        /// Crea o actualiza una puerta: centro del hueco en <paramref name="center"/> (a ras de suelo),
        /// <paramref name="across"/> = dirección a través del hueco (el lado de las dos salas), ancho del hueco.
        /// </summary>
        public static SwingDoor EnsureDoor(Transform parent, string name, Vector3 center, Vector3 across, float width,
            (Mesh left, Mesh right, Material[] leftMats, Material[] rightMats) leaves, float height = DoorHeight)
        {
            GameObject door = BuilderUtil.EnsureChild(parent, name);
            across.y = 0f;
            door.transform.SetPositionAndRotation(center, Quaternion.LookRotation(across.normalized, Vector3.up));
            door.transform.localScale = Vector3.one;

            // Que el NavMesh no la tenga en cuenta (los enemigos la cruzan abriéndola).
            var modifier = BuilderUtil.EnsureComponent<NavMeshModifier>(door);
            modifier.ignoreFromBuild = true;

            float leafWidth = width * 0.5f;
            Transform leftHinge = EnsureLeaf(door.transform, LeftHingeName, new Vector3(-width * 0.5f, 0f, 0f), leaves.left, leaves.leftMats, leafWidth, height);
            Transform rightHinge = EnsureLeaf(door.transform, RightHingeName, new Vector3(width * 0.5f, 0f, 0f), leaves.right, leaves.rightMats, leafWidth, height);

            var swing = BuilderUtil.EnsureComponent<SwingDoor>(door);
            var so = new SerializedObject(swing);
            so.FindProperty("leftHinge").objectReferenceValue = leftHinge;
            so.FindProperty("rightHinge").objectReferenceValue = rightHinge;
            so.FindProperty("width").floatValue = width;
            so.FindProperty("maxAngle").floatValue = 88f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return swing;
        }

        private static Transform EnsureLeaf(Transform door, string hingeName, Vector3 hingePos, Mesh mesh, Material[] mats, float leafWidth, float height)
        {
            GameObject hinge = BuilderUtil.EnsureChild(door, hingeName);
            BuilderUtil.SetLocal(hinge, hingePos, Vector3.one, Quaternion.identity);
            GameObject leaf = BuilderUtil.EnsureChild(hinge.transform, LeafName);
            // Un centímetro más larga: cerradas, las dos hojas se solapan en el centro y no queda rendija por la que
            // se vea o pase una bala (no tienen física, así que al girar no chocan entre ellas).
            BuilderUtil.SetLocal(leaf, Vector3.zero, new Vector3(leafWidth + 0.01f, height, LeafThickness), Quaternion.identity);
            BuilderUtil.EnsureComponent<MeshFilter>(leaf).sharedMesh = mesh;
            var renderer = BuilderUtil.EnsureComponent<MeshRenderer>(leaf);
            renderer.sharedMaterials = mats;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // Cerrada para las balas y la vista: una caja que ocupa toda la hoja.
            var box = BuilderUtil.EnsureComponent<BoxCollider>(leaf);
            Bounds b = mesh.bounds;
            box.center = b.center;
            box.size = new Vector3(Mathf.Abs(b.size.x), b.size.y, Mathf.Max(b.size.z, 1f));
            return hinge.transform;
        }

        /// <summary>Puertas de un nivel hecho con mapa (nivel 1): una por hueco de la lista del mapa.</summary>
        public static void BuildMapDoors(Scene scene, LevelLayout layout)
        {
            Transform root = EnsureRoot(scene);
            if (!ModelAvailable || layout.Doorways.Length == 0)
            {
                RemoveOthers(root, new string[0]);
                return;
            }
            var leaves = EnsureLeafMeshes();
            var names = new List<string>();
            foreach (Doorway d in layout.Doorways)
            {
                LevelMap map = layout.Floors[d.Floor];
                var (c2, r2) = d.Second;
                Vector3 center = (map.CellToWorld(d.Col, d.Row) + map.CellToWorld(c2, r2)) * 0.5f;
                // A través del hueco: en una pared horizontal (filas), de norte a sur; en una vertical, de oeste a este.
                Vector3 across = d.Vertical ? Vector3.right : Vector3.forward;
                EnsureDoor(root, d.Name, center, across, 2f * map.CellSize, leaves);
                names.Add(d.Name);
            }
            RemoveOthers(root, names);
        }

        /// <summary>Marcos de puerta del nivel 2 (escena de Rommel) en los que se pone una puerta batiente.</summary>
        public static readonly string[] Level02Frames = { "ClubDoorFrame", "DoorFrame", "StripDoorFrame" };

        /// <summary>
        /// Puertas en los marcos del nivel 2: el hueco se mide con rayos desde el centro del marco hacia los dos lados
        /// (hasta las jambas), a 1 m del suelo. Los marcos de los baños ya tienen sus puertas y no se tocan.
        /// </summary>
        public static void BuildFrameDoors(Scene scene)
        {
            Transform root = EnsureRoot(scene);
            if (!ModelAvailable)
            {
                RemoveOthers(root, new string[0]);
                return;
            }
            var leaves = EnsureLeafMeshes();
            Physics.SyncTransforms();
            var names = new List<string>();
            foreach (Transform frame in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                         .Where(t => Level02Frames.Contains(t.name)))
            {
                if (!TryMeasureOpening(frame, out Vector3 center, out Vector3 along, out float width)) continue;
                string name = "Door_" + frame.name;
                // En marcos de un modelo (nivel 2) la hoja llena el hueco: tan alta como el marco, sin pasarse.
                float frameHeight = FrameHeight(frame);
                float height = frameHeight > 0f ? Mathf.Max(2f, frameHeight - 0.08f) : DoorHeight;
                EnsureDoor(root, name, center, Vector3.Cross(along, Vector3.up), width, leaves, height);
                names.Add(name);
            }
            RemoveOthers(root, names);
        }

        private static float FrameHeight(Transform frame)
        {
            Renderer[] rs = frame.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return 0f;
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b.size.y;
        }

        /// <summary>Centro (a ras de suelo), dirección de la pared y ancho libre del hueco de un marco.</summary>
        public static bool TryMeasureOpening(Transform frame, out Vector3 center, out Vector3 along, out float width)
        {
            center = default;
            along = default;
            width = 0f;
            Renderer[] rs = frame.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return false;
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            along = b.size.x >= b.size.z ? Vector3.right : Vector3.forward;
            float half = (b.size.x >= b.size.z ? b.size.x : b.size.z) * 0.5f;
            Vector3 probe = new Vector3(b.center.x, b.min.y + 1f, b.center.z);
            float a = Distance(probe, along, half), c = Distance(probe, -along, half);
            if (a <= 0.3f || c <= 0.3f) return false; // el centro no es un hueco
            width = a + c;
            Vector3 mid = probe + along * ((a - c) * 0.5f);
            center = new Vector3(mid.x, b.min.y, mid.z);
            return width > 1f;
        }

        private static float Distance(Vector3 from, Vector3 dir, float max)
        {
            float best = max;
            foreach (RaycastHit hit in Physics.RaycastAll(from, dir, max, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<SwingDoor>() != null) continue;
                if (hit.collider.GetComponentInParent<Overdrive.Enemies.Enemy>() != null) continue;
                if (hit.collider is CharacterController) continue;
                if (hit.collider.GetComponent<Overdrive.Level.BulletWall>() != null) continue;
                best = Mathf.Min(best, hit.distance);
            }
            return best;
        }

        /// <summary>Raíz de las puertas en la escena (fuera de Level, para que el horneado del NavMesh las ignore).</summary>
        public static Transform EnsureRoot(Scene scene) => BuilderUtil.EnsureRoot(scene, DoorsRootName).transform;

        /// <summary>Quita las puertas que ya no están en la lista.</summary>
        public static void RemoveOthers(Transform root, ICollection<string> keep)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                if (!keep.Contains(root.GetChild(i).name)) Object.DestroyImmediate(root.GetChild(i).gameObject);
        }
    }
}
