using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    /// <summary>A two-stop gradient across a graphic, top to bottom or left to right. It multiplies the
    /// vertex colour, so the graphic's own colour stays white. Add it before Shadow, or the shadow
    /// takes the gradient too.</summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class LobbyGradient : BaseMeshEffect
    {
        public Color From = Color.white, To = Color.white;
        public bool Horizontal;

        public void Set(Color from, Color to, bool horizontal = false)
        {
            From = from; To = to; Horizontal = horizontal;
            if (graphic) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            var rect = graphic.rectTransform.rect;
            var vertex = new UIVertex();
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                // From is the top (or left) edge.
                float t = Horizontal
                    ? Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x)
                    : Mathf.InverseLerp(rect.yMax, rect.yMin, vertex.position.y);
                vertex.color = (Color)vertex.color * Color.Lerp(From, To, t);
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
