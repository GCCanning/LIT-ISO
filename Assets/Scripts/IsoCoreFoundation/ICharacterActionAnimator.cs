namespace IsoCore.Foundation
{
    /// <summary>
    /// A character body that can play a one-shot action animation (e.g. "slash", "spellcast",
    /// "thrust", "shoot", "hurt"). Implemented by the layered LPC animator in Assembly-CSharp;
    /// defined here so Foundation combat code can drive the player's animation without taking a
    /// hard dependency on the Assembly-CSharp animator type (same pattern as IDamageable).
    /// </summary>
    public interface ICharacterActionAnimator
    {
        void PlayActionAnim(string animId);
    }
}
