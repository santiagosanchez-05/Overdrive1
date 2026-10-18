using Overdrive.CameraRig;
using Overdrive.Enemies;
using Overdrive.Player;
using TMPro;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>Estado del ascensor (lógica pura, comprobable en el editor).</summary>
    public static class ElevatorLogic
    {
        /// <summary>Guardias que quedan en pie (vivos) de la lista.</summary>
        public static int Remaining(Enemy[] guards)
        {
            int n = 0;
            if (guards == null) return 0;
            foreach (Enemy e in guards)
                if (e != null && !e.IsDead) n++;
            return n;
        }

        public static string Label(int remaining) =>
            remaining > 0 ? $"ASCENSOR CERRADO · quedan {remaining}" : "ASCENSOR AL ÁTICO";
    }

    /// <summary>
    /// Ascensor del loft al ático del Jefe (nivel 3). Se abre cuando no queda ningún guardia del loft; al pisarlo, la
    /// pantalla funde a negro y el jugador aparece en la entrada del ático, con la cámara más alejada (sala grande).
    /// </summary>
    public class ArenaElevator : MonoBehaviour
    {
        [SerializeField] private PlayerLife player;
        [SerializeField] private Enemy[] guards = new Enemy[0];
        [SerializeField] private Transform arenaStart;
        [SerializeField] private TopDownCamera cameraRig;
        [SerializeField] private float arenaCameraDistance = 15f;
        [SerializeField] private float radius = 1.1f;
        [SerializeField] private GameObject openBeam;
        [SerializeField] private TextMeshPro label;
        [SerializeField] private float fadeTime = 0.35f;

        private float fade;
        private int phase; // 0 esperando, 1 fundiendo a negro, 2 volviendo
        public bool Used { get; private set; }
        public bool IsOpen => ElevatorLogic.Remaining(guards) == 0;
        public Transform ArenaStart => arenaStart;
        public Enemy[] Guards => guards;
        public float Radius => radius;

        private void Update()
        {
            bool open = IsOpen;
            if (openBeam != null && openBeam.activeSelf != (open && !Used)) openBeam.SetActive(open && !Used);
            if (label != null) label.text = Used ? "" : ElevatorLogic.Label(ElevatorLogic.Remaining(guards));

            if (phase == 1)
            {
                fade += Time.unscaledDeltaTime / fadeTime;
                if (fade >= 1f)
                {
                    Teleport();
                    phase = 2;
                }
            }
            else if (phase == 2)
            {
                fade -= Time.unscaledDeltaTime / fadeTime;
                if (fade <= 0f) { fade = 0f; phase = 0; }
            }
            else if (!Used && open && player != null && !player.IsDead &&
                     Vector3.Distance(Flat(player.transform.position), Flat(transform.position)) < radius)
            {
                phase = 1;
                Used = true;
            }
        }

        /// <summary>Lleva al jugador al ático. Público para las pruebas.</summary>
        public void Teleport()
        {
            Used = true;
            if (player == null || arenaStart == null) return;
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(arenaStart.position, arenaStart.rotation);
            if (controller != null) controller.enabled = true;
            if (cameraRig != null)
            {
                cameraRig.Distance = arenaCameraDistance;
                cameraRig.SnapToTarget();
            }
            CameraShake.Shake(0.2f);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private void OnGUI()
        {
            if (fade <= 0f) return;
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(fade));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
