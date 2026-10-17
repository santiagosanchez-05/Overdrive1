using UnityEngine;

namespace Overdrive.Core
{
    /// <summary>
    /// Punto único de lectura de controles (Input Manager antiguo).
    /// Si se cambia el mapeo de teclas, solo hay que tocar este archivo.
    /// </summary>
    public static class GameInput
    {
        /// <summary>WASD en ejes del mundo: x = A/D, y = S/W. Sin normalizar.</summary>
        public static Vector2 Move
        {
            get
            {
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
        public static bool LookFar => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        /// <summary>Ctrl: caminar agachado, más lento y sin hacer ruido.</summary>
        public static bool Sneak => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        public static bool RestartPressed => Input.GetKeyDown(KeyCode.R);
        public static bool RewindPressed => Input.GetKeyDown(KeyCode.T);
        public static bool AttackPressed => Input.GetMouseButtonDown(0);
        public static bool AttackHeld => Input.GetMouseButton(0);
        public static bool ThrowPressed => Input.GetMouseButtonDown(1);
        public static bool FinishPressed => Input.GetKeyDown(KeyCode.Space);
        public static bool PickupPressed => Input.GetKeyDown(KeyCode.E);
    }
}
