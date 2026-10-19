using System;
using UnityEngine;

namespace Overdrive.Level
{
    [Serializable]
    public sealed class NeonMaterialBinding
    {
        public Renderer renderer;
        public Material[] original;
        public Material[] styled;
    }

    /// <summary>Permite comparar el acabado con la escena original desactivando el objeto Visual Finish.</summary>
    public sealed class NeonSceneLook : MonoBehaviour
    {
        [SerializeField] private NeonMaterialBinding[] bindings = Array.Empty<NeonMaterialBinding>();
        public NeonMaterialBinding[] Bindings => bindings;

        public void Configure(NeonMaterialBinding[] values) { bindings = values; Apply(true); }
        public void RestoreOriginals() => Apply(false);
        private void OnEnable() => Apply(true);
        private void OnDisable() => Apply(false);

        private void Apply(bool styled)
        {
            foreach (var binding in bindings)
                if (binding.renderer != null) binding.renderer.sharedMaterials = styled ? binding.styled : binding.original;
        }
    }
}
