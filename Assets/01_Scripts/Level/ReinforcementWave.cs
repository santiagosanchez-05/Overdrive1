using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Player;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Refuerzos: esperan desactivados tras una puerta cerrada. Al activarse, la puerta se abre
    /// y entran a cazar al jugador.
    /// </summary>
    public class ReinforcementWave : MonoBehaviour, IRewindable
    {
        [SerializeField] private EnemyAI[] squad = new EnemyAI[0];
        [SerializeField] private GameObject door;

        public EnemyAI[] Squad => squad;
        public GameObject Door => door;
        public bool Triggered { get; private set; }

        /// <summary>Guarda si ya entraron; cada refuerzo restaura su propio estado (activo, posición...).</summary>
        public object Capture() => Triggered;

        public void Restore(object snapshot)
        {
            Triggered = (bool)snapshot;
            if (door == null) return;
            var dissolve = door.GetComponent<DoorDissolve>();
            if (dissolve == null) door.SetActive(!Triggered);
            else if (Triggered) dissolve.SetOpenImmediate();
            else dissolve.ResetClosed();
        }

        public void Trigger(PlayerLife target)
        {
            if (Triggered) return;
            Triggered = true;
            if (door != null)
            {
                // Con DoorDissolve la puerta se disuelve (y deja pasar al instante); si no, desaparece.
                var dissolve = door.GetComponent<DoorDissolve>();
                if (dissolve != null) dissolve.Play();
                else door.SetActive(false);
            }
            foreach (EnemyAI ai in squad)
            {
                if (ai == null) continue;
                ai.gameObject.SetActive(true);
                ai.Hunt(target);
            }
        }
    }
}
