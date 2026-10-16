namespace Overdrive.Core
{
    /// <summary>
    /// Un objeto cuyo estado se puede guardar y restaurar para rebobinar el tiempo.
    /// <see cref="Capture"/> devuelve una instantánea inmutable; <see cref="Restore"/> recibe una
    /// instantánea devuelta antes por el mismo objeto.
    /// </summary>
    public interface IRewindable
    {
        object Capture();
        void Restore(object snapshot);
    }
}
