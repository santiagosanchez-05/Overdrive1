using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>Zona de salida: rectángulo en el suelo. Se ilumina cuando ya no quedan enemigos.</summary>
    public class ExitZone : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new Vector2(3f, 3f);
        [SerializeField] private Renderer zoneRenderer;
        [SerializeField] private Material lockedMaterial;
        [SerializeField] private Material openMaterial;

        public Vector2 Size => size;
        public Renderer ZoneRenderer => zoneRenderer;
        public Material LockedMaterial => lockedMaterial;
        public Material OpenMaterial => openMaterial;
        public bool IsOpen { get; private set; }

        public bool Contains(Vector3 point) => Contains(transform.position, size, point);

        /// <summary>¿Está el punto dentro del rectángulo (ancho x, fondo z) centrado en <paramref name="center"/>?</summary>
        public static bool Contains(Vector3 center, Vector2 size, Vector3 point)
        {
            return Mathf.Abs(point.x - center.x) <= size.x * 0.5f && Mathf.Abs(point.z - center.z) <= size.y * 0.5f
                   && Mathf.Abs(point.y - center.y) <= 1.5f;
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (zoneRenderer == null) return;
            Material material = open ? openMaterial : lockedMaterial;
            if (material != null && zoneRenderer.sharedMaterial != material) zoneRenderer.sharedMaterial = material;
        }
    }
}
