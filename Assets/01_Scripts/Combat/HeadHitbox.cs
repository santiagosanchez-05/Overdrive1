using UnityEngine;

namespace Overdrive.Combat
{
    /// <summary>
    /// Zonas de impacto que se ajustan al modelo de un personaje (jugador o enemigo): una esfera en la cabeza
    /// (sigue al hueso Head) y una cápsula de cuerpo de los pies al cuello. Hacen falta porque los modelos
    /// (escala 1,3) son más altos que la cápsula de movimiento (1,8 m): sin ellas el pecho alto, el cuello y
    /// la cabeza no se podrían alcanzar.
    /// Son triggers (no empujan ni bloquean nada) en la capa "Ignore Raycast": las balas, las armas lanzadas
    /// y la mira los buscan expresamente (<see cref="IsHitbox"/>); las sondas del suelo, la visión y el
    /// movimiento no los ven.
    /// Se crean solas: <see cref="Ensure"/> desde el jugador y los enemigos.
    /// </summary>
    public class HeadHitbox : MonoBehaviour
    {
        public const string ObjectName = "HeadHitbox";
        public const string BodyName = "BodyHitbox";

        [SerializeField] private float radius = 0.24f;
        [SerializeField] private float bodyRadius = 0.32f;

        private Transform owner;
        private Transform headBone;
        private SphereCollider sphere;
        private CapsuleCollider body;
        private float boneSearch;

        /// <summary>Personaje al que pertenece (raíz del jugador o del enemigo).</summary>
        public Transform Owner => owner;

        public static HeadHitbox Ensure(Transform character)
        {
            Transform existing = character.Find(ObjectName);
            if (existing != null && existing.TryGetComponent(out HeadHitbox found)) return found;
            int layer = LayerMask.NameToLayer("Ignore Raycast");

            var go = new GameObject(ObjectName);
            if (layer >= 0) go.layer = layer;
            go.transform.SetParent(character, false);
            var box = go.AddComponent<HeadHitbox>();
            box.owner = character;
            box.sphere = go.AddComponent<SphereCollider>();
            box.sphere.radius = box.radius;
            box.sphere.isTrigger = true;

            var bodyGo = new GameObject(BodyName);
            if (layer >= 0) bodyGo.layer = layer;
            bodyGo.transform.SetParent(character, false);
            box.body = bodyGo.AddComponent<CapsuleCollider>();
            box.body.direction = 1; // eje Y
            box.body.radius = box.bodyRadius;
            box.body.isTrigger = true;

            // Que los colliders propios del personaje no choquen con sus zonas de impacto.
            foreach (Collider c in character.GetComponents<Collider>())
            {
                Physics.IgnoreCollision(c, box.sphere);
                Physics.IgnoreCollision(c, box.body);
            }
            Physics.IgnoreCollision(box.sphere, box.body);
            box.Follow();
            return box;
        }

        /// <summary>¿Es una zona de impacto (cabeza o cuerpo) de un personaje?</summary>
        public static bool IsHitbox(Collider c) => c != null && c.isTrigger && (c.name == ObjectName || c.name == BodyName);

        /// <summary>¿Es la zona de la cabeza?</summary>
        public static bool IsHead(Collider c) => c != null && c.isTrigger && c.name == ObjectName;

        /// <summary>Activa o desactiva las zonas (tumbado o muerto no se le puede disparar así).</summary>
        public void SetActive(bool active)
        {
            if (sphere != null) sphere.enabled = active;
            if (body != null) body.enabled = active;
        }

        private void LateUpdate() => Follow();

        private void Follow()
        {
            if (owner == null) owner = transform.parent;
            if (headBone == null || !headBone.gameObject.activeInHierarchy)
            {
                boneSearch -= Time.deltaTime;
                if (boneSearch <= 0f)
                {
                    boneSearch = 0.5f;
                    headBone = FindHead(owner);
                }
            }
            Vector3 head = headBone != null ? headBone.position + Vector3.up * 0.06f : owner.position + Vector3.up * 1.7f;
            transform.position = head;

            // Cuerpo: de los pies hasta el cuello, siguiendo la altura real del modelo.
            if (body != null)
            {
                float neck = Mathf.Max(0.8f, head.y - owner.position.y - radius * 0.8f);
                Vector3 ownerScale = owner.lossyScale;
                float sy = Mathf.Approximately(ownerScale.y, 0f) ? 1f : ownerScale.y;
                body.transform.localPosition = Vector3.zero;
                body.height = neck / sy;
                body.center = new Vector3(0f, neck * 0.5f / sy, 0f);
            }
        }

        private static Transform FindHead(Transform character)
        {
            if (character == null) return null;
            foreach (Animator a in character.GetComponentsInChildren<Animator>(false))
                if (a.isHuman && a.isActiveAndEnabled)
                {
                    Transform head = a.GetBoneTransform(HumanBodyBones.Head);
                    if (head != null) return head;
                }
            return null;
        }
    }
}
