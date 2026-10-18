using UnityEngine;

namespace Overdrive.Combat
{
    /// <summary>
    /// Zonas de impacto de un personaje (jugador o enemigo) que siguen su esqueleto en cada fotograma:
    /// cabeza (esfera sobre el hueso Head), torso (cápsula de la cadera al cuello) y piernas (cápsula de la
    /// cadera a los pies). Así acompañan a las animaciones (agacharse, inclinarse, caminar) y su tamaño es
    /// proporcional al personaje (también en niveles con otra escala). Sin esqueleto humanoide se usa una
    /// cápsula vertical de los pies al cuello.
    /// Son triggers (no empujan ni bloquean nada) en la capa "Ignore Raycast": las balas, las armas lanzadas
    /// y la mira los buscan expresamente (<see cref="IsHitbox"/>); las sondas del suelo, la visión y el
    /// movimiento no los ven.
    /// Se crean solas: <see cref="Ensure"/> desde el jugador y los enemigos.
    /// </summary>
    public class HeadHitbox : MonoBehaviour
    {
        public const string ObjectName = "HeadHitbox";
        public const string BodyName = "BodyHitbox";
        public const string LegsName = "LegsHitbox";

        private Transform owner;
        private Animator animator;
        private Transform hips, neck, head, leftFoot, rightFoot;
        private SphereCollider sphere;
        private CapsuleCollider body;
        private CapsuleCollider legs;
        private float boneSearch;

        /// <summary>Personaje al que pertenece (raíz del jugador o del enemigo).</summary>
        public Transform Owner => owner;

        public static HeadHitbox Ensure(Transform character)
        {
            Transform existing = character.Find(ObjectName);
            if (existing != null && existing.TryGetComponent(out HeadHitbox found)) return found;
            int layer = LayerMask.NameToLayer("Ignore Raycast");

            GameObject Part(string name)
            {
                var go = new GameObject(name);
                if (layer >= 0) go.layer = layer;
                go.transform.SetParent(character, false);
                return go;
            }

            var headGo = Part(ObjectName);
            var box = headGo.AddComponent<HeadHitbox>();
            box.owner = character;
            box.sphere = headGo.AddComponent<SphereCollider>();
            box.sphere.isTrigger = true;
            box.body = Capsule(Part(BodyName));
            box.legs = Capsule(Part(LegsName));

            // Que los colliders propios del personaje no choquen con sus zonas de impacto.
            foreach (Collider c in character.GetComponents<Collider>())
            {
                Physics.IgnoreCollision(c, box.sphere);
                Physics.IgnoreCollision(c, box.body);
                Physics.IgnoreCollision(c, box.legs);
            }
            box.Follow();
            return box;
        }

        private static CapsuleCollider Capsule(GameObject go)
        {
            var c = go.AddComponent<CapsuleCollider>();
            c.direction = 1; // eje Y local
            c.isTrigger = true;
            return c;
        }

        /// <summary>¿Es una zona de impacto (cabeza, torso o piernas) de un personaje?</summary>
        public static bool IsHitbox(Collider c) =>
            c != null && c.isTrigger && (c.name == ObjectName || c.name == BodyName || c.name == LegsName);

        /// <summary>¿Es la zona de la cabeza?</summary>
        public static bool IsHead(Collider c) => c != null && c.isTrigger && c.name == ObjectName;

        /// <summary>Activa o desactiva las zonas (tumbado o muerto no se le puede disparar así).</summary>
        public void SetActive(bool active)
        {
            if (sphere != null) sphere.enabled = active;
            if (body != null) body.enabled = active;
            if (legs != null) legs.enabled = active;
        }

        private void LateUpdate() => Follow();

        private void Follow()
        {
            if (owner == null) owner = transform.parent;
            if (owner == null) return;
            if (head == null || !head.gameObject.activeInHierarchy) FindBones();

            float scale = Mathf.Max(0.01f, Mathf.Abs(owner.lossyScale.y));
            if (hips != null && neck != null && head != null)
            {
                Vector3 h = hips.position, n = neck.position, hd = head.position;
                float torso = Mathf.Max(0.1f, Vector3.Distance(h, n));
                float bodyRadius = Mathf.Clamp(torso * 0.34f, 0.08f, 0.5f);
                float headRadius = Mathf.Clamp(torso * 0.24f, 0.06f, 0.35f);

                // Cabeza: un poco por encima del hueso Head (que está en la base del cráneo).
                Vector3 up = (hd - n).sqrMagnitude > 1e-6f ? (hd - n).normalized : Vector3.up;
                transform.position = hd + up * headRadius * 0.55f;
                sphere.radius = headRadius / scale;

                Place(body, h, n, bodyRadius, scale, capsInside: true); // de la cadera al cuello, sin tapar la cabeza
                Vector3 feet = leftFoot != null && rightFoot != null ? (leftFoot.position + rightFoot.position) * 0.5f
                    : new Vector3(h.x, owner.position.y, h.z);
                // Piernas abiertas: la cápsula abarca de un pie al otro.
                float stance = leftFoot != null && rightFoot != null
                    ? Vector3.ProjectOnPlane(leftFoot.position - rightFoot.position, Vector3.up).magnitude * 0.5f : 0f;
                Place(legs, h, feet, Mathf.Max(bodyRadius * 0.85f, stance + bodyRadius * 0.35f), scale);
                legs.enabled = sphere.enabled;
            }
            else
            {
                // Sin esqueleto: cápsula vertical de los pies al cuello y cabeza encima.
                Vector3 headPos = owner.position + Vector3.up * 1.7f * scale;
                transform.position = headPos;
                sphere.radius = 0.22f;
                float neckH = Mathf.Max(0.8f * scale, headPos.y - owner.position.y - 0.2f * scale);
                Place(body, owner.position, owner.position + Vector3.up * neckH, 0.32f * scale, scale);
                legs.enabled = false;
            }
        }

        /// <summary>Coloca una cápsula entre dos puntos del mundo con el radio dado (también en mundo).</summary>
        private static void Place(CapsuleCollider capsule, Vector3 a, Vector3 b, float radius, float scale, bool capsInside = false)
        {
            Vector3 axis = b - a;
            float length = axis.magnitude;
            Transform t = capsule.transform;
            t.position = (a + b) * 0.5f;
            t.rotation = length > 1e-4f ? Quaternion.FromToRotation(Vector3.up, axis / length) : Quaternion.identity;
            capsule.center = Vector3.zero;
            capsule.radius = radius / scale;
            // capsInside: los extremos redondeados terminan en a y b (no sobresalen).
            capsule.height = Mathf.Max(radius * 2f, capsInside ? length : length + radius * 2f) / scale;
        }

        private void FindBones()
        {
            boneSearch -= Time.deltaTime;
            if (boneSearch > 0f && animator != null) return;
            boneSearch = 0.5f;
            animator = null;
            foreach (Animator a in owner.GetComponentsInChildren<Animator>(false))
                if (a.isHuman && a.isActiveAndEnabled) { animator = a; break; }
            if (animator == null) { hips = neck = head = leftFoot = rightFoot = null; return; }
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck) ?? animator.GetBoneTransform(HumanBodyBones.UpperChest)
                   ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }
    }
}
