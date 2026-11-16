using TMPro;
using UnityEngine;

namespace Wreckabulary.Voice
{
    public sealed class VoiceTestView : MonoBehaviour
    {
        public const string StartLabel = "TEST", StopLabel = "STOP";

        LocalVoiceTest test;
        RectTransform fill;
        TMP_Text status, button;

        public LocalVoiceTest Test => test;

        public static VoiceTestView Bind(GameObject host, LocalVoiceTest test, RectTransform fill, TMP_Text status, TMP_Text button)
        {
            if (!host.TryGetComponent(out VoiceTestView view)) view = host.AddComponent<VoiceTestView>();
            view.test = test;
            view.fill = fill;
            view.status = status;
            view.button = button;
            if (test) test.Owner = view;
            view.Show();
            return view;
        }

        void OnEnable()
        {
            if (test) test.Owner = this;
        }

        void Update() => Show();

        void Show()
        {
            if (!test) return;
            if (fill) fill.anchorMax = new Vector2(Mathf.Clamp01(test.Level), 1f);
            if (status && status.text != test.Status) status.text = test.Status;
            string label = test.Running || test.Waiting ? StopLabel : StartLabel;
            if (button && button.text != label) button.text = label;
        }
    }
}
