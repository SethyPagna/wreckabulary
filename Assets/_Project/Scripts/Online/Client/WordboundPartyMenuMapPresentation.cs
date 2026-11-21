using UnityEngine;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundPartyMenu
    {
        const float PreviewReservedHeight = 460, PreviewMinimumWidth = 160, PreviewMaximumWidth = 360, PreviewAspect = 16f / 9f;
        GUIStyle centeredTitle, centeredHint, centeredProblem;
        int centeredGeneration = -1;
        LobbyPlayLayout installedMapArt;

        void PrepareCenteredStyles()
        {
            if (centeredGeneration != ui.Generation || centeredTitle == null)
            {
                centeredTitle = new GUIStyle(ui.Heading) { alignment = TextAnchor.MiddleCenter };
                centeredHint = new GUIStyle(ui.Wrapped) { alignment = TextAnchor.MiddleCenter };
                centeredProblem = paper.Problem != null ? new GUIStyle(paper.Problem) { alignment = TextAnchor.MiddleCenter } : null;
                centeredGeneration = ui.Generation;
            }
            title = centeredTitle; hint = centeredHint;
        }

        GUIStyle ProblemCentered() => centeredProblem ?? hint;

        void DrawInstalledMapPreview()
        {
            installedMapArt ??= Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            bool arena = Modes[mode] == "FreeForAll";
            GUILayout.Space(ui.Gap);
            GUILayout.Label(arena ? "Toybox Arena" : "The Lost Address", title);
            var sprite = arena ? null : LobbyMapSnapshots.For(WordboundSoloEntry.LostAddressMap);
            bool snapshot = sprite && sprite.texture;
            if (!snapshot) sprite = installedMapArt ? arena ? installedMapArt.CombatArenaPreview : installedMapArt.LostAddressPreview : null;
            if (!sprite || !sprite.texture) return;
            float previewWidth = Mathf.Clamp((partyAvailableHeight - PreviewReservedHeight) * PreviewAspect, PreviewMinimumWidth, PreviewMaximumWidth);
            float width = Mathf.Min(previewWidth, Mathf.Max(120, partyRect.width - ui.Panel.padding.horizontal - 32));
            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            var image = GUILayoutUtility.GetRect(width, width / PreviewAspect, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
            if (Event.current.type != EventType.Repaint) return;
            Rect uv = sprite.textureRect;
            uv = new Rect(uv.x / sprite.texture.width, uv.y / sprite.texture.height,
                uv.width / sprite.texture.width, uv.height / sprite.texture.height);
            if (paper.Dressed) { paper.Picture(image, sprite.texture, uv, snapshot); return; }
            Color previous = GUI.color;
            try
            {
                GUI.color = Color.white;
                Rect pixels = sprite.textureRect;
                float scale = Mathf.Min(image.width / pixels.width, image.height / pixels.height);
                var fitted = new Rect(image.center - new Vector2(pixels.width, pixels.height) * scale * .5f,
                    new Vector2(pixels.width, pixels.height) * scale);
                GUI.DrawTextureWithTexCoords(fitted, sprite.texture, uv, true);
            }
            finally { GUI.color = previous; }
        }
    }
}
