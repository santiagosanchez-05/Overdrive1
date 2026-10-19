using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Pared invisible solo para balas, armas lanzadas y la vista de los enemigos: los personajes la atraviesan.
    /// En el nivel 2 las paredes del club se recortan en altura para que se vean bien desde arriba; esta colisión
    /// recupera su altura completa para que nadie dispare (ni vea) por encima de ellas.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BulletWall : MonoBehaviour
    {
        /// <summary>Cada cuánto se vuelve a aplicar (aparecen refuerzos y al rebobinar se reactivan objetos).</summary>
        private const float Refresh = 1f;

        private Collider wall;
        private float timer;

        private void OnEnable()
        {
            wall = GetComponent<Collider>();
            IgnoreCharacters();
        }

        private void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            IgnoreCharacters();
        }

        /// <summary>El movimiento de los personajes no choca con ella; los rayos y barridos (balas, vista) sí.</summary>
        private void IgnoreCharacters()
        {
            timer = Refresh;
            if (wall == null) return;
            foreach (CharacterController cc in FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude))
                Physics.IgnoreCollision(cc, wall, true);
        }
    }
}
