using System.Collections.Generic;
using System.IO;
using System.Linq;
using Overdrive.Enemies;
using Overdrive.Level;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Importa y configura los modelos 3D y animaciones de Enemy_Mask_Character (enemigo que ataca con cuchillo/daga o bate)
    /// y Female_Character (personaje de ambiente que no ataca).
    /// </summary>
    internal static class CharacterModelFactory
    {
        public static string CharactersPath => AssetFactory.NumberedFolder("Modelos", "02_Modelos") + "/Characters";
        public static string AnimationsPath => PlayerModelFactory.AnimationsPath;

        // --- Enemy_Mask_Character ---
        public static string EnemyModelPath => CharactersPath + "/Enemy_Mask_Character.fbx";
        public static string EnemyTexturePath => CharactersPath + "/Enemy_Mask_basecolor.PNG";
        public static string EnemyMaterialPath => AssetFactory.MaterialsPath + "/M_EnemyMaskModel.mat";
        public static string EnemyControllerPath => AnimationsPath + "/EnemyMask.controller";
        public const string EnemyModelName = "EnemyModel";
        public const float EnemyModelScale = 2.0f;
        public const float AttackDuration = 0.35f;

        public const string ClipEnemyIdle = "Enemy_Mask_Character@Standing Idle.fbx";
        public const string ClipEnemyWalk = "Enemy_Mask_Character@Standing Walk Forward.fbx";
        public const string ClipEnemyKnife = "Player@Attack Daga.fbx";
        public const string ClipEnemyBat = "Enemy_Mask_Character@AttackBate.fbx";
        public const string ClipEnemyDeath = "Enemy_Mask_Character@Zombie Dying.fbx";

        public static readonly string[] EnemyClips =
        {
            ClipEnemyIdle, ClipEnemyWalk, ClipEnemyKnife, ClipEnemyBat, ClipEnemyDeath,
        };

        private static readonly HashSet<string> LoopingClips = new HashSet<string>
        {
            ClipEnemyIdle, ClipEnemyWalk, ClipFemaleIdle, ClipFemaleTalking,
        };

        // --- Female_Character ---
        public static string FemaleModelPath => CharactersPath + "/Female_Character.fbx";
        public static string FemaleTexturePath => CharactersPath + "/Female_Character_basecolor.PNG";
        public static string FemaleMaterialPath => AssetFactory.MaterialsPath + "/M_FemaleModel.mat";
        public static string FemaleControllerPath => AnimationsPath + "/Female.controller";
        public static string FemalePrefabPath => AssetFactory.PrefabsPath + "/FemaleCharacter.prefab";
        public const string FemaleModelName = "FemaleModel";
        public const float FemaleModelScale = 2.0f;

        public const string ClipFemaleIdle = "Female_Character@Idle.fbx";
        public const string ClipFemaleTalking = "Female_Character@Talking.fbx";

        public static readonly string[] FemaleClips =
        {
            ClipFemaleIdle, ClipFemaleTalking,
        };

        private static readonly Color ModelEmission = new Color(0.28f, 0.28f, 0.32f);

        public static string ClipPath(string file) => $"{AnimationsPath}/{file}";

        public static bool EnemyModelAvailable =>
            File.Exists(EnemyModelPath) && EnemyClips.All(f => File.Exists(ClipPath(f)));

        public static bool FemaleModelAvailable =>
            File.Exists(FemaleModelPath) && FemaleClips.All(f => File.Exists(ClipPath(f)));

        public static AnimationClip LoadClip(string file) =>
            AssetDatabase.LoadAllAssetsAtPath(ClipPath(file))
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        public static void EnsureImports()
        {
            if (EnemyModelAvailable)
            {
                EnsureHumanoid(EnemyModelPath, false, false);
                foreach (string file in EnemyClips) EnsureHumanoid(ClipPath(file), true, LoopingClips.Contains(file));
            }
            if (FemaleModelAvailable)
            {
                EnsureHumanoid(FemaleModelPath, false, false);
                foreach (string file in FemaleClips) EnsureHumanoid(ClipPath(file), true, LoopingClips.Contains(file));
            }
        }

        private static void EnsureHumanoid(string path, bool animation, bool loop)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;
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
                    clip.lockRootRotation = true;
                    clip.keepOriginalOrientation = true;
                    clip.lockRootHeightY = true;
                    clip.keepOriginalPositionY = true;
                    clip.lockRootPositionXZ = false;
                    dirty = true;
                }
                if (dirty) importer.clipAnimations = clips;
            }

            if (dirty) importer.SaveAndReimport();
        }

        public static AnimatorController EnsureEnemyController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
            if (existing != null && EnemyControllerIsComplete(existing)) return existing;
            if (existing != null) AssetDatabase.DeleteAsset(EnemyControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(EnemyControllerPath);
            controller.AddParameter(EnemyAnimator.MoveParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(EnemyAnimator.DownParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(EnemyAnimator.StabKnife, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(EnemyAnimator.SwingBat, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = EnemyAnimator.StepSpeedParam, type = AnimatorControllerParameterType.Float, defaultFloat = 1f,
            });

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            var tree = new BlendTree
            {
                name = EnemyAnimator.LocomotionState,
                blendType = BlendTreeType.Simple1D,
                blendParameter = EnemyAnimator.MoveParam,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(LoadClip(ClipEnemyIdle), 0f);
            tree.AddChild(LoadClip(ClipEnemyWalk), 1f);

            AnimatorState locomotion = sm.AddState(EnemyAnimator.LocomotionState, new Vector3(300f, 0f));
            locomotion.motion = tree;
            locomotion.speedParameter = EnemyAnimator.StepSpeedParam;
            locomotion.speedParameterActive = true;
            sm.defaultState = locomotion;

            AnimatorState death = sm.AddState("Death", new Vector3(300f, 200f));
            death.motion = LoadClip(ClipEnemyDeath);
            AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, EnemyAnimator.DownParam);
            toDeath.canTransitionToSelf = false;
            toDeath.duration = 0.08f;
            AnimatorStateTransition revive = death.AddTransition(locomotion);
            revive.AddCondition(AnimatorConditionMode.IfNot, 0f, EnemyAnimator.DownParam);
            revive.duration = 0.05f;

            AddEnemyAttack(sm, locomotion, EnemyAnimator.StabKnife, ClipEnemyKnife, new Vector3(600f, -60f));
            AddEnemyAttack(sm, locomotion, EnemyAnimator.SwingBat, ClipEnemyBat, new Vector3(600f, 60f));

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void AddEnemyAttack(AnimatorStateMachine sm, AnimatorState locomotion, string trigger, string file, Vector3 position)
        {
            AnimationClip clip = LoadClip(file);
            AnimatorState state = sm.AddState(trigger, position);
            state.motion = clip;
            state.speed = clip != null ? clip.length / AttackDuration : 1f;

            AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, EnemyAnimator.DownParam);
            enter.canTransitionToSelf = true;
            enter.duration = 0.05f;

            AnimatorStateTransition exit = state.AddTransition(locomotion);
            exit.hasExitTime = true;
            exit.exitTime = 0.85f;
            exit.duration = 0.1f;
        }

        public static bool EnemyControllerIsComplete(AnimatorController controller)
        {
            string[] parameters =
            {
                EnemyAnimator.MoveParam, EnemyAnimator.DownParam, EnemyAnimator.StabKnife,
                EnemyAnimator.SwingBat, EnemyAnimator.StepSpeedParam,
            };
            if (controller.layers.Length != 1 || !parameters.All(p => controller.parameters.Any(x => x.name == p))) return false;
            ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
            string[] names = { EnemyAnimator.LocomotionState, "Death", EnemyAnimator.StabKnife, EnemyAnimator.SwingBat };
            return names.All(n => states.Any(s => s.state.name == n && s.state.motion != null));
        }

        public static AnimatorController EnsureFemaleController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(FemaleControllerPath);
            if (existing != null && FemaleControllerIsComplete(existing)) return existing;
            if (existing != null) AssetDatabase.DeleteAsset(FemaleControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(FemaleControllerPath);
            controller.AddParameter(FemaleNpcAnimator.TalkingParam, AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idle = sm.AddState(FemaleNpcAnimator.IdleState, new Vector3(300f, 0f));
            idle.motion = LoadClip(ClipFemaleIdle);
            sm.defaultState = idle;

            AnimatorState talking = sm.AddState(FemaleNpcAnimator.TalkingState, new Vector3(300f, 160f));
            talking.motion = LoadClip(ClipFemaleTalking);

            AnimatorStateTransition toTalking = idle.AddTransition(talking);
            toTalking.AddCondition(AnimatorConditionMode.If, 0f, FemaleNpcAnimator.TalkingParam);
            toTalking.hasExitTime = false;
            toTalking.duration = 0.2f;

            AnimatorStateTransition toIdle = talking.AddTransition(idle);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, FemaleNpcAnimator.TalkingParam);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.2f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        public static bool FemaleControllerIsComplete(AnimatorController controller)
        {
            if (controller.layers.Length != 1 || !controller.parameters.Any(p => p.name == FemaleNpcAnimator.TalkingParam)) return false;
            ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
            return states.Any(s => s.state.name == FemaleNpcAnimator.IdleState && s.state.motion != null) &&
                   states.Any(s => s.state.name == FemaleNpcAnimator.TalkingState && s.state.motion != null);
        }

        public static Material EnsureMaterial(string materialPath, string texturePath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool created = mat == null;
            if (created) mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", texture);
            mat.SetTexture("_MainTex", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.35f);
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", texture);
            mat.SetColor("_EmissionColor", ModelEmission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (created) AssetDatabase.CreateAsset(mat, materialPath);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void EnsureEnemyModel(GameObject root, GameObject body, GameObject nose)
        {
            if (!EnemyModelAvailable) return;
            EnsureImports();
            AnimatorController controller = EnsureEnemyController();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyModelPath);

            Transform existing = root.transform.Find(EnemyModelName);
            if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) != source)
            {
                Object.DestroyImmediate(existing.gameObject);
                existing = null;
            }
            GameObject model = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            model.name = EnemyModelName;
            BuilderUtil.SetLocal(model, Vector3.zero, Vector3.one * EnemyModelScale, Quaternion.identity);

            var animator = BuilderUtil.EnsureComponent<Animator>(model);
            animator.runtimeAnimatorController = controller;
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(EnemyModelPath).OfType<Avatar>().FirstOrDefault();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;

            Material material = EnsureMaterial(EnemyMaterialPath, EnemyTexturePath);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }

            body.GetComponent<Renderer>().enabled = false;
            nose.GetComponent<Renderer>().enabled = false;

            var anim = BuilderUtil.EnsureComponent<EnemyAnimator>(root);
            BuilderUtil.SetRef(anim, "animator", animator);
            BuilderUtil.SetRef(anim, "enemy", root.GetComponent<Enemy>());
            BuilderUtil.SetRef(anim, "ai", root.GetComponent<EnemyAI>());
        }

        public static GameObject EnsureFemalePrefab()
        {
            if (!FemaleModelAvailable) return null;
            EnsureImports();
            BuilderUtil.EnsureFolder(AssetFactory.PrefabsPath);
            AnimatorController controller = EnsureFemaleController();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(FemaleModelPath);
            Material material = EnsureMaterial(FemaleMaterialPath, FemaleTexturePath);

            return BuilderUtil.EditPrefab(FemalePrefabPath, "FemaleCharacter", root =>
            {
                Transform existing = root.transform.Find(FemaleModelName);
                if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) != source)
                {
                    Object.DestroyImmediate(existing.gameObject);
                    existing = null;
                }
                GameObject model = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                model.name = FemaleModelName;
                BuilderUtil.SetLocal(model, Vector3.zero, Vector3.one * FemaleModelScale, Quaternion.identity);

                var animator = BuilderUtil.EnsureComponent<Animator>(model);
                animator.runtimeAnimatorController = controller;
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(FemaleModelPath).OfType<Avatar>().FirstOrDefault();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;

                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
                }

                var npc = BuilderUtil.EnsureComponent<FemaleNpcAnimator>(root);
                BuilderUtil.SetRef(npc, "animator", animator);
            });
        }
    }
}
