using Overdrive.Weapons;
using UnityEditor;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Crea el empty <see cref="HandSocket.Name"/> en la mano derecha de cada modelo humanoide del jugador
    /// y de los enemigos. Si ya existe no lo toca: se puede colocar a mano en el prefab y se respeta.
    /// </summary>
    internal static class HandSocketFactory
    {
        [MenuItem("TopDown/Crear puntos de agarre en las manos")]
        public static void EnsureAll()
        {
            EnsureIn(AssetFactory.PlayerPrefabPath);
            EnsureIn(AssetFactory.EnemyPrefabPath);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureIn(string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return;
            BuilderUtil.EditPrefab(prefabPath, System.IO.Path.GetFileNameWithoutExtension(prefabPath), root =>
            {
                foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                {
                    if (!animator.isHuman) continue;
                    Transform bone = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    if (bone == null || bone.Find(HandSocket.Name) != null) continue;
                    var socket = new GameObject(HandSocket.Name).transform;
                    socket.SetParent(bone, false);
                    socket.position = bone.position + animator.transform.forward * HandSocket.FallbackForward;
                    socket.rotation = animator.transform.rotation;
                }
            });
        }
    }
}
