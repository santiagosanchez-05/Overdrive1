using System.Linq;
using Overdrive.Enemies;
using Overdrive.Story;
using Overdrive.Weapons;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Modelos de los personajes de Jhael (02_Modelos/Characters) para los enemigos y para Nina.
    /// Se importan como Humanoid, así las animaciones de Mixamo se comparten entre todos (en el sitio).
    /// Si un modelo no tiene esqueleto humanoide se usa quieto, sin animar.
    /// </summary>
    internal static class CharacterFactory
    {
        public static string CharactersPath => AssetFactory.NumberedFolder("Modelos", "02_Modelos") + "/Characters";
        public static string AnimationsPath => PlayerModelFactory.AnimationsPath;

        /// <summary>Altura del modelo en su pose de referencia, la misma que el jugador (~1.8 m de pie en reposo).</summary>
        public const float TargetBindHeight = 2.2f * PlayerModelFactory.CharacterScale;
        public static readonly Color Emission = new Color(0.35f, 0.35f, 0.35f);

        // ---- Enemigos: un modelo por arma ----

        /// <summary>Modelo de cada tipo de enemigo, en el orden de <see cref="EnemyVisualLogic.ModelIndex"/>.</summary>
        public static readonly (WeaponType weapon, string name, string file)[] EnemyModels =
        {
            (WeaponType.Bat, "Enmascarado", "Enemy_Mask_Character.fbx"),
            (WeaponType.Knife, "Chino", "EnemyChino_Character.fbx"),
            (WeaponType.Shotgun, "Gordo", "EnemyFat_Character.fbx"),
            (WeaponType.Smg, "Raul", "Ral_Ale.fbx"),
        };

        public const string EnemyIdle = "Enemy_Mask_Character@Standing Idle.fbx";
        public const string EnemyWalk = "Enemy_Mask_Character@Standing Walk Forward.fbx";
        public const string EnemySwing = "Enemy_Mask_Character@AttackBate.fbx";
        public const string EnemyDown = "Enemy_Mask_Character@Zombie Dying.fbx";
        public static string EnemyRun => PlayerModelFactory.ClipRun;
        public static string EnemyStab => PlayerModelFactory.ClipKnife;
        public static readonly string[] EnemyLoopClips = { EnemyIdle, EnemyWalk };
        public static readonly string[] EnemyOwnClips = { EnemyIdle, EnemyWalk, EnemySwing, EnemyDown };
        /// <summary>Duración del golpe en pantalla (s): el enemigo tarda 0.3 s en pegar y luego termina el gesto.</summary>
        public const float EnemyAttackDuration = 0.6f;

        public static string EnemyControllerPath => AnimationsPath + "/Enemy.controller";
        public static string ModelPath(string file) => CharactersPath + "/" + file;
        public static string ModelChildName(WeaponType weapon) => "Model_" + weapon;

        // ---- Nina ----

        public const string NinaFile = "Female_Character.fbx";
        public const string NinaIdle = "Female_Character@Idle.fbx";
        public const string NinaTalking = "Female_Character@Talking.fbx";
        public static string NinaControllerPath => AnimationsPath + "/Nina.controller";
        public static string NinaPrefabPath => AssetFactory.PrefabsPath + "/Nina.prefab";

        public static bool Exists(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        public static string ClipPath(string file) => AnimationsPath + "/" + file;

        public static AnimationClip LoadClip(string file) =>
            AssetDatabase.LoadAllAssetsAtPath(ClipPath(file)).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        public static Avatar LoadAvatar(string modelPath) =>
            AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault(a => a.isValid && a.isHuman);

        public static void EnsureImports()
        {
            foreach (var (_, _, file) in EnemyModels)
                if (Exists(ModelPath(file))) PlayerModelFactory.EnsureHumanoid(ModelPath(file), false, false);
            if (Exists(ModelPath(NinaFile))) PlayerModelFactory.EnsureHumanoid(ModelPath(NinaFile), false, false);
            foreach (string clip in EnemyOwnClips)
                if (Exists(ClipPath(clip))) PlayerModelFactory.EnsureHumanoid(ClipPath(clip), true, EnemyLoopClips.Contains(clip));
            foreach (string clip in new[] { NinaIdle, NinaTalking })
                if (Exists(ClipPath(clip))) PlayerModelFactory.EnsureHumanoid(ClipPath(clip), true, true);
        }

        // ---- Animator Controllers (solo se recrean si faltan o están incompletos, para no ensuciar git) ----

        public static AnimatorController EnsureEnemyController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
            if (existing != null && EnemyControllerComplete(existing)) return existing;
            if (existing != null) AssetDatabase.DeleteAsset(EnemyControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(EnemyControllerPath);
            controller.AddParameter(EnemyVisual.SpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(EnemyVisual.DownParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(EnemyVisual.SwingParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(EnemyVisual.StabParam, AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // Quieto (0), andando de patrulla (0.5), corriendo detrás del jugador (1).
            var tree = new BlendTree
            {
                name = EnemyVisual.LocomotionState,
                blendType = BlendTreeType.Simple1D,
                blendParameter = EnemyVisual.SpeedParam,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(LoadClip(EnemyIdle), 0f);
            tree.AddChild(LoadClip(EnemyWalk), 0.5f);
            tree.AddChild(PlayerModelFactory.LoadClip(EnemyRun), 1f);
            AnimatorState locomotion = sm.AddState(EnemyVisual.LocomotionState, new Vector3(300f, 0f));
            locomotion.motion = tree;
            sm.defaultState = locomotion;

            AnimatorState down = sm.AddState("Down", new Vector3(300f, 200f));
            down.motion = LoadClip(EnemyDown);
            AnimatorStateTransition toDown = sm.AddAnyStateTransition(down);
            toDown.AddCondition(AnimatorConditionMode.If, 0f, EnemyVisual.DownParam);
            toDown.canTransitionToSelf = false;
            toDown.duration = 0.08f;
            AnimatorStateTransition up = down.AddTransition(locomotion);
            up.AddCondition(AnimatorConditionMode.IfNot, 0f, EnemyVisual.DownParam);
            up.duration = 0f;

            AddAttack(sm, locomotion, EnemyVisual.SwingParam, LoadClip(EnemySwing), EnemyAttackDuration, EnemyVisual.DownParam, new Vector3(600f, -60f));
            AddAttack(sm, locomotion, EnemyVisual.StabParam, PlayerModelFactory.LoadClip(EnemyStab), EnemyAttackDuration, EnemyVisual.DownParam, new Vector3(600f, 60f));

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void AddAttack(AnimatorStateMachine sm, AnimatorState back, string trigger, AnimationClip clip, float duration, string blockedBy, Vector3 position)
        {
            AnimatorState state = sm.AddState(trigger, position);
            state.motion = clip;
            state.speed = clip != null ? clip.length / duration : 1f;
            AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, blockedBy);
            enter.duration = 0.05f;
            AnimatorStateTransition exit = state.AddTransition(back);
            exit.hasExitTime = true;
            exit.exitTime = 0.85f;
            exit.duration = 0.1f;
        }

        public static bool EnemyControllerComplete(AnimatorController controller)
        {
            string[] parameters = { EnemyVisual.SpeedParam, EnemyVisual.DownParam, EnemyVisual.SwingParam, EnemyVisual.StabParam };
            if (controller.layers.Length != 1 || !parameters.All(p => controller.parameters.Any(x => x.name == p))) return false;
            var states = controller.layers[0].stateMachine.states;
            string[] names = { EnemyVisual.LocomotionState, "Down", EnemyVisual.SwingParam, EnemyVisual.StabParam };
            return names.All(n => states.Any(s => s.state.name == n && s.state.motion != null)) &&
                   states.First(s => s.state.name == EnemyVisual.LocomotionState).state.motion is BlendTree tree &&
                   tree.children.Length == 3 && tree.children.All(ch => ch.motion != null);
        }

        public static AnimatorController EnsureNinaController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(NinaControllerPath);
            if (existing != null && NinaControllerComplete(existing)) return existing;
            if (existing != null) AssetDatabase.DeleteAsset(NinaControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(NinaControllerPath);
            controller.AddParameter(NpcDialogue.TalkingParam, AnimatorControllerParameterType.Bool);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idle = sm.AddState("Idle", new Vector3(300f, 0f));
            idle.motion = LoadClip(NinaIdle);
            sm.defaultState = idle;
            AnimatorState talking = sm.AddState("Talking", new Vector3(300f, 150f));
            talking.motion = LoadClip(NinaTalking);
            AnimatorStateTransition start = idle.AddTransition(talking);
            start.AddCondition(AnimatorConditionMode.If, 0f, NpcDialogue.TalkingParam);
            start.duration = 0.25f;
            AnimatorStateTransition stop = talking.AddTransition(idle);
            stop.AddCondition(AnimatorConditionMode.IfNot, 0f, NpcDialogue.TalkingParam);
            stop.duration = 0.35f;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        public static bool NinaControllerComplete(AnimatorController controller) =>
            controller.parameters.Any(p => p.name == NpcDialogue.TalkingParam) &&
            new[] { "Idle", "Talking" }.All(n => controller.layers[0].stateMachine.states.Any(s => s.state.name == n && s.state.motion != null));

        // ---- Modelos dentro de los prefabs ----

        /// <summary>Altura del hueso de la cabeza de pie, la misma que la del jugador (~1.65 m).</summary>
        public const float TargetHeadHeight = 1.65f * PlayerModelFactory.CharacterScale;

        /// <summary>
        /// Escala para que el personaje mida como el jugador: por la altura del hueso de la cabeza en su pose humanoide
        /// (la caja de la malla engaña con pelo, accesorios o brazos abiertos). Sin esqueleto, por la altura de la malla.
        /// </summary>
        public static float ScaleFor(string modelPath)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var temp = (GameObject)Object.Instantiate(source, new Vector3(0f, -800f, 0f), Quaternion.identity);
            try
            {
                Avatar avatar = LoadAvatar(modelPath);
                if (avatar != null)
                {
                    if (!temp.TryGetComponent(out Animator animator)) animator = temp.AddComponent<Animator>();
                    animator.avatar = avatar;
                    animator.runtimeAnimatorController = null;
                    animator.Rebind();
                    animator.Update(0f);
                    Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                    float height = head != null ? head.position.y - temp.transform.position.y : 0f;
                    if (height > 0.05f) return TargetHeadHeight / height;
                }
                Renderer[] renderers = temp.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return 1f;
                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                return b.size.y > 0.01f ? TargetBindHeight / b.size.y : 1f;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        /// <summary>Material del personaje en 04_Materials: su textura con algo de luz propia para que se vea en el nivel oscuro.</summary>
        public static Material EnsureMaterial(string name, GameObject source)
        {
            string path = AssetFactory.MaterialsPath + "/M_" + name + ".mat";
            Texture texture = source.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials).Where(m => m != null)
                .Select(m => m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture).FirstOrDefault(t => t != null);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", texture);
            mat.SetTexture("_MainTex", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.3f);
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", texture);
            mat.SetColor("_EmissionColor", Emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Instancia (o reutiliza) un modelo como hijo, a escala, con su material y, si es humanoide, su Animator.</summary>
        public static GameObject EnsureModelChild(Transform parent, string childName, string modelPath, string materialName,
            RuntimeAnimatorController controller)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Transform existing = parent.Find(childName);
            if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) != source)
            {
                Object.DestroyImmediate(existing.gameObject);
                existing = null;
            }
            GameObject model = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            model.name = childName;
            BuilderUtil.SetLocal(model, Vector3.zero, Vector3.one * ScaleFor(modelPath), Quaternion.identity);

            Material material = EnsureMaterial(materialName, source);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }

            Avatar avatar = LoadAvatar(modelPath);
            if (avatar != null)
            {
                var animator = BuilderUtil.EnsureComponent<Animator>(model);
                animator.runtimeAnimatorController = controller;
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            else if (model.TryGetComponent(out Animator unused))
            {
                Object.DestroyImmediate(unused);
            }
            return model;
        }

        /// <summary>Monta los cuatro modelos en el prefab Enemy y el componente que elige cuál se ve según el arma.</summary>
        public static void EnsureEnemyModels(GameObject root, Renderer bodyRenderer, Renderer noseRenderer)
        {
            if (!EnemyModels.All(m => Exists(ModelPath(m.file)))) return;
            AnimatorController controller = EnsureEnemyController();
            var models = new GameObject[EnemyModels.Length];
            foreach (var (weapon, name, file) in EnemyModels)
            {
                GameObject model = EnsureModelChild(root.transform, ModelChildName(weapon), ModelPath(file), "Enemigo" + name, controller);
                models[EnemyVisualLogic.ModelIndex(weapon)] = model;
                model.SetActive(weapon == WeaponType.Bat);
            }

            var visual = BuilderUtil.EnsureComponent<EnemyVisual>(root);
            var so = new SerializedObject(visual);
            so.FindProperty("enemy").objectReferenceValue = root.GetComponent<Enemy>();
            so.FindProperty("ai").objectReferenceValue = root.GetComponent<EnemyAI>();
            SerializedProperty modelsProp = so.FindProperty("models");
            modelsProp.arraySize = models.Length;
            for (int i = 0; i < models.Length; i++) modelsProp.GetArrayElementAtIndex(i).objectReferenceValue = models[i];
            SerializedProperty capsule = so.FindProperty("capsuleRenderers");
            capsule.arraySize = 2;
            capsule.GetArrayElementAtIndex(0).objectReferenceValue = bodyRenderer;
            capsule.GetArrayElementAtIndex(1).objectReferenceValue = noseRenderer;
            so.ApplyModifiedPropertiesWithoutUndo();

            // En el editor se ve el modelo del bate; al jugar cada enemigo enciende el de su arma.
            bodyRenderer.enabled = false;
            noseRenderer.enabled = false;
        }

        /// <summary>Prefab de Nina: modelo animado, colisión, obstáculo para la IA, diálogo y una luz rosa suave encima.</summary>
        public static GameObject EnsureNinaPrefab()
        {
            if (!Exists(ModelPath(NinaFile))) return null;
            AnimatorController controller = EnsureNinaController();
            return BuilderUtil.EditPrefab(NinaPrefabPath, "Nina", root =>
            {
                var collider = BuilderUtil.EnsureComponent<CapsuleCollider>(root);
                collider.center = new Vector3(0f, 0.9f, 0f);
                collider.height = 1.8f;
                collider.radius = 0.35f;

                var obstacle = BuilderUtil.EnsureComponent<UnityEngine.AI.NavMeshObstacle>(root);
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
                obstacle.center = new Vector3(0f, 0.9f, 0f);
                obstacle.height = 1.8f;
                obstacle.radius = 0.4f;
                obstacle.carving = false; // sin recortar el NavMesh: los enemigos la esquivan pero nunca corta una ruta

                GameObject model = EnsureModelChild(root.transform, "Model", ModelPath(NinaFile), "Nina", controller);

                GameObject lightGo = BuilderUtil.EnsureChild(root.transform, "NinaLight");
                BuilderUtil.SetLocal(lightGo, new Vector3(0f, 2.5f, 0.4f), Vector3.one, Quaternion.identity);
                var light = BuilderUtil.EnsureComponent<Light>(lightGo);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.6f, 0.85f);
                light.intensity = 1.4f;
                light.range = 3.4f;
                light.shadows = LightShadows.None;

                var dialogue = BuilderUtil.EnsureComponent<NpcDialogue>(root);
                BuilderUtil.SetRef(dialogue, "animator", model.GetComponent<Animator>());
            });
        }
    }
}
