using System.Linq;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Story;
using Overdrive.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>Coloca a Nina en los niveles y deja cada enemigo con el modelo de su arma también en el editor.</summary>
    internal static class StoryFactory
    {
        public const string NinaName = "Nina";
        public const string Level02Path = "Assets/00_Scenes/Level02.unity";

        /// <summary>
        /// Distancia de la cámara cenital por nivel: la casa del nivel 1 es pequeña; el club del nivel 2 es enorme y,
        /// con la cámara igual de lejos, los personajes se veían diminutos. Más cerca, se ven grandes y con detalle.
        /// </summary>
        public const float Level01CameraDistance = 17f;
        public const float Level02CameraDistance = 13f;

        public static void SetCameraDistance(Scene scene, float distance)
        {
            foreach (var cam in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Overdrive.CameraRig.TopDownCamera>(true)))
            {
                var so = new SerializedObject(cam);
                so.FindProperty("distance").floatValue = distance;
                so.ApplyModifiedPropertiesWithoutUndo();
                cam.ApplyCameraOffset(); // la cámara guardada en la escena ya a la nueva distancia
            }
        }

        public static float CameraDistance(Scene scene)
        {
            var cam = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Overdrive.CameraRig.TopDownCamera>(true)).FirstOrDefault();
            return cam != null ? new SerializedObject(cam).FindProperty("distance").floatValue : 0f;
        }

        /// <summary>Celda de Nina en el nivel 1: rincón suroeste de la sala inicial, a un paso del jugador y fuera del paso.</summary>
        public static readonly Vector2Int Level01Cell = new Vector2Int(1, 7);

        /// <summary>Nina en el nivel 1, mirando hacia el inicio del jugador.</summary>
        public static GameObject EnsureLevel01(Scene scene, LevelMap ground)
        {
            Vector3 position = ground.CellToWorld(Level01Cell.x, Level01Cell.y);
            var (pc, pr) = ground.FindAll(LevelMap.PlayerSpawn)[0];
            return EnsureNina(scene, position, ground.CellToWorld(pc, pr), StoryLines.Level01);
        }

        public static GameObject EnsureNina(Scene scene, Vector3 position, Vector3 lookAt, string[] lines)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFactory.NinaPrefabPath);
            GameObject nina = BuilderUtil.FindRoot(scene, NinaName);
            if (prefab == null)
            {
                if (nina != null) Object.DestroyImmediate(nina);
                return null;
            }
            if (nina != null && PrefabUtility.GetCorrespondingObjectFromSource(nina) != prefab)
            {
                Object.DestroyImmediate(nina);
                nina = null;
            }
            if (nina == null)
            {
                nina = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                nina.name = NinaName;
            }
            Vector3 to = lookAt - position;
            to.y = 0f;
            nina.transform.SetPositionAndRotation(position, to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to) : Quaternion.identity);
            PrefabUtility.RecordPrefabInstancePropertyModifications(nina.transform);

            var dialogue = nina.GetComponent<NpcDialogue>();
            var so = new SerializedObject(dialogue);
            so.FindProperty("speaker").stringValue = StoryLines.NinaName;
            SerializedProperty prop = so.FindProperty("lines");
            prop.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++) prop.GetArrayElementAtIndex(i).stringValue = lines[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return nina;
        }

        /// <summary>Quita la chica de ambiente anterior (FemaleNPC, sin diálogo): Nina la sustituye.</summary>
        public static void RemoveOldNpcs(Scene scene)
        {
            foreach (Transform t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                         .Where(t => t.name.StartsWith("FemaleNPC")).ToList())
                if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        /// <summary>Enciende en cada enemigo de la escena el modelo de su arma (al jugar lo hace él solo en Awake).</summary>
        public static int ApplyEnemyLooks(Scene scene)
        {
            int count = 0;
            foreach (EnemyVisual visual in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnemyVisual>(true)))
            {
                var enemy = visual.GetComponent<Enemy>();
                visual.Apply(enemy != null && enemy.Weapon != null ? enemy.Weapon.Definition.Type : (WeaponType?)null);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Nivel 2 (escena de Rommel): no se reconstruye; solo se ponen los modelos de los enemigos y a Nina
        /// junto a la entrada, a un lado del pasillo y dentro del alcance de diálogo del jugador.
        /// </summary>
        public static void EnsureLevel02()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Level02Path) == null) return;
            Scene scene = EditorSceneManager.OpenScene(Level02Path, OpenSceneMode.Single);
            RemoveOldNpcs(scene);
            ApplyEnemyLooks(scene);
            foreach (GameObject root in scene.GetRootGameObjects().Where(r => r.CompareTag("Player")))
                PlayerModelFactory.RevertModelScaleOverride(root);
            SetCameraDistance(scene, Level02CameraDistance);
            DoorFactory.BuildFrameDoors(scene);
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(r => r.CompareTag("Player"));
            // Nina ya colocada no cuenta como obstáculo al buscar su sitio (si no, cambiaría de sitio en cada construcción).
            GameObject previous = BuilderUtil.FindRoot(scene, NinaName);
            if (previous != null) previous.SetActive(false);
            Physics.SyncTransforms();
            Vector3 spot = default;
            bool found = player != null && FindNinaSpot(player.transform, out spot);
            if (previous != null) previous.SetActive(true);
            if (found) EnsureNina(scene, spot, player.transform.position, StoryLines.Level02);
            else if (previous != null) Object.DestroyImmediate(previous); // mejor sin Nina que tapando el paso
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Radio sin paredes ni muebles alrededor de Nina: así está en una sala abierta y nunca tapa un pasillo.</summary>
        public const float NinaClearance = 1.2f;

        /// <summary>
        /// Nada sólido (pared, mueble, personaje) a menos de <see cref="NinaClearance"/> m: rayos horizontales a la altura
        /// de las rodillas y del pecho en 16 direcciones (sirve también con las paredes de malla del nivel 2).
        /// </summary>
        public static bool IsOpenSpot(Vector3 point, Transform ignore, float clearance = NinaClearance)
        {
            foreach (float height in new[] { 0.4f, 1.2f })
            for (int i = 0; i < 16; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * height, dir, clearance, ~0, QueryTriggerInteraction.Ignore))
                {
                    Transform root = hit.collider.transform.root;
                    if ((ignore != null && root == ignore.root) || root.name == NinaName) continue;
                    if (ignore != null && hit.collider.gameObject.scene != ignore.gameObject.scene) continue; // otra escena abierta a la vez
                    return false;
                }
            }
            return true;
        }

        /// <summary>Distancia máxima a la entrada del jugador (por camino) para colocar a Nina.</summary>
        public const float NinaMaxRoute = 30f;

        /// <summary>
        /// Busca el punto abierto más cercano al jugador (por camino del NavMesh, hasta <see cref="NinaMaxRoute"/> m):
        /// en una sala, sin paredes ni muebles alrededor, para que su obstáculo nunca corte un pasillo.
        /// </summary>
        public static bool FindNinaSpot(Transform player, out Vector3 spot)
        {
            if (TryFindEntranceSpot(player, out spot)) return true;
            Vector3 origin = player.position;
            float best = float.MaxValue;
            spot = origin;
            var path = new NavMeshPath();
            var routes = KeyRoutes(origin);
            for (float distance = 2f; distance <= NinaMaxRoute; distance += 1f)
            for (int i = 0; i < 24; i++) // cada 15 grados
            {
                Vector3 candidate = origin + Quaternion.Euler(0f, i * 15f, 0f) * player.forward * distance;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.y - origin.y) > 0.3f) continue;
                if (!IsOpenSpot(hit.position, player)) continue;
                if (DistanceToRoutes(hit.position, routes) < RouteMargin) continue;
                if (!NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float route = RouteLength(path.corners);
                if (route > NinaMaxRoute || route >= best) continue;
                best = route;
                spot = hit.position;
            }
            return best < float.MaxValue;
        }

        // A side of the four-metre entrance leaves more than two metres of corridor free.
        // These margins include Nina's .35 m capsule and the player's .3 m controller.
        public const float EntranceClearance = .7f;
        public const float EntranceRouteMargin = .8f;

        public static bool TryFindEntranceSpot(Transform player, out Vector3 spot)
        {
            var path = new NavMeshPath();
            foreach (float side in new[] { -1f, 1f })
            foreach (float along in new[] { -.75f, .6f })
            {
                Vector3 candidate = player.position + player.right * (side * 1.05f) + player.forward * along;
                if (!NavMesh.SamplePosition(candidate, out var hit, .3f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.y - player.position.y) > .3f) continue;
                if (!IsOpenSpot(hit.position, player, EntranceClearance)) continue;
                // Exit reinforcements fan out across this hall; reserve the actual centre
                // passage instead of excluding every future guard's straight route.
                if (Mathf.Abs(Vector3.Dot(hit.position - player.position, player.right)) < EntranceRouteMargin) continue;
                if (!NavMesh.CalculatePath(player.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                if (RouteLength(path.corners) > 3f) continue;
                spot = hit.position; return true;
            }
            spot = default; return false;
        }

        [MenuItem("TopDown/Nivel 02/Acercar a Nina al pasillo de entrada")]
        public static void UpdateNinaEntrance()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Detén Play Mode antes de mover a Nina.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new System.InvalidOperationException("Guarda las escenas antes de actualizar a Nina.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(Level02Path, OpenSceneMode.Single);
                var player = scene.GetRootGameObjects().First(r => r.CompareTag("Player"));
                var previous = BuilderUtil.FindRoot(scene, NinaName);
                bool active = previous != null && previous.activeSelf;
                if (previous != null) previous.SetActive(false);
                Vector3 spot;
                bool found;
                try { Physics.SyncTransforms(); found = TryFindEntranceSpot(player.transform, out spot); }
                finally { if (previous != null) previous.SetActive(active); }
                if (!found) throw new System.InvalidOperationException("No hay espacio seguro junto al jugador para Nina.");
                var nina = EnsureNina(scene, spot, player.transform.position, StoryLines.Level02);
                PrefabUtility.RecordPrefabInstancePropertyModifications(nina.transform);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("Nina junto a la entrada, a " + Vector3.Distance(spot, player.transform.position).ToString("F1") + " m del jugador.");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        /// <summary>Distancia mínima a las rutas principales: Nina no se pone en medio del camino.</summary>
        public const float RouteMargin = 1.5f;

        /// <summary>Rutas del NavMesh desde la entrada hasta cada enemigo, el maletín y la salida.</summary>
        public static System.Collections.Generic.List<Vector3[]> KeyRoutes(Vector3 origin)
        {
            var targets = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include).Select(e => e.transform.position)
                .Concat(Object.FindObjectsByType<BriefcasePickup>(FindObjectsInactive.Include).Select(b => b.transform.position))
                .Concat(Object.FindObjectsByType<ExitZone>(FindObjectsInactive.Include).Select(x => x.transform.position));
            var routes = new System.Collections.Generic.List<Vector3[]>();
            foreach (Vector3 target in targets)
            {
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, path) && path.corners.Length > 1) routes.Add(path.corners);
            }
            return routes;
        }

        public static float DistanceToRoutes(Vector3 point, System.Collections.Generic.IEnumerable<Vector3[]> routes)
        {
            float best = float.MaxValue;
            Vector3 p = new Vector3(point.x, 0f, point.z);
            foreach (Vector3[] route in routes)
            for (int i = 1; i < route.Length; i++)
            {
                if (Mathf.Abs(route[i].y - point.y) > 1f && Mathf.Abs(route[i - 1].y - point.y) > 1f) continue; // otro piso
                Vector3 a = new Vector3(route[i - 1].x, 0f, route[i - 1].z), ab = new Vector3(route[i].x, 0f, route[i].z) - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector3.Distance(p, a + ab * t));
            }
            return best;
        }

        public static float RouteLength(Vector3[] corners)
        {
            float length = 0f;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }
    }
}
