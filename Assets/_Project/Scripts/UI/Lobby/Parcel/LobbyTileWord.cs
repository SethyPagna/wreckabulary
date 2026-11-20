using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class LobbyTileWord : MonoBehaviour
    {
        public const string Name = "Tile word";
        const float Travel = .42f, Drop = .38f, Stagger = .045f, Gap = .1f;

        sealed class Tile
        {
            public RectTransform rect;
            public char letter;
            public Vector3 from;
            public bool dropped;
            public float delay, tilt;
        }

        sealed class Loose
        {
            public RectTransform rect;
            public Vector2 velocity;
            public float spin, age;
        }

        static readonly List<(char letter, Vector3 at, float size)> last = new();
        static bool pending;
        static readonly List<Loose> falling = new();
        static RectTransform fallLayer;

        readonly List<Tile> tiles = new();
        string word;
        float size, clock = -1f;
        public string Word => word;
        public int Count => tiles.Count;
        public bool Settled => clock < 0f;

        public static LobbyTileWord Create(RectTransform parent, string word, float size)
        {
            var rect = LobbyKit.Rect(parent, Name);
            var tileWord = rect.gameObject.AddComponent<LobbyTileWord>();
            tileWord.Set(word, size);
            return tileWord;
        }

        public static LobbyTileWord Replace(TMPro.TMP_Text label, float size)
        {
            if (!label) return null;
            var rect = label.rectTransform;
            var host = LobbyKit.Rect(rect.parent, Name);
            host.anchorMin = rect.anchorMin; host.anchorMax = rect.anchorMax; host.pivot = rect.pivot;
            host.anchoredPosition = rect.anchoredPosition; host.sizeDelta = rect.sizeDelta;
            host.SetSiblingIndex(rect.GetSiblingIndex());
            if (rect.TryGetComponent(out LayoutElement element))
            {
                var copy = host.gameObject.AddComponent<LayoutElement>();
                copy.minWidth = element.minWidth; copy.preferredWidth = element.preferredWidth; copy.flexibleWidth = element.flexibleWidth;
                copy.minHeight = element.minHeight; copy.preferredHeight = element.preferredHeight; copy.flexibleHeight = element.flexibleHeight;
            }
            var tileWord = host.gameObject.AddComponent<LobbyTileWord>();
            tileWord.align = label.alignment;
            tileWord.Set(label.text, size);
            label.gameObject.SetActive(false);
            return tileWord;
        }

        TMPro.TextAlignmentOptions align = TMPro.TextAlignmentOptions.Left;

        public void Set(string text, float tileSize)
        {
            word = LobbyKit.Upper(text).Trim();
            size = tileSize;
            foreach (var t in tiles) if (t.rect) Destroy(t.rect.gameObject);
            tiles.Clear();
            int i = 0;
            foreach (char c in word)
            {
                if (c == ' ') { tiles.Add(new Tile { letter = ' ' }); continue; }
                var rect = Square(transform, c, size);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
                rect.sizeDelta = new Vector2(size, size);
                float tilt = ((c * 7 + i * 13) % 9 - 4) * 1.1f;
                tiles.Add(new Tile { rect = rect, letter = c, tilt = tilt });
                i++;
            }
            bool animate = pending;
            pending = false;
            Layout(animate);
            clock = animate ? 0f : -1f;
            if (!animate) Settle();
        }

        void Settle()
        {
            for (int i = 0; i < tiles.Count; i++)
                if (tiles[i].rect) { tiles[i].rect.anchoredPosition = Slot(i); tiles[i].rect.localRotation = Quaternion.Euler(0f, 0f, -tiles[i].tilt); }
        }

        float Width => tiles.Count == 0 ? 0f : tiles.Count * size + (tiles.Count - 1) * size * Gap;

        Vector2 Slot(int index)
        {
            var rect = ((RectTransform)transform).rect;
            float start = align switch
            {
                TMPro.TextAlignmentOptions.Center or TMPro.TextAlignmentOptions.Midline or TMPro.TextAlignmentOptions.Top
                    or TMPro.TextAlignmentOptions.Bottom => (rect.width - Width) * .5f,
                TMPro.TextAlignmentOptions.Right or TMPro.TextAlignmentOptions.MidlineRight or TMPro.TextAlignmentOptions.TopRight
                    or TMPro.TextAlignmentOptions.BottomRight => rect.width - Width,
                _ => 0f,
            };
            float lift = ((index * 5) % 3 - 1) * size * .04f;
            return new Vector2(start + index * size * (1f + Gap), lift);
        }

        void Layout(bool begin)
        {
            var pool = new List<(char letter, Vector3 at, float size)>(last);
            float delay = 0f;
            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                if (!t.rect) continue;
                t.rect.anchoredPosition = Slot(i);
                if (!begin) continue;
                int reuse = pool.FindIndex(p => p.letter == t.letter);
                if (reuse >= 0) { t.from = pool[reuse].at; t.dropped = false; pool.RemoveAt(reuse); }
                else t.dropped = true;
                t.delay = delay;
                delay += Stagger;
            }
            if (!begin) return;
            foreach (var spare in pool) Tumble(spare.letter, spare.at, spare.size);
            last.Clear();
        }

        void OnRectTransformDimensionsChange()
        {
            if (clock < 0f) Settle();
        }

        void Update()
        {
            UpdateLoose(Time.unscaledDeltaTime);
            if (clock < 0f) return;
            clock += Time.unscaledDeltaTime;
            bool done = true;
            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                if (!t.rect) continue;
                var slot = Slot(i);
                float k = Mathf.Clamp01((clock - t.delay) / (t.dropped ? Drop : Travel));
                if (k < 1f) done = false;
                if (t.dropped)
                {
                    float fall = 1f - Bounce(k);
                    t.rect.anchoredPosition = slot + Vector2.up * size * 2.2f * fall;
                    t.rect.localRotation = Quaternion.Euler(0f, 0f, -t.tilt + fall * 25f);
                    t.rect.localScale = Vector3.one * (k <= 0f ? 0f : 1f);
                }
                else
                {
                    float ease = 1f - Mathf.Pow(1f - k, 3f);
                    var target = t.rect.parent.TransformPoint(Anchored(t.rect, slot));
                    var hop = Vector3.up * Mathf.Sin(k * Mathf.PI) * size * .9f * t.rect.lossyScale.y;
                    t.rect.position = Vector3.LerpUnclamped(t.from, target, ease) + hop;
                    t.rect.localRotation = Quaternion.Euler(0f, 0f, -t.tilt + Mathf.Sin(k * Mathf.PI) * 18f);
                    t.rect.localScale = Vector3.one;
                }
            }
            if (!done) return;
            clock = -1f;
            Settle();
        }

        public static RectTransform Square(Transform parent, char letter, float size)
        {
            var tile = LobbyKit.Rect(parent, "Tile " + letter);
            tile.sizeDelta = new Vector2(size, size);
            LobbyKit.Face(tile, LobbyKit.WoodHi, 3, LobbyKit.Cocoa, 2, Mathf.Max(2f, size * .07f), LobbyKit.WoodLo, dropColour: LobbyKit.Cocoa);
            var shine = LobbyKit.Rect(tile, "Shine").Place(new Vector2(0, 1), Vector2.one, new Vector2(size * .16f, -size * .15f), new Vector2(-size * .16f, -size * .09f));
            shine.Paint(new Color(1, 1, 1, .5f), 1).raycastTarget = false;
            LobbyKit.Display(tile, letter.ToString(), size * .66f, LobbyKit.WoodInk).rectTransform.Fill();
            return tile;
        }

        static Vector3 Anchored(RectTransform rect, Vector2 slot)
        {
            var parent = (RectTransform)rect.parent;
            var r = parent.rect;
            return new Vector3(r.xMin + slot.x + rect.rect.width * rect.pivot.x, r.center.y + slot.y, 0f);
        }

        static float Bounce(float t)
        {
            if (t < .64f) { float u = t / .64f; return u * u; }
            float v = (t - .82f) / .18f;
            return 1f - .12f * (1f - v * v);
        }

        public static void Handoff(Transform outgoing)
        {
            last.Clear();
            pending = true;
            if (!outgoing) return;
            foreach (var word in outgoing.GetComponentsInChildren<LobbyTileWord>())
                foreach (var t in word.tiles)
                    if (t.rect && t.letter != ' ') last.Add((t.letter, t.rect.position, word.size));
        }

        void Tumble(char letter, Vector3 at, float tileSize)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (!canvas) return;
            if (!fallLayer || fallLayer.root != canvas.rootCanvas.transform)
            {
                fallLayer = LobbyKit.Rect(canvas.rootCanvas.transform, "Falling tiles").Fill();
                var group = fallLayer.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false; group.interactable = false;
            }
            fallLayer.SetAsLastSibling();
            var rect = Square(fallLayer, letter, tileSize);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.sizeDelta = new Vector2(tileSize, tileSize);
            rect.position = at;
            falling.Add(new Loose
            {
                rect = rect,
                velocity = new Vector2(((letter * 31) % 7 - 3) * 60f, 260f + (letter % 5) * 40f),
                spin = ((letter * 17) % 11 - 5) * 70f,
            });
        }

        static void UpdateLoose(float dt)
        {
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                var loose = falling[i];
                loose.age += dt;
                if (!loose.rect || loose.age > 1.4f)
                {
                    if (loose.rect) Destroy(loose.rect.gameObject);
                    falling.RemoveAt(i);
                    continue;
                }
                loose.velocity += Vector2.down * 1900f * dt;
                loose.rect.anchoredPosition += loose.velocity * dt;
                loose.rect.localRotation = Quaternion.Euler(0f, 0f, loose.rect.localEulerAngles.z + loose.spin * dt);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime() { last.Clear(); falling.Clear(); fallLayer = null; pending = false; }
    }
}
