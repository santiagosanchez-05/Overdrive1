using UnityEngine;

namespace Overdrive.Combat
{
    /// <summary>
    /// Zonas de impacto de un personaje (jugador o enemigo): cabeza o cuerpo. La cabeza se toma del hueso
    /// Head del modelo humanoide; sin modelo, del 15 % superior de la cápsula.
    /// </summary>
    public static class HitZones
    {
        /// <summary>Cuánto por debajo del hueso de la cabeza empieza la zona de la cabeza (cuello incluido).</summary>
        public const float HeadMargin = 0.14f;

        /// <summary>¿El punto de impacto está en la cabeza?</summary>
        public static bool IsHead(Transform character, Vector3 point)
        {
            if (character == null) return false;
            return point.y >= HeadY(character) - HeadMargin;
        }

        /// <summary>Altura (en el mundo) del centro de la cabeza.</summary>
        public static float HeadY(Transform character)
        {
            Transform head = HeadBone(character);
            if (head != null) return head.position.y + 0.06f;
            float height = CapsuleHeight(character);
            return character.position.y + height * 0.9f;
        }

        /// <summary>
        /// Punto al que apuntar para dar en la cabeza: su esfera <see cref="HeadHitbox"/>. Si aún no la tiene,
        /// lo alto de su cápsula (que sí se puede alcanzar).
        /// </summary>
        public static Vector3 HeadPoint(Transform character)
        {
            Transform box = character.Find(HeadHitbox.ObjectName);
            if (box != null && box.gameObject.activeInHierarchy) return box.position;
            return new Vector3(character.position.x, Mathf.Min(HeadY(character), ReachTop(character)), character.position.z);
        }

        /// <summary>Punto al que apuntar para dar en el pecho.</summary>
        public static Vector3 ChestPoint(Transform character)
        {
            float feet = character.position.y;
            float y = Mathf.Lerp(feet, HeadY(character), 0.6f);
            // Sin zona de cuerpo (aún no ha arrancado), que quede dentro de la cápsula.
            if (character.Find(HeadHitbox.BodyName) == null) y = Mathf.Min(y, ReachTop(character) - 0.1f);
            return new Vector3(character.position.x, y, character.position.z);
        }

        /// <summary>
        /// Altura alcanzable de la cápsula de movimiento, sin contar la media esfera de arriba
        /// (la física del CharacterController es algo más baja que su altura nominal).
        /// </summary>
        private static float ReachTop(Transform character)
        {
            var cc = character.GetComponent<CharacterController>();
            if (cc != null) return character.position.y + cc.center.y + cc.height * 0.5f - cc.radius;
            return character.position.y + CapsuleHeight(character) * 0.8f;
        }

        private static Transform HeadBone(Transform character)
        {
            foreach (Animator animator in character.GetComponentsInChildren<Animator>(false))
            {
                if (!animator.isHuman || !animator.isActiveAndEnabled) continue;
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null) return head;
            }
            return null;
        }

        private static float CapsuleHeight(Transform character)
        {
            var cc = character.GetComponent<CharacterController>();
            if (cc != null) return cc.height;
            var capsule = character.GetComponentInChildren<CapsuleCollider>();
            return capsule != null ? capsule.height * capsule.transform.lossyScale.y : 1.8f;
        }
    }
}
