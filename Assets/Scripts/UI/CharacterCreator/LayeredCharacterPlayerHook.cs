using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// Connects the LPC layered wardrobe to the in-game player. Lives in
    /// Assembly-CSharp (CharacterCreator can't be referenced from
    /// IsoCore.Foundation without a circular assembly reference), so it hooks
    /// itself in via <see cref="FoundationBootstrap.Ready"/> instead.
    ///
    /// When the LPC catalog is available, replaces the player's
    /// PlayerAnimator with a LayeredCharacterAnimator that self-loads the
    /// saved LayeredAppearance — i.e. whatever character was built in
    /// CharacterCreatorUI (or the sanitized default) is what's actually
    /// rendered/animated for the player in-world.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public static class LayeredCharacterPlayerHook
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            FoundationBootstrap.Ready += OnReady;
        }

        static void OnReady(FoundationBootstrap bootstrap)
        {
            if (!CharacterLayerCatalog.Available) return;
            var player = bootstrap.Player;
            if (player == null) return;
            var go = player.gameObject;

            var legacy = go.GetComponent<PlayerAnimator>();
            if (legacy != null) legacy.enabled = false;

            if (go.GetComponent<LayeredCharacterAnimator>() == null)
            {
                var layered = go.AddComponent<LayeredCharacterAnimator>();
                layered.useWorldAmbientTint = true;
            }

            // Drives the 5 one-shot action animations (attack key + spell/damage
            // events) so cast/thrust/slash/shoot/hurt are reachable in play.
            if (go.GetComponent<LayeredCharacterActions>() == null)
                go.AddComponent<LayeredCharacterActions>();
        }
    }
}
