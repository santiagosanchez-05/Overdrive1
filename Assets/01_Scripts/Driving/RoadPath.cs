using UnityEngine;

namespace Overdrive.Driving
{
    /// <summary>
    /// Eje de la carretera (puntos en el mundo, de la salida a la meta) y su ancho.
    /// Sirve para saber cuánto se ha avanzado, dónde aparecen los enemigos y hacia dónde va la calzada.
    /// </summary>
    public class RoadPath : MonoBehaviour
    {
        [SerializeField] private Vector3[] points = new Vector3[0];
        [SerializeField] private float width = 22f;

        private float[] cumulative;

        public Vector3[] Points => points;
        public float Width => width;
        public float Length
        {
            get
            {
                EnsureCumulative();
                return cumulative.Length > 0 ? cumulative[cumulative.Length - 1] : 0f;
            }
        }

        public void Set(Vector3[] newPoints, float newWidth)
        {
            points = newPoints;
            width = newWidth;
            cumulative = null;
        }

        private void EnsureCumulative()
        {
            if (cumulative != null && cumulative.Length == points.Length) return;
            cumulative = new float[points.Length];
            for (int i = 1; i < points.Length; i++) cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }

        /// <summary>Punto del eje a una distancia desde la salida.</summary>
        public Vector3 PointAt(float distance)
        {
            EnsureCumulative();
            if (points.Length == 0) return transform.position;
            distance = Mathf.Clamp(distance, 0f, Length);
            int i = Index(distance);
            float segment = cumulative[i + 1] - cumulative[i];
            float t = segment > 0f ? (distance - cumulative[i]) / segment : 0f;
            return Vector3.Lerp(points[i], points[i + 1], t);
        }

        /// <summary>Dirección de la calzada (hacia la meta) a una distancia.</summary>
        public Vector3 TangentAt(float distance)
        {
            EnsureCumulative();
            if (points.Length < 2) return Vector3.forward;
            int i = Index(Mathf.Clamp(distance, 0f, Length));
            Vector3 d = points[i + 1] - points[i];
            d.y = 0f;
            return d.sqrMagnitude > 0f ? d.normalized : Vector3.forward;
        }

        /// <summary>Lado derecho de la calzada a una distancia.</summary>
        public Vector3 RightAt(float distance) => Vector3.Cross(Vector3.up, TangentAt(distance));

        /// <summary>Distancia recorrida (proyección sobre el eje) y desplazamiento lateral de un punto.</summary>
        public float Project(Vector3 position, out float lateral)
        {
            EnsureCumulative();
            lateral = 0f;
            if (points.Length < 2) return 0f;
            float best = float.MaxValue, along = 0f;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i], ab = points[i + 1] - a;
                ab.y = 0f;
                Vector3 ap = position - a;
                ap.y = 0f;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
                Vector3 closest = a + ab * t;
                Vector3 off = position - closest;
                off.y = 0f;
                float d = off.sqrMagnitude;
                if (d >= best) continue;
                best = d;
                along = cumulative[i] + ab.magnitude * t;
                lateral = Vector3.Dot(off, Vector3.Cross(Vector3.up, ab.normalized));
            }
            return along;
        }

        private int Index(float distance)
        {
            int lo = 0, hi = points.Length - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (cumulative[mid] <= distance) lo = mid;
                else hi = mid - 1;
            }
            return Mathf.Clamp(lo, 0, Mathf.Max(0, points.Length - 2));
        }
    }
}
