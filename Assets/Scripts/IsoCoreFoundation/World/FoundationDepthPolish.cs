using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Centralized visual-depth helpers for runtime-spawned Foundation sprites.</summary>
    public static class FoundationDepthPolish
    {
        public static void Attach(GameObject go, bool fadeWhenOccluding = true,
            bool castLongShadow = true, float contactScale = 1f, float contactAlpha = 0.28f)
        {
            if (go == null || go.GetComponent<SpriteRenderer>() == null)
                return;

            var contact = go.GetComponent<FoundationContactShadow>() ??
                          go.AddComponent<FoundationContactShadow>();
            contact.Configure(contactScale, contactAlpha);

            if (castLongShadow && go.GetComponent<DecorationShadow>() == null)
                go.AddComponent<DecorationShadow>();

            if (fadeWhenOccluding && go.GetComponent<PropOcclusionFader>() == null)
                go.AddComponent<PropOcclusionFader>();
        }
    }
}
