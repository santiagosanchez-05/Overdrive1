using Overdrive.Enemies;
using Overdrive.Player;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Refuerzos: esperan desactivados tras una puerta cerrada. Al activarse, la puerta se abre
    /// y entran a cazar al jugador.
    /// </summary>
    public class ReinforcementWave : MonoBehaviour
    {
        [SerializeField] private EnemyAI[] squad = new EnemyAI[0];
        [SerializeField] private GameObject door;

        public EnemyAI[] Squad => squad;
        public GameObject Door => door;
        public bool Triggered { get; private set; }

        public void Trigger(PlayerLife target)
        {
            if (Triggered) return;
            Triggered = true;
            if (door != null) door.SetActive(false);
            foreach (EnemyAI ai in squad)
            {
                if (ai == null) continue;
                ai.gameObject.SetActive(true);
                ai.Hunt(target);
            }
        }
    }
}
