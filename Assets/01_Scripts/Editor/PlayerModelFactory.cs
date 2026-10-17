using System.Linq;
using Overdrive.Combat;
using Overdrive.Player;
using Overdrive.Weapons;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Modelo 3D del jugador (Player_Origin, rig de Mixamo) con sus animaciones de 08_Animations.
    /// Importa el modelo y las animaciones como Humanoid (en el sitio: el movimiento lo da el CharacterController),
    /// genera el Animator Controller y monta el modelo dentro del prefab Player, ocultando la cápsula.
    /// </summary>
    internal static class PlayerModelFactory
    {
        public const string ModelName = "Model";
        public const float ModelScale = 1.6f;

        public const string ClipIdle = "Player@Warrior Idle.fbx";
        public const string ClipRun = "Player@Running.fbx";
        public const string ClipRunBack = "Player@Run Backwards.fbx";
        public const string ClipRunGun = "PlayerRunRevolver.fbx";
        public const string ClipPunch = "Player@Boxing.fbx";
        public const string ClipBat = "Player@AttackBate.fbx";
        public const string ClipKnife = "Player@Attack Daga.fbx";
        public const string ClipDeath = "Player@Falling Back Death.fbx";

        /// <summary>Clips que se repiten en bucle (el resto se reproduce una vez).</summary>
        public static readonly string[] LoopingClips = { ClipIdle, ClipRun, ClipRunBack, ClipRunGun };
        public static readonly string[] AllClips = { ClipIdle, ClipRun, ClipRunBack, ClipRunGun, ClipPunch, ClipBat, ClipKnife, ClipDeath };

        /// <summary>Duración en pantalla de cada ataque (s): los clips de Mixamo son largos para un golpe de un solo clic.</summary>
        public const float AttackDuration = 0.45f;

        public static string ModelPath => AssetFactory.PrefabsPath + "/Player_Origin.fbx";
        public static string AnimationsPath => AssetFactory.NumberedFolder("Animations", "08_Animations");
        public static string ControllerPath => AnimationsPath + "/Player.controller";
        public static string ClipPath(string file) => AnimationsPath + "/" + file;

        public static bool ModelAvailable => AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) != null &&
                                             AllClips.All(f => AssetDatabase.LoadAssetAtPath<GameObject>(ClipPath(f)) != null);

        public static AnimationClip LoadClip(string file) =>
            AssetDatabase.LoadAllAssetsAtPath(ClipPath(file)).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        public static void EnsureImports()
        {
            if (!ModelAvailable)
            {
                Debug.LogWarning("[PlayerModelFactory] Falta el modelo o alguna animación del jugador: se usa la cápsula.");
                return;
            }
            EnsureHumanoid(ModelPath, false, false);
            foreach (string file in AllClips) EnsureHumanoid(ClipPath(file), true, LoopingClips.Contains(file));
        }

        /// <summary>Humanoid con avatar propio; las animaciones quedan en el sitio y a la altura original.</summary>
        private static void EnsureHumanoid(string path, bool animation, bool loop)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human) { importer.animationType = ModelImporterAnimationType.Human; dirty = true; }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
            if (importer.importAnimation != animation) { importer.importAnimation = animation; dirty = true; }

            if (animation)
            {
                ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    bool ok = clip.loopTime == loop && clip.lockRootRotation && clip.keepOriginalOrientation &&
                              clip.lockRootHeightY && clip.keepOriginalPositionY && !clip.lockRootPositionXZ;
                    if (ok && importer.clipAnimations.Length > 0) continue;
                    clip.loopTime = loop;
                    clip.lockRootRotation = true;        // no gira el cuerpo: lo orienta PlayerAim
                    clip.keepOriginalOrientation = true;
                    clip.lockRootHeightY = true;         // la altura (y la caída al morir) va en la pose
                    clip.keepOriginalPositionY = true;
                    clip.lockRootPositionXZ = false;     // el avance es root motion y se descarta: corre en el sitio
                    dirty = true;
                }
                if (dirty) importer.clipAnimations = clips;
            }

            if (dirty) importer.SaveAndReimport();
        }

        /// <summary>Crea el Animator Controller si falta o no está completo (si ya está bien no se toca, para no ensuciar git).</summary>
        public static AnimatorController EnsureController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null && ControllerIsComplete(existing)) return existing;
            if (existing != null) AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(PlayerAnimator.MoveParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(PlayerAnimator.ArmedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(PlayerAnimator.DeadParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(PlayerAnimator.Punch, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(PlayerAnimator.SwingBat, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(PlayerAnimator.StabKnife, AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // Correr: X = Move (-1 de espaldas, 0 quieto, 1 de frente), Y = con arma de fuego.
            var tree = new BlendTree
            {
                name = PlayerAnimator.LocomotionState,
                blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = PlayerAnimator.MoveParam,
                blendParameterY = PlayerAnimator.ArmedParam,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(LoadClip(ClipIdle), new Vector2(0f, 0f));
            tree.AddChild(LoadClip(ClipRun), new Vector2(1f, 0f));
            tree.AddChild(LoadClip(ClipRunBack), new Vector2(-1f, 0f));
            tree.AddChild(LoadClip(ClipIdle), new Vector2(0f, 1f));
            tree.AddChild(LoadClip(ClipRunGun), new Vector2(1f, 1f));
            tree.AddChild(LoadClip(ClipRunBack), new Vector2(-1f, 1f));

            AnimatorState locomotion = sm.AddState(PlayerAnimator.LocomotionState, new Vector3(300f, 0f));
            locomotion.motion = tree;
            sm.defaultState = locomotion;

            AnimatorState death = sm.AddState("Death", new Vector3(300f, 200f));
            death.motion = LoadClip(ClipDeath);
            AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, PlayerAnimator.DeadParam);
            toDeath.canTransitionToSelf = false;
            toDeath.duration = 0.1f;
            AnimatorStateTransition revive = death.AddTransition(locomotion);
            revive.AddCondition(AnimatorConditionMode.IfNot, 0f, PlayerAnimator.DeadParam);
            revive.duration = 0f;

            AddAttack(sm, locomotion, PlayerAnimator.Punch, ClipPunch, new Vector3(600f, -100f));
            AddAttack(sm, locomotion, PlayerAnimator.SwingBat, ClipBat, new Vector3(600f, 0f));
            AddAttack(sm, locomotion, PlayerAnimator.StabKnife, ClipKnife, new Vector3(600f, 100f));

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void AddAttack(AnimatorStateMachine sm, AnimatorState locomotion, string trigger, string file, Vector3 position)
        {
            AnimationClip clip = LoadClip(file);
            AnimatorState state = sm.AddState(trigger, position);
            state.motion = clip;
            state.speed = clip != null ? clip.length / AttackDuration : 1f;

            AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, PlayerAnimator.DeadParam);
            enter.canTransitionToSelf = true;
            enter.duration = 0.05f;

            AnimatorStateTransition exit = state.AddTransition(locomotion);
            exit.hasExitTime = true;
            exit.exitTime = 0.85f;
            exit.duration = 0.1f;
        }

        public static bool ControllerIsComplete(AnimatorController controller)
        {
            string[] parameters =
            {
                PlayerAnimator.MoveParam, PlayerAnimator.ArmedParam, PlayerAnimator.DeadParam,
                PlayerAnimator.Punch, PlayerAnimator.SwingBat, PlayerAnimator.StabKnife,
            };
            if (controller.layers.Length != 1 || !parameters.All(p => controller.parameters.Any(x => x.name == p))) return false;
            ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
            string[] names = { PlayerAnimator.LocomotionState, "Death", PlayerAnimator.Punch, PlayerAnimator.SwingBat, PlayerAnimator.StabKnife };
            return names.All(n => states.Any(s => s.state.name == n && s.state.motion != null));
        }

        /// <summary>Monta el modelo animado dentro del prefab y oculta la cápsula (que sigue para la lógica de muerte y el puño).</summary>
        public static void EnsureModel(GameObject root, GameObject body, GameObject nose)
        {
            if (!ModelAvailable) return;
            AnimatorController controller = EnsureController();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

            Transform existing = root.transform.Find(ModelName);
            if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) != source)
            {
                Object.DestroyImmediate(existing.gameObject);
                existing = null;
            }
            GameObject model = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            model.name = ModelName;
            BuilderUtil.SetLocal(model, Vector3.zero, Vector3.one * ModelScale, Quaternion.identity);

            var animator = BuilderUtil.EnsureComponent<Animator>(model);
            animator.runtimeAnimatorController = controller;
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }

            // La cápsula y la nariz siguen existiendo (muerte, puño, rebobinar), pero ya no se ven.
            body.GetComponent<Renderer>().enabled = false;
            nose.GetComponent<Renderer>().enabled = false;

            var anim = BuilderUtil.EnsureComponent<PlayerAnimator>(root);
            BuilderUtil.SetRef(anim, "animator", animator);
            BuilderUtil.SetRef(anim, "controller", root.GetComponent<PlayerController>());
            BuilderUtil.SetRef(anim, "life", root.GetComponent<PlayerLife>());
            BuilderUtil.SetRef(anim, "weapons", root.GetComponent<PlayerWeapons>());
            BuilderUtil.SetRef(anim, "melee", root.GetComponent<PlayerMelee>());
            BuilderUtil.SetRef(anim, "finisher", root.GetComponent<PlayerFinisher>());
        }
    }
}
