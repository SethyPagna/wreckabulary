using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class LobbyLooseTiles : MonoBehaviour
    {
        public const string Name = "Falling tiles";
        public const float Life = 1.4f;
        const float Gravity = 1900f, Sideways = 60f, Upward = 260f, UpwardStep = 40f, Spin = 70f;

        sealed class Loose
        {
            public RectTransform rect;
            public Vector2 velocity;
            public float spin, age;
        }

        readonly List<Loose> falling = new();
        public int Count => falling.Count;

        public static LobbyLooseTiles Behind(Transform pages)
        {
            var host = pages ? pages.parent : null;
            if (!host) return null;
            var known = host.Find(Name);
            var layer = known ? known.GetComponent<LobbyLooseTiles>() : null;
            if (!layer)
            {
                var rect = LobbyKit.Rect(host, Name).Fill();
                var group = rect.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false; group.interactable = false;
                layer = rect.gameObject.AddComponent<LobbyLooseTiles>();
            }
            int under = pages.GetSiblingIndex();
            int at = layer.transform.GetSiblingIndex();
            layer.transform.SetSiblingIndex(at < under ? under - 1 : under);
            return layer;
        }

        public void Toss(char letter, Vector3 at, float size)
        {
            var rect = LobbyTileWord.Square(transform, letter, size);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.position = at;
            falling.Add(new Loose
            {
                rect = rect,
                velocity = new Vector2(((letter * 31) % 7 - 3) * Sideways, Upward + (letter % 5) * UpwardStep),
                spin = ((letter * 17) % 11 - 5) * Spin,
            });
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                var loose = falling[i];
                loose.age += dt;
                if (!loose.rect || loose.age > Life)
                {
                    if (loose.rect) Destroy(loose.rect.gameObject);
                    falling.RemoveAt(i);
                    continue;
                }
                loose.velocity += Vector2.down * Gravity * dt;
                loose.rect.anchoredPosition += loose.velocity * dt;
                loose.rect.localRotation = Quaternion.Euler(0f, 0f, loose.rect.localEulerAngles.z + loose.spin * dt);
            }
        }

        void OnDisable()
        {
            foreach (var loose in falling)
                if (loose.rect) Destroy(loose.rect.gameObject);
            falling.Clear();
        }
    }
}
