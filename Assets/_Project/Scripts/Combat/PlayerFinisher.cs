using System.Collections.Generic;
using Overdrive.CameraRig;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Player;
using UnityEngine;

namespace Overdrive.Combat
{
    /// <summary>
    /// Remate con Espacio sobre un enemigo derribado cercano. Durante el remate el jugador
    /// queda inmóvil y vulnerable; si muere, el remate se cancela.
    /// </summary>
    public class PlayerFinisher : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerLife life;
        [SerializeField] private float radius = 1.6f;
        [SerializeField] private float duration = 0.6f;
        [SerializeField] private int hits = 3;

        private readonly List<MeleeTargeting.Target> targets = new List<MeleeTargeting.Target>();
        private FinisherSequence sequence;
        private Enemy target;

        public PlayerController Controller => controller;
        public PlayerLife Life => life;
        public Enemy Target => target;
        public bool IsFinishing => sequence != null && sequence.Active;
        public float Duration => duration;

        private FinisherSequence Sequence => sequence ??= new FinisherSequence(duration, hits);

        private void Update()
        {
            if (life != null && life.IsDead)
            {
                Cancel();
                return;
            }
            if (IsFinishing) Advance(Time.deltaTime);
            else if (GameInput.FinishPressed) TryStart(Enemy.All);
        }

        public bool TryStart(IReadOnlyList<Enemy> candidates)
        {
            if (IsFinishing) return false;
            Enemy.CollectTargets(candidates, targets);
            int index = MeleeTargeting.FindFinishTarget(transform.position, targets, radius);
            if (index < 0) return false;

            Enemy enemy = candidates[index];
            if (!enemy.BeginFinish()) return false;

            target = enemy;
            Sequence.Start();
            if (controller != null) controller.MovementLocked = true;
            MoveOnto(enemy.transform.position);
            return true;
        }

        /// <summary>Avanza el remate. Público para simularlo en el editor.</summary>
        public void Advance(float deltaTime)
        {
            if (!IsFinishing) return;
            FinisherSequence.Step step = Sequence.Tick(deltaTime);
            for (int i = 0; i < step.NewHits; i++) CameraShake.Shake(0.2f);
            if (!step.Completed) return;

            Enemy finished = target;
            Release();
            if (finished != null) finished.CompleteFinish();
        }

        /// <summary>Rebobinar: olvida el remate en curso sin tocar al enemigo (que restaura su propio estado).</summary>
        public void ResetForRewind()
        {
            sequence?.Cancel();
            target = null;
        }

        public void Cancel()
        {
            if (!IsFinishing) return;
            Sequence.Cancel();
            if (target != null) target.CancelFinish();
            Release();
        }

        private void Release()
        {
            target = null;
            if (controller != null) controller.MovementLocked = false;
        }

        private void MoveOnto(Vector3 position)
        {
            position.y = transform.position.y;
            CharacterController cc = controller != null ? controller.Controller : null;
            if (cc != null) cc.enabled = false;
            transform.position = position;
            if (cc != null) cc.enabled = true;
        }
    }
}
