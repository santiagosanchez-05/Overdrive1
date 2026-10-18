using Overdrive.Combat;

namespace Overdrive.Enemies
{
    /// <summary>
    /// Armadura opcional de un <see cref="Enemy"/> (el jefe): decide si un golpe se absorbe en vez de tumbarlo o matarlo.
    /// </summary>
    public interface IHitArmor
    {
        /// <summary>Golpe letal (arma, disparo). true = lo absorbe y sigue vivo.</summary>
        bool AbsorbLethal(KillCause cause);

        /// <summary>Derribo (puñetazo, pisotón, arma lanzada). true = no cae.</summary>
        bool ResistKnockdown(KnockdownCause cause);
    }
}
