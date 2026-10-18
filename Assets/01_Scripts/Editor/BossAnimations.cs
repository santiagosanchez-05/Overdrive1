using System.Linq;
using Overdrive.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Animator del Jefe con sus animaciones de 08_Animations/Jefe2_Animaciones:
    /// Entrada al empezar, Apuntando al atacar, Muerte al caer y Dancing cuando mata al jugador.
    /// Para moverse reutiliza las de los enemigos (quieto, andar, correr). Mismos parámetros que Enemy.controller,
    /// así EnemyVisual lo maneja igual que a cualquier enemigo.
    /// </summary>
    internal static class BossAnimations
    {
        public const string Folder = "Assets/08_Animations/Jefe2_Animaciones";
        public const string ControllerPath = Folder + "/Boss.controller";
        public const string Aim = "Jefe_2Apuntando.fbx";
        public const string Dance = "Jefe_2Dancing.fbx";
        public const string Intro = "Jefe_2Entrada.fbx";
        public const string Death = "Jefe_2Muerte.fbx";

        public const string IntroState = "Entrada";
        public const float AttackDuration = 0.8f;

        private static string ClipPath(string file) => Folder + "/" + file;

        private static AnimationClip Clip(string file) =>
            AssetDatabase.LoadAllAssetsAtPath(ClipPath(file)).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        /// <summary>Importa los clips como humanoides (para usarlos con el modelo del Jefe) y crea el controller.</summary>
        public static AnimatorController EnsureController()
        {
            PlayerModelFactory.EnsureHumanoid(ClipPath(Intro), true, false);
            PlayerModelFactory.EnsureHumanoid(ClipPath(Aim), true, false);
            PlayerModelFactory.EnsureHumanoid(ClipPath(Death), true, false);
            PlayerModelFactory.EnsureHumanoid(ClipPath(Dance), true, true);
            CharacterFactory.EnsureImports(); // clips de andar/correr de los enemigos

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(EnemyVisual.SpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(EnemyVisual.DownParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(EnemyVisual.SwingParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(EnemyVisual.StabParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(BossEnemy.DanceParam, AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // Moverse: quieto (0), andar (0.5), correr (1).
            var tree = new BlendTree
            {
                name = EnemyVisual.LocomotionState,
                blendType = BlendTreeType.Simple1D,
                blendParameter = EnemyVisual.SpeedParam,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(CharacterFactory.LoadClip(CharacterFactory.EnemyIdle), 0f);
            tree.AddChild(CharacterFactory.LoadClip(CharacterFactory.EnemyWalk), 0.5f);
            tree.AddChild(PlayerModelFactory.LoadClip(CharacterFactory.EnemyRun), 1f);
            AnimatorState locomotion = sm.AddState(EnemyVisual.LocomotionState, new Vector3(300f, 0f));
            locomotion.motion = tree;

            // Entrada: la primera vez que aparece; luego pasa a moverse (o antes, si le atacan).
            AnimatorState intro = sm.AddState(IntroState, new Vector3(0f, 0f));
            intro.motion = Clip(Intro);
            sm.defaultState = intro;
            AnimatorStateTransition introDone = intro.AddTransition(locomotion);
            introDone.hasExitTime = true;
            introDone.exitTime = 0.95f;
            introDone.duration = 0.15f;
            AnimatorStateTransition introMoving = intro.AddTransition(locomotion);
            introMoving.AddCondition(AnimatorConditionMode.Greater, 0.3f, EnemyVisual.SpeedParam);
            introMoving.duration = 0.2f;

            // Muerte.
            AnimatorState death = sm.AddState("Down", new Vector3(300f, 220f));
            death.motion = Clip(Death);
            AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, EnemyVisual.DownParam);
            toDeath.canTransitionToSelf = false;
            toDeath.duration = 0.08f;
            AnimatorStateTransition revive = death.AddTransition(locomotion); // al rebobinar
            revive.AddCondition(AnimatorConditionMode.IfNot, 0f, EnemyVisual.DownParam);
            revive.duration = 0f;

            // Ataque: apunta y dispara con cualquiera de los dos disparadores de EnemyVisual.
            AnimationClip aim = Clip(Aim);
            AnimatorState attack = sm.AddState("Apuntando", new Vector3(600f, 0f));
            attack.motion = aim;
            attack.speed = aim != null ? aim.length / AttackDuration : 1f;
            foreach (string trigger in new[] { EnemyVisual.SwingParam, EnemyVisual.StabParam })
            {
                AnimatorStateTransition enter = sm.AddAnyStateTransition(attack);
                enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                enter.AddCondition(AnimatorConditionMode.IfNot, 0f, EnemyVisual.DownParam);
                enter.canTransitionToSelf = false;
                enter.duration = 0.05f;
            }
            AnimatorStateTransition attackDone = attack.AddTransition(locomotion);
            attackDone.hasExitTime = true;
            attackDone.exitTime = 0.9f;
            attackDone.duration = 0.1f;

            // Baile de victoria cuando el jugador muere.
            AnimatorState dance = sm.AddState("Dancing", new Vector3(600f, 220f));
            dance.motion = Clip(Dance);
            AnimatorStateTransition toDance = sm.AddAnyStateTransition(dance);
            toDance.AddCondition(AnimatorConditionMode.If, 0f, BossEnemy.DanceParam);
            toDance.AddCondition(AnimatorConditionMode.IfNot, 0f, EnemyVisual.DownParam);
            toDance.canTransitionToSelf = false;
            toDance.duration = 0.2f;

            foreach (var (file, clip) in new[] { (Intro, intro.motion), (Aim, (Motion)aim), (Death, death.motion), (Dance, dance.motion) })
                if (clip == null) Debug.LogWarning("[Jefe] No se encontró la animación " + ClipPath(file));

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }
    }
}
