using UnityEngine;

namespace Overdrive.Core
{
    /// <summary>
    /// Punto único de lectura de controles (Input Manager antiguo).
    /// Si se cambia el mapeo de teclas, solo hay que tocar este archivo.
    /// </summary>
    public static class GameInput
    {
        /// <summary>Con el juego en pausa, todos los controles de juego devuelven "nada pulsado".</summary>
        public static bool Blocked { get; set; }

        /// <summary>Esc o P: abre o cierra el menú de pausa (no se bloquea).</summary>
        public static bool PausePressed => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);

        /// <summary>WASD en ejes del mundo: x = A/D, y = S/W. Sin normalizar.</summary>
        public static Vector2 Move
        {
            get
            {
                if (Blocked) return Vector2.zero;
                float x = 0f, y = 0f;
                if (Input.GetKey(KeyCode.A)) x -= 1f;
                if (Input.GetKey(KeyCode.D)) x += 1f;
                if (Input.GetKey(KeyCode.S)) y -= 1f;
                if (Input.GetKey(KeyCode.W)) y += 1f;
                return new Vector2(x, y);
            }
        }

        public static Vector3 MousePosition => Input.mousePosition;

        /// <summary>Shift: la cámara se adelanta más para mirar lejos.</summary>
        public static bool LookFar => !Blocked && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        /// <summary>Ctrl: caminar agachado, más lento y sin hacer ruido.</summary>
        public static bool Sneak => !Blocked && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));

        public static bool RestartPressed => !Blocked && Input.GetKeyDown(KeyCode.R);
        public static bool RewindPressed => !Blocked && Input.GetKeyDown(KeyCode.T);
        public static bool AttackPressed => !Blocked && Input.GetMouseButtonDown(0);
        public static bool AttackHeld => !Blocked && Input.GetMouseButton(0);
        public static bool ThrowPressed => !Blocked && Input.GetMouseButtonDown(1);
        public static bool FinishPressed => !Blocked && Input.GetKeyDown(KeyCode.Space);
        public static bool PickupPressed => !Blocked && Input.GetKeyDown(KeyCode.E);
    }
}
