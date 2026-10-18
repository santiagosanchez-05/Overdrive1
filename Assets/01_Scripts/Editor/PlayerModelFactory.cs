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
        /// <summary>Escala del modelo: la cabeza a la altura de la antigua cápsula (1.8 m), que desde arriba se lea igual de grande.</summary>
        public const float ModelScale = 2.0f;
        public const string MaterialName = "PlayerModel";
        public const string LightName = "PlayerLight";
        /// <summary>Brillo propio de la textura: el nivel es muy oscuro y sin esto no se ven los detalles del personaje.</summary>
        public static readonly Color ModelEmission = new Color(0.42f, 0.42f, 0.42f);

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
        public static string TexturePath => AssetFactory.PrefabsPath + "/Player_basecolor.PNG";
        public static string MaterialPath => AssetFactory.MaterialsPath + "/M_" + MaterialName + ".mat";

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
        internal static void EnsureHumanoid(string path, bool animation, bool loop)
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
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = PlayerAnimator.StepSpeedParam, type = AnimatorControllerParameterType.Float, defaultFloat = 1f,
            });

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
            locomotion.speedParameter = PlayerAnimator.StepSpeedParam; // los pasos van al ritmo real del jugador
            locomotion.speedParameterActive = true;
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
                PlayerAnimator.Punch, PlayerAnimator.SwingBat, PlayerAnimator.StabKnife, PlayerAnimator.StepSpeedParam,
            };
            if (controller.layers.Length != 1 || !parameters.All(p => controller.parameters.Any(x => x.name == p))) return false;
            ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
            string[] names = { PlayerAnimator.LocomotionState, "Death", PlayerAnimator.Punch, PlayerAnimator.SwingBat, PlayerAnimator.StabKnife };
            return names.All(n => states.Any(s => s.state.name == n && s.state.motion != null)) &&
                   states.Any(s => s.state.name == PlayerAnimator.LocomotionState && s.state.speedParameterActive &&
                                   s.state.speedParameter == PlayerAnimator.StepSpeedParam);
        }

        /// <summary>Material del modelo: su textura, poco brillo especular y algo de luz propia para que se vea en la oscuridad.</summary>
        public static Material EnsureMaterial()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
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
            if (created) AssetDatabase.CreateAsset(mat, MaterialPath);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Velocidad (m/s) a la que avanza de verdad cada animación de correr con el modelo a su escala:
        /// se reproduce con root motion en una copia temporal y se mide cuánto se desplaza.
        /// </summary>
        public static (float run, float back, float gun) MeasureStepSpeeds(AnimatorController controller)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var temp = (GameObject)Object.Instantiate(source, new Vector3(0f, -500f, 0f), Quaternion.identity);
            try
            {
                temp.transform.localScale = Vector3.one * ModelScale;
                if (!temp.TryGetComponent(out Animator animator)) animator = temp.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
                animator.applyRootMotion = true;
                float Measure(float move, float armed)
                {
                    temp.transform.SetPositionAndRotation(new Vector3(0f, -500f, 0f), Quaternion.identity);
                    animator.Rebind();
                    animator.SetFloat(PlayerAnimator.MoveParam, move);
                    animator.SetFloat(PlayerAnimator.ArmedParam, armed);
                    animator.SetFloat(PlayerAnimator.StepSpeedParam, 1f);
                    for (int i = 0; i < 10; i++) animator.Update(0.02f);
                    Vector3 start = temp.transform.position;
                    for (int i = 0; i < 100; i++) animator.Update(0.02f);
                    Vector3 delta = temp.transform.position - start;
                    delta.y = 0f;
                    return delta.magnitude / 2f;
                }
                return (Measure(1f, 0f), Measure(-1f, 0f), Measure(1f, 1f));
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
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

            Material material = EnsureMaterial();
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }

            // La cápsula y la nariz siguen existiendo (muerte, puño, rebobinar), pero ya no se ven.
            body.GetComponent<Renderer>().enabled = false;
            nose.GetComponent<Renderer>().enabled = false;

            // Luz suave encima del jugador: lo separa del suelo oscuro y deja ver sus detalles.
            GameObject lightGo = BuilderUtil.EnsureChild(root.transform, LightName);
            BuilderUtil.SetLocal(lightGo, new Vector3(0f, 2.6f, 0.4f), Vector3.one, Quaternion.identity);
            var light = BuilderUtil.EnsureComponent<Light>(lightGo);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.93f, 0.85f);
            light.intensity = 1.6f;
            light.range = 3.6f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;

            var (run, back, gun) = MeasureStepSpeeds(controller);
            var anim = BuilderUtil.EnsureComponent<PlayerAnimator>(root);
            var so = new SerializedObject(anim);
            so.FindProperty("runClipSpeed").floatValue = run;
            so.FindProperty("backClipSpeed").floatValue = back;
            so.FindProperty("gunRunClipSpeed").floatValue = gun;
            so.ApplyModifiedPropertiesWithoutUndo();
            BuilderUtil.SetRef(anim, "animator", animator);
            BuilderUtil.SetRef(anim, "controller", root.GetComponent<PlayerController>());
            BuilderUtil.SetRef(anim, "life", root.GetComponent<PlayerLife>());
            BuilderUtil.SetRef(anim, "weapons", root.GetComponent<PlayerWeapons>());
            BuilderUtil.SetRef(anim, "melee", root.GetComponent<PlayerMelee>());
            BuilderUtil.SetRef(anim, "finisher", root.GetComponent<PlayerFinisher>());
        }
    }
}
