using System.Linq;
using Overdrive.Combat;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Story;
using Overdrive.Weapons;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>Verificación de los modelos de enemigos por arma, sus animaciones, Nina y su diálogo (niveles 1 y 2).</summary>
    internal static partial class Verifier
    {
        private static void VerifyCharacters(Checker c, LevelLayout layout)
        {
            VerifyCharacterLogic(c);
            VerifyEnemyModelsPrefab(c);
            VerifyEnemyPoseSimulation(c);
            VerifyEnemyLooksInScene(c, SceneManager.GetActiveScene(), "Nivel 1");
            c.Near(StoryFactory.CameraDistance(SceneManager.GetActiveScene()), StoryFactory.Level01CameraDistance,
                "Nivel 1: distancia de cámara para que los personajes se vean bien");
            VerifyNina(c, layout);
            VerifyDialogueSimulation(c);
            VerifyLevel02Characters(c);
        }

        private static void VerifyCharacterLogic(Checker c)
        {
            c.Check(EnemyVisualLogic.ModelIndex(WeaponType.Bat) == 0 && EnemyVisualLogic.ModelIndex(WeaponType.Knife) == 1 &&
                    EnemyVisualLogic.ModelIndex(WeaponType.Shotgun) == 2 && EnemyVisualLogic.ModelIndex(WeaponType.Smg) == 3 &&
                    EnemyVisualLogic.ModelIndex(null) == 0, "Cada arma tiene su modelo de enemigo (desarmado usa el del bate)");
            c.Near(EnemyVisualLogic.Speed(0f, 2.2f, 5f), 0f, "Enemigo quieto: Speed 0");
            c.Near(EnemyVisualLogic.Speed(2.2f, 2.2f, 5f), 0.5f, "Enemigo patrullando: anda (Speed 0.5)");
            c.Near(EnemyVisualLogic.Speed(5f, 2.2f, 5f), 1f, "Enemigo persiguiendo: corre (Speed 1)");
            c.Check(EnemyVisualLogic.AttackTrigger(WeaponType.Knife) == EnemyVisual.StabParam &&
                    EnemyVisualLogic.AttackTrigger(WeaponType.Bat) == EnemyVisual.SwingParam &&
                    EnemyVisualLogic.AttackTrigger(null) == EnemyVisual.SwingParam, "El de cuchillo apuñala; el resto golpea");
            c.Check(DialogueLogic.VisibleChars("Hola", 0.05f, 40f) == 2 && DialogueLogic.VisibleChars("Hola", 9f, 40f) == 4,
                "El diálogo se escribe letra a letra");
            c.Near(DialogueLogic.LineDuration("1234567890", 10f, 2f), 3f, "Cada línea dura lo que tarda en escribirse más el tiempo de lectura");
        }

        private static void VerifyEnemyModelsPrefab(Checker c)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            var visual = prefab != null ? prefab.GetComponent<EnemyVisual>() : null;
            c.NotNull(visual, "El prefab Enemy tiene EnemyVisual");
            if (visual == null) return;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterFactory.EnemyControllerPath);
            c.Check(controller != null && CharacterFactory.EnemyControllerComplete(controller),
                "El Animator Controller de enemigos tiene quieto/andar/correr, golpe, puñalada y caída");

            int animated = 0;
            foreach (var (weapon, name, file) in CharacterFactory.EnemyModels)
            {
                GameObject model = visual.Models.ElementAtOrDefault(EnemyVisualLogic.ModelIndex(weapon));
                c.Check(model != null && model.transform.parent == prefab.transform && model.name == CharacterFactory.ModelChildName(weapon),
                    $"Enemigo de {weapon}: modelo {name} dentro del prefab");
                if (model == null) continue;
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFactory.ModelPath(file));
                c.Check(PrefabUtility.GetCorrespondingObjectFromSource(model) == source, $"Enemigo de {weapon}: usa {file}");
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                c.Check(renderers.Length > 0 && renderers.All(r => r.sharedMaterials.All(m => m != null && m.IsKeywordEnabled("_EMISSION"))),
                    $"Enemigo de {weapon}: material con su textura y algo de luz propia");
                bool humanoid = CharacterFactory.LoadAvatar(CharacterFactory.ModelPath(file)) != null;
                var animator = model.GetComponent<Animator>();
                if (humanoid)
                {
                    animated++;
                    c.Check(animator != null && animator.runtimeAnimatorController == controller && animator.avatar != null && !animator.applyRootMotion,
                        $"Enemigo de {weapon}: animado con el controller de enemigos");
                }
                else c.Check(animator == null, $"Enemigo de {weapon}: sin esqueleto humanoide, se queda sin Animator");
            }
            c.Check(CharacterFactory.LoadAvatar(CharacterFactory.ModelPath("Enemy_Mask_Character.fbx")) != null,
                "El enmascarado (el que trae animaciones propias) es humanoide");
            Debug.Log($"[Verify] Modelos de enemigo animados: {animated} de {CharacterFactory.EnemyModels.Length}");

            Renderer body = prefab.transform.Find("Body").GetComponent<Renderer>();
            c.Check(!body.enabled, "La cápsula del enemigo ya no se ve en el editor");
        }

        /// <summary>Simulación: cada arma enciende un único modelo; de pie mide como el jugador y al caer queda en el suelo.</summary>
        private static void VerifyEnemyPoseSimulation(Checker c)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            if (prefab == null || prefab.GetComponent<EnemyVisual>() == null) return;
            var enemyGo = (GameObject)Object.Instantiate(prefab, new Vector3(600f, 0f, 600f), Quaternion.identity);
            try
            {
                var visual = enemyGo.GetComponent<EnemyVisual>();
                foreach (var (weapon, name, _) in CharacterFactory.EnemyModels)
                {
                    visual.Apply(weapon);
                    c.Check(visual.Models.Count(m => m.activeSelf) == 1 && visual.ActiveModel == visual.Models[EnemyVisualLogic.ModelIndex(weapon)],
                        $"Con {weapon} solo se ve el modelo {name}");
                    c.Check(enemyGo.transform.Find("Body").GetComponent<Renderer>().forceRenderingOff, $"Con {weapon} la cápsula queda oculta");

                    Animator animator = visual.Animator;
                    if (animator == null) continue;
                    animator.Rebind();
                    animator.Update(0.3f);
                    Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                    float standing = head.position.y - enemyGo.transform.position.y;
                    c.Check(standing > 1.4f * PlayerModelFactory.CharacterScale && standing < 2.0f * PlayerModelFactory.CharacterScale, $"{name} de pie: cabeza a {standing:F2} m (como el jugador)");
                    animator.SetFloat(EnemyVisual.SpeedParam, 1f);
                    float drift = 0f;
                    for (int i = 0; i < 20; i++)
                    {
                        animator.Update(0.05f);
                        Vector3 d = animator.GetBoneTransform(HumanBodyBones.Hips).position - enemyGo.transform.position;
                        d.y = 0f;
                        drift = Mathf.Max(drift, d.magnitude);
                    }
                    c.Check(drift < 0.5f * PlayerModelFactory.CharacterScale, $"{name} corre en el sitio ({drift:F2} m)");
                    animator.SetFloat(EnemyVisual.SpeedParam, 0f);
                    animator.SetBool(EnemyVisual.DownParam, true);
                    for (int i = 0; i < 80; i++) animator.Update(0.05f);
                    float down = head.position.y - enemyGo.transform.position.y;
                    c.Check(down < 0.7f * PlayerModelFactory.CharacterScale, $"{name} derribado: cae al suelo (cabeza a {down:F2} m)");
                }
            }
            finally
            {
                Object.DestroyImmediate(enemyGo);
            }
        }

        private static void VerifyEnemyLooksInScene(Checker c, Scene scene, string label)
        {
            var enemies = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Enemy>(true)).ToList();
            c.Check(enemies.Count > 0, $"{label}: hay enemigos");
            foreach (Enemy enemy in enemies)
            {
                var visual = enemy.GetComponent<EnemyVisual>();
                WeaponType? weapon = enemy.Weapon != null ? enemy.Weapon.Definition.Type : (WeaponType?)null;
                GameObject expected = visual != null ? visual.Models.ElementAtOrDefault(EnemyVisualLogic.ModelIndex(weapon)) : null;
                c.Check(expected != null && expected.activeSelf && visual.Models.Count(m => m != null && m.activeSelf) == 1,
                    $"{label}/{enemy.name}: se ve el modelo de su arma ({weapon?.ToString() ?? "sin arma"})");
            }
        }

        private static void VerifyNina(Checker c, LevelLayout layout)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFactory.NinaPrefabPath);
            c.NotNull(prefab, "Existe el prefab de Nina");
            if (prefab == null) return;
            var dialogue = prefab.GetComponent<NpcDialogue>();
            var model = prefab.transform.Find("Model");
            c.Check(dialogue != null && model != null, "Nina tiene modelo y diálogo");
            var animator = model != null ? model.GetComponent<Animator>() : null;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterFactory.NinaControllerPath);
            c.Check(animator != null && animator.avatar != null && animator.runtimeAnimatorController == controller && dialogue.Animator == animator,
                "Nina está animada (reposo y hablar)");
            c.Check(controller != null && CharacterFactory.NinaControllerComplete(controller), "El controller de Nina pasa de reposo a hablar con Talking");
            if (animator != null)
            {
                var copy = (GameObject)Object.Instantiate(prefab, new Vector3(650f, 0f, 650f), Quaternion.identity);
                try
                {
                    var copyAnimator = copy.transform.Find("Model").GetComponent<Animator>();
                    copyAnimator.Rebind();
                    copyAnimator.Update(0.3f);
                    float head = copyAnimator.GetBoneTransform(HumanBodyBones.Head).position.y - copy.transform.position.y;
                    c.Check(head > 1.4f * PlayerModelFactory.CharacterScale && head < 1.8f * PlayerModelFactory.CharacterScale, $"Nina mide como el resto de personajes (cabeza a {head:F2} m)");
                }
                finally
                {
                    Object.DestroyImmediate(copy);
                }
            }
            var obstacle = prefab.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            c.Check(prefab.GetComponent<CapsuleCollider>() != null && obstacle != null && !obstacle.carving, "No se la atraviesa; la IA la esquiva sin que corte ninguna ruta del NavMesh");

            GameObject nina = BuilderUtil.FindRoot(SceneManager.GetActiveScene(), StoryFactory.NinaName);
            c.NotNull(nina, "Nivel 1: Nina está en la escena");
            if (nina == null) return;
            var sceneDialogue = nina.GetComponent<NpcDialogue>();
            c.Check(sceneDialogue.Lines.SequenceEqual(StoryLines.Level01) && sceneDialogue.Speaker == StoryLines.NinaName,
                "Nivel 1: Nina dice sus líneas de la casa del dealer");
            LevelMap ground = layout.Floors[0];
            Vector3 cell = ground.CellToWorld(StoryFactory.Level01Cell.x, StoryFactory.Level01Cell.y);
            c.Check(ground.IsWalkable(StoryFactory.Level01Cell.x, StoryFactory.Level01Cell.y) && Vector3.Distance(nina.transform.position, cell) < 0.01f,
                "Nivel 1: Nina está en suelo libre de la sala inicial");
            var (pc, pr) = ground.FindAll(LevelMap.PlayerSpawn)[0];
            float toSpawn = Vector3.Distance(nina.transform.position, ground.CellToWorld(pc, pr));
            c.Check(toSpawn <= sceneDialogue.TalkRadius, $"Nivel 1: Nina habla nada más empezar ({toSpawn:F1} m del jugador)");
            Vector3 facing = ground.CellToWorld(pc, pr) - nina.transform.position;
            facing.y = 0f;
            c.Check(Vector3.Angle(nina.transform.forward, facing) < 5f, "Nivel 1: Nina mira hacia el jugador");
        }

        private static void VerifyDialogueSimulation(Checker c)
        {
            var go = new GameObject("DialogueSim");
            var target = new GameObject("TargetSim");
            try
            {
                var dialogue = go.AddComponent<NpcDialogue>();
                var so = new SerializedObject(dialogue);
                SerializedProperty lines = so.FindProperty("lines");
                lines.arraySize = 2;
                lines.GetArrayElementAtIndex(0).stringValue = "Esta primera línea es larga para que tarde en escribirse.";
                lines.GetArrayElementAtIndex(1).stringValue = "Dos.";
                so.ApplyModifiedPropertiesWithoutUndo();

                target.transform.position = new Vector3(10f, 0f, 0f);
                dialogue.Tick(0.1f, target.transform, false);
                c.Check(!dialogue.Talking, "Simulación: lejos, Nina no habla");
                target.transform.position = new Vector3(2f, 0f, 0f);
                for (int i = 0; i < 5; i++) dialogue.Tick(0.1f, target.transform, false);
                c.Check(dialogue.Talking && dialogue.LineIndex == 0, "Simulación: al acercarse empieza a hablar");
                c.Check(Vector3.Angle(go.transform.forward, Vector3.right) < 45f, "Simulación: se gira hacia el jugador");
                dialogue.Tick(0.01f, target.transform, true);
                c.Check(dialogue.LineIndex == 0, "Simulación: E termina de escribir la línea");
                dialogue.Tick(0.01f, target.transform, true);
                c.Check(dialogue.LineIndex == 1, "Simulación: E pasa a la siguiente línea");
                target.transform.position = new Vector3(10f, 0f, 0f);
                dialogue.Tick(5f, target.transform, false);
                c.Check(!dialogue.Talking && dialogue.LineIndex == 1, "Simulación: al alejarse se calla y no pierde por dónde iba");
                target.transform.position = new Vector3(2f, 0f, 0f);
                for (int i = 0; i < 100; i++) dialogue.Tick(0.1f, target.transform, false);
                c.Check(dialogue.Finished && !dialogue.Talking, "Simulación: termina sola tras la última línea");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(target);
            }
        }

        private static void VerifyLevel02Characters(Checker c)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(StoryFactory.Level02Path) == null) return;
            Scene scene = EditorSceneManager.OpenScene(StoryFactory.Level02Path, OpenSceneMode.Additive);
            try
            {
                VerifyEnemyLooksInScene(c, scene, "Nivel 2");
                var doors = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Overdrive.Level.SwingDoor>(true)).ToArray();
                c.Check(doors.Length >= 2 && doors.All(d => d.Width > 1f && d.IsClosed),
                    $"Nivel 2: hay puertas batientes en los marcos ({doors.Length}: {string.Join(", ", doors.Select(d => $"{d.name} {d.Width:F1} m"))})");
                c.Near(StoryFactory.CameraDistance(scene), StoryFactory.Level02CameraDistance,
                    "Nivel 2: la cámara está más cerca (el club es enorme y los personajes se veían diminutos)");
                GameObject nina = scene.GetRootGameObjects().FirstOrDefault(r => r.name == StoryFactory.NinaName);
                c.NotNull(nina, "Nivel 2: Nina está en el club");
                GameObject player = scene.GetRootGameObjects().FirstOrDefault(r => r.CompareTag("Player"));
                if (nina == null || player == null) return;
                var dialogue = nina.GetComponent<NpcDialogue>();
                c.Check(dialogue.Lines.SequenceEqual(StoryLines.Level02), "Nivel 2: Nina dice sus líneas del Elysium");
                float distance = Vector3.Distance(nina.transform.position, player.transform.position);
                c.Check(distance <= StoryFactory.NinaMaxRoute && distance > 1f, $"Nivel 2: Nina está cerca de la entrada ({distance:F1} m del jugador)");
                Physics.SyncTransforms();
                float toRoutes = StoryFactory.DistanceToRoutes(nina.transform.position, StoryFactory.KeyRoutes(player.transform.position));
                c.Check(toRoutes >= StoryFactory.RouteMargin - 0.05f, $"Nivel 2: Nina no está en medio de ninguna ruta principal ({toRoutes:F1} m)");
                c.Check(StoryFactory.IsOpenSpot(nina.transform.position, player.transform),
                    $"Nivel 2: Nina está en zona abierta (nada a menos de {StoryFactory.NinaClearance} m) y no tapa ningún paso");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
