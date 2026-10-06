using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Shrinks a child to the width of this rect, never growing it: a row of cards keeps its design size
    /// and gets smaller as a whole when there are more cards or the screen is narrower, instead of wrapping
    /// onto whatever is below it.
    /// </summary>
    public sealed class LobbyFit : MonoBehaviour
    {
        public RectTransform Target;

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();

        public void Fit()
        {
            if (!Target || Target.sizeDelta.x <= 0f) return;
            float width = ((RectTransform)transform).rect.width;
            float scale = width > 0f ? Mathf.Min(1f, width / Target.sizeDelta.x) : 1f;
            Target.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
