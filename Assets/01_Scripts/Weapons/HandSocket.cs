using UnityEngine;

namespace Overdrive.Weapons
{
    /// <summary>
    /// Punto de la mano donde se sujeta el arma: un empty "WeaponSocket" hijo del hueso de la mano derecha
    /// de cada personaje (lo crea el constructor; se puede mover a mano en el prefab).
    /// </summary>
    public static class HandSocket
    {
        public const string Name = "WeaponSocket";

        /// <summary>Distancia por delante del hueso cuando el modelo no tiene WeaponSocket.</summary>
        public const float FallbackForward = 0.08f;

        /// <summary>El WeaponSocket de la mano derecha, o null si el modelo no tiene mano humanoide.</summary>
        public static Transform Find(Animator animator, out bool isSocket)
        {
            isSocket = false;
            if (animator == null || !animator.isHuman) return null;
            Transform bone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (bone == null) return null;
            Transform socket = bone.Find(Name);
            isSocket = socket != null;
            return isSocket ? socket : bone;
        }

        /// <summary>Posición de agarre: el WeaponSocket, o el hueso un poco adelantado si no hay socket.</summary>
        public static Vector3 GripPosition(Transform socketOrBone, bool isSocket, Vector3 forward) =>
            isSocket ? socketOrBone.position : socketOrBone.position + forward * FallbackForward;
    }
}
