using System.Collections.Generic;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>Acumula los resultados de la verificación y registra cada fallo.</summary>
    internal sealed class Checker
    {
        private readonly List<string> failures = new();

        public int Passed { get; private set; }
        public IReadOnlyList<string> Failures => failures;

        public void Check(bool condition, string message)
        {
            if (condition)
            {
                Passed++;
                return;
            }
            failures.Add(message);
            Debug.LogError("[Verify] FALLO: " + message);
        }

        public void NotNull(Object obj, string message) => Check(obj != null, message);

        public void Near(float actual, float expected, string message, float epsilon = 0.001f)
        {
            Check(Mathf.Abs(actual - expected) <= epsilon, $"{message} (esperado {expected}, obtenido {actual})");
        }

        public void Near(Vector3 actual, Vector3 expected, string message, float epsilon = 0.001f)
        {
            Check((actual - expected).magnitude <= epsilon, $"{message} (esperado {expected}, obtenido {actual})");
        }
    }
}
