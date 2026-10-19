using UnityEngine;
using UnityEngine.UI;

namespace Overdrive.UI
{
    /// <summary>Movimiento ambiental suave; sigue vivo con el juego pausado y no modifica el material compartido.</summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class NeonMenuAtmosphere : MonoBehaviour
    {
        private static readonly int ClockId = Shader.PropertyToID("_NeonTime");
        private Graphic graphic;
        private Material original, instance;
        public float Clock { get; private set; }

        private void OnEnable()
        {
            graphic = GetComponent<Graphic>();
            original = graphic.material;
            if (original == null) return;
            instance = new Material(original) { name = original.name + " (Runtime)" };
            graphic.material = instance;
        }

        private void Update()
        {
            Clock = Time.unscaledTime;
            if (instance != null) instance.SetFloat(ClockId, Clock);
        }

        private void OnDisable()
        {
            if (graphic != null) graphic.material = original;
            if (instance != null)
            {
                if (Application.isPlaying) Destroy(instance);
                else DestroyImmediate(instance);
            }
            instance = null;
        }
    }
}
