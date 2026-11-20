using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class LobbyParcelGrain : MonoBehaviour
    {
        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();

        void Fit()
        {
            if (!TryGetComponent(out RawImage image)) return;
            if (!image.texture) image.texture = LobbyParcel.GrainTexture();
            var size = ((RectTransform)transform).rect.size;
            image.uvRect = new Rect(0f, 0f, Mathf.Max(1f, size.x) / LobbyParcel.GrainSize, Mathf.Max(1f, size.y) / LobbyParcel.GrainSize);
        }
    }
}
