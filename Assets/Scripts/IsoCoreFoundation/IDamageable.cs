namespace IsoCore.Foundation
{
    /// <summary>
    /// Any entity that can receive damage (enemies, destructibles, etc.).
    /// Defined in IsoCore.Foundation so the player assembly can call it
    /// without taking a hard dependency on Assembly-CSharp types.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(int amount);
    }
}
