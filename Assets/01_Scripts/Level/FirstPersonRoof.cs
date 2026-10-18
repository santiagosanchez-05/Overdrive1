using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Techos del nivel: en vista cenital (o tercera persona) están apagados porque taparían la cámara, y al pasar a
    /// primera persona (tecla V) se encienden para que se vea el techo. Los techos no tienen colliders: son solo visuales.
    /// Este componente va en un objeto siempre activo; los techos son los objetos que se activan y desactivan.
    /// </summary>
    public class FirstPersonRoof : MonoBehaviour
    {
        [SerializeField] private GameObject[] roofs = new GameObject[0];

        public GameObject[] Roofs => roofs;

        private void OnEnable()
        {
            ViewMode.Changed += Apply;
            Apply(ViewMode.FirstPerson);
        }

        private void OnDisable() => ViewMode.Changed -= Apply;

        /// <summary>Enciende los techos en primera persona y los apaga en cenital. Público para simularlo en el editor.</summary>
        public void Apply(bool firstPerson)
        {
            foreach (GameObject roof in roofs)
                if (roof != null && roof.activeSelf != firstPerson) roof.SetActive(firstPerson);
        }
    }
}
