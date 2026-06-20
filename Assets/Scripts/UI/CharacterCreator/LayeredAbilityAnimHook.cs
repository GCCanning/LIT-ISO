using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// Phase 4 ability->animation bridge (Assembly-CSharp side).
    ///
    /// <see cref="LayeredAbilityAnim.Resolve"/> maps a Foundation ability id to its LPC one-shot
    /// animation by reading <see cref="FoundationAbilityDefinition.ResolvedAnimationId"/> off the
    /// live content. Used by both the NPC driver (<see cref="LayeredNpcDriver"/>) and the player
    /// listener below.
    ///
    /// <see cref="LayeredAbilityPlayerHook"/> wires the PLAYER path: it subscribes to
    /// <see cref="FoundationAbilitySystem.OnAbilityUsed"/> and plays the matching one-shot on the
    /// player's <see cref="LayeredCharacterAnimator"/>. NPCs are handled in LayeredNpcDriver via
    /// <see cref="Mob.AbilityUsed"/>. All animator calls live here in Assembly-CSharp.
    /// </summary>
    public static class LayeredAbilityAnim
    {
        static FoundationContent _content;

        public static void SetContent(FoundationContent content) => _content = content;

        /// <summary>Resolves an ability id to its LPC one-shot animation id (spellcast/shoot/slash/...).</summary>
        public static string Resolve(string abilityId)
        {
            if (!string.IsNullOrEmpty(abilityId) && _content != null)
            {
                var def = _content.Abilities.Get(abilityId);
                if (def != null) return def.ResolvedAnimationId;
            }
            return "slash"; // safe melee default when the ability is unknown
        }
    }

    /// <summary>
    /// Bridges the player's Foundation ability use to the layered animator. Lives in
    /// Assembly-CSharp; hooks itself in via <see cref="FoundationBootstrap.Ready"/>.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public static class LayeredAbilityPlayerHook
    {
        static FoundationBootstrap _bootstrap;
        static LayeredCharacterAnimator _anim;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            FoundationBootstrap.Ready += OnReady;
        }

        static void OnReady(FoundationBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            LayeredAbilityAnim.SetContent(bootstrap.Content);

            if (bootstrap.Abilities != null)
            {
                bootstrap.Abilities.OnAbilityUsed -= OnPlayerAbilityUsed;
                bootstrap.Abilities.OnAbilityUsed += OnPlayerAbilityUsed;
            }
        }

        static void OnPlayerAbilityUsed(string abilityId, string animationId)
        {
            var anim = ResolvePlayerAnimator();
            if (anim == null) return;
            anim.PlayOneShot(string.IsNullOrEmpty(animationId) ? "spellcast" : animationId);
        }

        static LayeredCharacterAnimator ResolvePlayerAnimator()
        {
            if (_anim != null) return _anim;
            var player = _bootstrap != null ? _bootstrap.Player : null;
            if (player != null)
                _anim = player.GetComponent<LayeredCharacterAnimator>();
            return _anim;
        }
    }
}
