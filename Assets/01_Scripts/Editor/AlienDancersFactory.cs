using System.Collections.Generic;
using System.Linq;
using Overdrive.Enemies;
using Overdrive.Level;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Aliens bailando en la pista de Level02: modelo Alien.fbx con los bailes de 08_Animations/Alien_Animaciones.
    /// Cada uno baila al azar (AlienDancer). Son obstáculos para el jugador y la IA, pero no cambian el NavMesh.
    /// </summary>
    internal static class AlienDancersFactory
    {
        public const string ModelPath = "Assets/02_Modelos/Characters/Alien.fbx";
        public const string ClipsFolder = "Assets/08_Animations/Alien_Animaciones";
        public const string ControllerPath = ClipsFolder + "/Alien.controller";
        public const string RootName = "Alien Dancers";
        public const int Count = 6;

        /// <summary>Solo bailes (Punching no es un baile).</summary>
        public static readonly string[] Dances =
        {
            "alien@Breakdance Freeze Var 2.fbx",
            "alien@Dancing Maraschino Step.fbx",
            "alien@Hip Hop Dancing.fbx",
            "alien@Salsa Dancing.fbx",
            "alien@Silly Dancing.fbx",
        };

        // Centro de la pista de Level02 (bajo las luces DanceWash) y radios donde buscar sitio.
        private static readonly Vector3 DanceFloor = Vector3.zero;
        private const float MinRadius = 2f;
        private const float MaxRadius = 14f;
        private const float Spacing = 2f;

        [MenuItem("TopDown/Nivel 02/Colocar aliens bailando")]
        public static void PlaceInLevel02()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Aliens] Detén Play Mode primero."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(Level02Builder.ScenePath, OpenSceneMode.Single);

            GameObject old = BuilderUtil.FindRoot(scene, RootName);
            if (old != null) Object.DestroyImmediate(old);
            Physics.SyncTransforms();

            AnimatorController controller = EnsureController();
            List<Vector3> spots = FindSpots(scene);
            if (spots.Count < Count)
                Debug.LogWarning($"[Aliens] Solo hay sitio libre para {spots.Count} aliens en la pista.");

            var root = new GameObject(RootName).transform;
            for (int i = 0; i < spots.Count; i++) CreateAlien(root, i, spots[i], controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Aliens] {spots.Count} aliens bailando en Level02.");
        }

        public static AnimatorController EnsureController()
        {
            PlayerModelFactory.EnsureHumanoid(ModelPath, false, false);
            foreach (string clip in Dances) PlayerModelFactory.EnsureHumanoid(ClipsFolder + "/" + clip, true, true);

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            for (int i = 0; i < Dances.Length; i++)
            {
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipsFolder + "/" + Dances[i])
                    .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip == null) Debug.LogWarning("[Aliens] Falta la animación " + Dances[i]);
                AnimatorState state = sm.AddState(AlienDancer.StatePrefix + i, new Vector3(300f, i * 70f));
                state.motion = clip;
                if (i == 0) sm.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>Puntos libres del NavMesh alrededor de la pista, separados entre sí y lejos de enemigos y del jugador.</summary>
        private static List<Vector3> FindSpots(Scene scene)
        {
            var avoid = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.GetComponent<Enemy>() != null || t.CompareTag("Player") || t.name == StoryFactory.NinaName)
                .Select(t => t.position).ToList();

            var spots = new List<Vector3>();
            var rng = new System.Random(2026); // misma colocación en cada construcción
            for (float radius = MinRadius; radius <= MaxRadius && spots.Count < Count; radius += 1f)
            {
                int samples = Mathf.CeilToInt(2f * Mathf.PI * radius / 1.2f);
                float offset = (float)rng.NextDouble() * 360f;
                for (int i = 0; i < samples && spots.Count < Count; i++)
                {
                    Vector3 p = DanceFloor + Quaternion.Euler(0f, offset + i * 360f / samples, 0f) * Vector3.forward * radius;
                    if (!NavMesh.SamplePosition(p + Vector3.up, out var hit, 1.5f, NavMesh.AllAreas)) continue;
                    if (Mathf.Abs(hit.position.y - DanceFloor.y) > 2f) continue; // solo en la planta de la pista, no en balcones
                    if (!NavMesh.FindClosestEdge(hit.position, out var edge, NavMesh.AllAreas) || edge.distance < 0.8f) continue;
                    if (spots.Any(s => Vector3.Distance(s, hit.position) < Spacing)) continue;
                    if (avoid.Any(a => Vector3.Distance(a, hit.position) < 2.5f)) continue;
                    spots.Add(hit.position);
                }
            }
            return spots;
        }

        private static void CreateAlien(Transform parent, int index, Vector3 position, AnimatorController controller)
        {
            var alien = new GameObject("Alien_" + (index + 1).ToString("D2"));
            alien.transform.SetParent(parent, false);
            Vector3 toCenter = DanceFloor - position;
            toCenter.y = 0f;
            float yaw = (toCenter.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCenter).eulerAngles.y : 0f) + (index % 3 - 1) * 25f;
            alien.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            // Sólido para el jugador, las balas y la IA (sin tallar el NavMesh, así no corta pasillos).
            var capsule = alien.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            var obstacle = alien.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.center = capsule.center;
            obstacle.height = capsule.height;
            obstacle.radius = capsule.radius;
            obstacle.carving = false;

            GameObject model = CharacterFactory.EnsureModelChild(alien.transform, "Model", ModelPath, "Alien", controller);
            var animator = model.GetComponent<Animator>();
            alien.AddComponent<AlienDancer>().Setup(animator, Dances.Length);
        }
    }
}
