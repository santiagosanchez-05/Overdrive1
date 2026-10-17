using System.Linq;
using Overdrive.Combat;
using Overdrive.Player;
using Overdrive.Weapons;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>Verificación del modelo animado del jugador: importación, Animator Controller, prefab y simulación de poses.</summary>
    internal static partial class Verifier
    {
        private static void VerifyPlayerModel(Checker c)
        {
            VerifyPlayerAnimLogic(c);
            if (!PlayerModelFactory.ModelAvailable)
            {
                c.Check(false, "Falta el modelo Player_Origin o alguna animación del jugador");
                return;
            }
            VerifyPlayerModelImports(c);
            VerifyPlayerController(c);
            VerifyPlayerModelPrefab(c);
            VerifyPlayerPoseSimulation(c);
            VerifyOtherCharacterModels(c);
        }

        private static void VerifyOtherCharacterModels(Checker c)
        {
            c.Check(CharacterModelFactory.EnemyModelAvailable, "Existen Enemy_Mask_Character y sus animaciones");
            if (CharacterModelFactory.EnemyModelAvailable)
            {
                var enemyCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterModelFactory.EnemyControllerPath);
                c.Check(enemyCtrl != null && CharacterModelFactory.EnemyControllerIsComplete(enemyCtrl),
                    "EnemyMask.controller tiene Locomotion, StabKnife (daga), SwingBat y Death");
                var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
                Transform enemyModel = enemyPrefab != null ? enemyPrefab.transform.Find(CharacterModelFactory.EnemyModelName) : null;
                c.NotNull(enemyModel, "El prefab Enemy tiene el modelo 3D EnemyModel (Enemy_Mask_Character)");
                var enemyAnim = enemyPrefab != null ? enemyPrefab.GetComponent<Overdrive.Enemies.EnemyAnimator>() : null;
                c.Check(enemyAnim != null && enemyAnim.Animator != null, "Enemy tiene EnemyAnimator conectado");
                c.Check(Overdrive.Enemies.EnemyAnimator.AttackTrigger(WeaponType.Knife) == Overdrive.Enemies.EnemyAnimator.StabKnife,
                    "Enemy_Mask_Character usa ataque de cuchillo/daga (StabKnife)");
            }

            c.Check(CharacterModelFactory.FemaleModelAvailable, "Existen Female_Character y sus animaciones");
            if (CharacterModelFactory.FemaleModelAvailable)
            {
                var femaleCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterModelFactory.FemaleControllerPath);
                c.Check(femaleCtrl != null && CharacterModelFactory.FemaleControllerIsComplete(femaleCtrl),
                    "Female.controller tiene Idle y Talking (sin ataques)");
                var femalePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelFactory.FemalePrefabPath);
                Transform femaleModel = femalePrefab != null ? femalePrefab.transform.Find(CharacterModelFactory.FemaleModelName) : null;
                c.NotNull(femaleModel, "Existe el prefab FemaleCharacter con FemaleModel");
                var npcAnim = femalePrefab != null ? femalePrefab.GetComponent<Overdrive.Level.FemaleNpcAnimator>() : null;
                c.Check(npcAnim != null && npcAnim.Animator != null, "FemaleCharacter tiene FemaleNpcAnimator conectado");
            }
        }

        private static void VerifyPlayerAnimLogic(Checker c)
        {
            Vector3 fwd = Vector3.forward;
            c.Near(PlayerAnimLogic.Move(Vector3.zero, fwd, 6f), 0f, "Quieto: Move 0");
            c.Near(PlayerAnimLogic.Move(new Vector3(0f, 0f, 6f), fwd, 6f), 1f, "Corriendo hacia donde mira: Move 1");
            c.Near(PlayerAnimLogic.Move(new Vector3(0f, 0f, -3f), fwd, 6f), -0.5f, "Retrocediendo a media velocidad: Move -0.5");
            c.Check(PlayerAnimLogic.Move(new Vector3(6f, 0f, 0f), fwd, 6f) > 0.9f, "De lado cuenta como correr de frente");
            c.Near(PlayerAnimLogic.Move(new Vector3(0f, -9f, 20f), fwd, 6f), 1f, "La caída no cuenta y la velocidad se limita a 1");
            c.Near(PlayerAnimLogic.StepSpeed(0f, 5f), 1f, "Quieto, la animación va a su ritmo");
            c.Near(PlayerAnimLogic.StepSpeed(7f, 5f), 1.4f, "Más rápido que el clip: los pasos se aceleran", 0.001f);
            c.Near(PlayerAnimLogic.StepSpeed(1f, 5f), 0.6f, "Muy despacio (sigilo): los pasos se frenan hasta un mínimo");
            c.Near(PlayerAnimLogic.StepSpeed(50f, 5f), 1.8f, "Y no se aceleran sin límite");
            c.Check(PlayerAnimLogic.AttackTrigger(null) == PlayerAnimator.Punch, "Sin arma: puñetazo");
            c.Check(PlayerAnimLogic.AttackTrigger(WeaponType.Bat) == PlayerAnimator.SwingBat, "Bate: golpe de bate");
            c.Check(PlayerAnimLogic.AttackTrigger(WeaponType.Knife) == PlayerAnimator.StabKnife, "Cuchillo: puñalada");
        }

        private static void VerifyPlayerModelImports(Checker c)
        {
            var model = (ModelImporter)AssetImporter.GetAtPath(PlayerModelFactory.ModelPath);
            c.Check(model.animationType == ModelImporterAnimationType.Human, "Player_Origin se importa como Humanoid");
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(PlayerModelFactory.ModelPath).OfType<Avatar>().FirstOrDefault();
            c.Check(avatar != null && avatar.isValid && avatar.isHuman, "Player_Origin tiene un avatar humanoide válido");

            foreach (string file in PlayerModelFactory.AllClips)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(PlayerModelFactory.ClipPath(file));
                bool loop = PlayerModelFactory.LoopingClips.Contains(file);
                c.Check(importer.animationType == ModelImporterAnimationType.Human, $"{file}: Humanoid");
                c.Check(importer.clipAnimations.Length > 0 && importer.clipAnimations.All(a =>
                        a.loopTime == loop && a.lockRootRotation && a.lockRootHeightY && !a.lockRootPositionXZ),
                    $"{file}: {(loop ? "en bucle" : "una vez")} y en el sitio");
                AnimationClip clip = PlayerModelFactory.LoadClip(file);
                c.Check(clip != null && clip.isHumanMotion, $"{file}: clip humanoide");
            }
        }

        private static void VerifyPlayerController(Checker c)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerModelFactory.ControllerPath);
            c.NotNull(controller, "Falta el Animator Controller del jugador");
            if (controller == null) return;
            c.Check(PlayerModelFactory.ControllerIsComplete(controller), "El Animator Controller tiene todos sus estados y parámetros");
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            c.Check(sm.defaultState != null && sm.defaultState.name == PlayerAnimator.LocomotionState, "Empieza en Locomotion");
            var tree = sm.defaultState?.motion as BlendTree;
            c.Check(tree != null && tree.children.Length == 6 && tree.children.All(ch => ch.motion != null),
                "Locomotion mezcla quieto, correr, correr de espaldas y correr con arma");
            foreach (string attack in new[] { PlayerAnimator.Punch, PlayerAnimator.SwingBat, PlayerAnimator.StabKnife })
            {
                AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == attack);
                var clip = state?.motion as AnimationClip;
                c.Check(clip != null && Mathf.Abs(clip.length / state.speed - PlayerModelFactory.AttackDuration) < 0.01f,
                    $"{attack}: dura {PlayerModelFactory.AttackDuration} s en pantalla");
                c.Check(state != null && state.transitions.Any(t => t.destinationState == sm.defaultState && t.hasExitTime),
                    $"{attack}: vuelve a Locomotion al terminar");
            }
            c.Check(sm.anyStateTransitions.Any(t => t.destinationState?.name == "Death" &&
                    t.conditions.Any(x => x.parameter == PlayerAnimator.DeadParam && x.mode == AnimatorConditionMode.If)),
                "Al morir pasa a la animación de muerte");
        }

        private static void VerifyPlayerModelPrefab(Checker c)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            Transform model = prefab != null ? prefab.transform.Find(PlayerModelFactory.ModelName) : null;
            c.NotNull(model, "El prefab Player tiene el modelo 3D");
            if (model == null) return;

            var animator = model.GetComponent<Animator>();
            c.Check(animator != null && animator.runtimeAnimatorController != null && animator.avatar != null && animator.avatar.isHuman,
                "El modelo tiene Animator con controller y avatar humanoide");
            c.Check(animator != null && !animator.applyRootMotion, "El Animator no mueve al jugador (lo mueve el CharacterController)");

            var anim = prefab.GetComponent<PlayerAnimator>();
            c.Check(anim != null && anim.Animator == animator, "PlayerAnimator apunta al Animator del modelo");
            foreach (string field in new[] { "controller", "life", "weapons", "melee", "finisher" })
                c.Check(anim != null && BuilderUtil.GetRef(anim, field) != null, $"PlayerAnimator.{field} asignado");

            c.Check(!prefab.transform.Find("Body").GetComponent<Renderer>().enabled && !prefab.transform.Find("Body/Nose").GetComponent<Renderer>().enabled,
                "La cápsula del jugador ya no se ve");
            c.Check(anim != null && anim.RunClipSpeed > 1f && anim.BackClipSpeed > 0.5f && anim.GunRunClipSpeed > 1f,
                $"Se midió a qué velocidad avanza cada clip de correr ({anim?.RunClipSpeed:F1} / {anim?.BackClipSpeed:F1} / {anim?.GunRunClipSpeed:F1} m/s)");
            float runStep = anim != null ? PlayerAnimLogic.StepSpeed(prefab.GetComponent<PlayerController>().MoveSpeed, anim.RunClipSpeed) : 0f;
            c.Check(runStep > 0.6f && runStep < 1.8f, $"A velocidad normal los pasos no patinan ni se saturan (x{runStep:F2})");

            var material = AssetDatabase.LoadAssetAtPath<Material>(PlayerModelFactory.MaterialPath);
            c.Check(material != null && material.IsKeywordEnabled("_EMISSION") && material.GetTexture("_EmissionMap") != null &&
                    material.GetColor("_EmissionColor").maxColorComponent > 0.2f &&
                    (material.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive) != 0,
                "El material del modelo tiene luz propia para que se vean sus detalles");
            Light light = prefab.transform.Find(PlayerModelFactory.LightName)?.GetComponent<Light>();
            c.Check(light != null && light.type == LightType.Point && light.shadows == LightShadows.None && light.range > 2f &&
                    light.transform.localPosition.y > 2f, "El jugador lleva una luz suave encima, sin sombras");

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            c.Check(renderers.All(r => r.sharedMaterials.All(m => m == material)), "El modelo usa M_PlayerModel");
            c.Check(renderers.Length > 0 && renderers.All(r => r.enabled && r.sharedMaterials.All(m => m != null)), "El modelo se ve y tiene materiales");
        }

        /// <summary>Simulación: el modelo de pie mide como la cápsula, corre en el sitio y al morir queda en el suelo.</summary>
        private static void VerifyPlayerPoseSimulation(Checker c)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            var player = (GameObject)Object.Instantiate(prefab, new Vector3(500f, 0f, 500f), Quaternion.identity);
            try
            {
                var animator = player.transform.Find(PlayerModelFactory.ModelName).GetComponent<Animator>();
                animator.Rebind();
                animator.Update(0.2f);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                float headHeight = head.position.y - player.transform.position.y;
                c.Check(headHeight > 1.6f && headHeight < 1.9f, $"De pie, la cabeza queda a la altura de la cápsula ({headHeight:F2} m)");

                Vector3 shoulders = animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position -
                                    animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                shoulders.y = 0f;
                c.Check(Vector3.Angle(shoulders, player.transform.right) < 30f,
                    $"El modelo mira hacia donde apunta el jugador ({Vector3.Angle(shoulders, player.transform.right):F0} grados)");

                Vector3 standingHips = hips.position;
                animator.SetFloat(PlayerAnimator.MoveParam, 1f);
                float maxDrift = 0f;
                for (int i = 0; i < 30; i++)
                {
                    animator.Update(0.05f);
                    Vector3 d = hips.position - player.transform.position;
                    d.y = 0f;
                    maxDrift = Mathf.Max(maxDrift, d.magnitude);
                }
                c.Check(maxDrift < 0.45f, $"Corre en el sitio, sin alejarse del jugador ({maxDrift:F2} m)");
                c.Check(Vector3.Distance(hips.position, standingHips) > 0.01f || maxDrift > 0.01f, "La animación de correr mueve el cuerpo");

                animator.SetFloat(PlayerAnimator.MoveParam, 0f);
                animator.SetBool(PlayerAnimator.DeadParam, true);
                for (int i = 0; i < 60; i++) animator.Update(0.05f);
                float deadHead = head.position.y - player.transform.position.y;
                c.Check(deadHead < 0.6f, $"Al morir el modelo cae al suelo (cabeza a {deadHead:F2} m)");

                animator.SetBool(PlayerAnimator.DeadParam, false);
                animator.Play(PlayerAnimator.LocomotionState, 0, 0f);
                animator.Update(0f);
                animator.Update(0.1f);
                float revivedHead = head.position.y - player.transform.position.y;
                c.Check(revivedHead > 1.3f, $"Al rebobinar vuelve a estar de pie al instante ({revivedHead:F2} m)");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
