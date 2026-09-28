using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// The wardrobe in the house. Walk up and press grab to open the dress-up page: up/down picks a row,
    /// left/right changes it, grab on Shuffle or Done, spell to finish. Changes show on the roommate
    /// straight away and are kept for the session, so they follow you into every mode.
    /// </summary>
    [DefaultExecutionOrder(-40)] // after input is read, before combat sees the grab press
    public class Wardrobe : MonoBehaviour
    {
        public enum Row { Colour, Letter, Hat, Extra, Shuffle, Done }

        [SerializeField] WardrobePage page;
        [Tooltip("Films the roommate for the page's preview, like a fitting-room mirror.")]
        [SerializeField] Camera previewCamera;
        [SerializeField] TextMeshPro prompt;
        [SerializeField] float useRange = 2f;

        RenderTexture previewTexture;
        PlayerLook look;
        float spin;

        public PlayerController User { get; private set; }
        public Row Selected { get; private set; }
        public PlayerLook Look => look;
        public Camera PreviewCamera => previewCamera;
        public WardrobePage Page => page;

        void Awake()
        {
            previewTexture = new RenderTexture(512, 512, 24) { name = "Wardrobe Preview" };
            if (previewCamera)
            {
                previewCamera.targetTexture = previewTexture;
                previewCamera.enabled = false;
            }
            if (page) page.Hide();
        }

        void OnDestroy()
        {
            if (previewTexture) previewTexture.Release();
        }

        void Update()
        {
            if (User)
            {
                if (User.IsKnockedOut || User.IsHeld) { Close(); return; }
                var c = User.Commands;
                if (c.up) Step(-1);
                if (c.down) Step(1);
                if (c.left) Change(-1);
                if (c.right) Change(1);
                if (c.grab || c.confirm)
                {
                    if (Selected == Row.Shuffle) Shuffle();
                    else if (Selected == Row.Done) Close();
                    else Change(1);
                }
                if (c.spellDown) Close();
                return;
            }
            foreach (var p in World.Players)
            {
                if (!p.CanAct || p.Combat.IsHolding || !InRange(p) || !p.Commands.grab) continue;
                Open(p);
                break;
            }
        }

        void LateUpdate()
        {
            if (prompt)
            {
                bool near = false;
                foreach (var p in World.Players) near |= InRange(p);
                prompt.text = User ? "" : near ? "<color=#FFD24A>Press grab to dress up</color>" : "WARDROBE";
                Popup.Billboard(prompt.transform);
            }
            if (!User) return;

            // The preview camera sways gently around the roommate, like turning in front of a mirror.
            spin += Time.deltaTime;
            var target = User.transform.position + Vector3.up * 0.85f;
            var offset = Quaternion.Euler(0f, Mathf.Sin(spin * 0.8f) * 35f, 0f) * new Vector3(0f, 0.35f, -2.4f);
            previewCamera.transform.position = target + offset;
            previewCamera.transform.LookAt(target);
            page.Refresh(User, look, Selected);
        }

        public bool InRange(PlayerController p) => World.Flat(p.transform.position - transform.position).magnitude <= useRange;

        public void Open(PlayerController p)
        {
            Close();
            User = p;
            p.Frozen = true;
            p.FaceTowards(Vector3.back); // towards the camera, so the mirror sees your face
            look = p.Look;
            Selected = Row.Colour;
            spin = 0f;
            previewCamera.enabled = true;
            page.Show(previewTexture);
            page.Refresh(p, look, Selected);
            Sfx.Play(Sound.Door, transform.position, 0.6f, 1.3f);
        }

        public void Close()
        {
            if (!User) return;
            Session.Looks[User.Index] = look;
            User.Frozen = false;
            User = null;
            previewCamera.enabled = false;
            page.Hide();
            Sfx.Play(Sound.Placed, transform.position, 0.6f);
        }

        public void Select(Row row) => Selected = row;

        void Step(int d)
        {
            Selected = (Row)Looks.Wrap((int)Selected + d, 6);
            Sfx.Play(Sound.SpellAdd, transform.position);
        }

        /// <summary>Changes the selected row's option and puts it on straight away.</summary>
        public void Change(int d)
        {
            switch (Selected)
            {
                case Row.Colour: look.colour = Looks.Wrap(look.colour + d, Looks.Colours.Length); break;
                case Row.Letter: look.initial = (char)('A' + Looks.Wrap(look.initial - 'A' + d, 26)); break;
                case Row.Hat: look.hat = Looks.Wrap(look.hat + d, Looks.Hats.Length); break;
                case Row.Extra: look.extra = Looks.Wrap(look.extra + d, Looks.Extras.Length); break;
                default: return;
            }
            Wear();
            Sfx.Play(Sound.Pickup, transform.position, 0.6f, 1.1f);
        }

        public void Shuffle()
        {
            look = Looks.Random();
            Wear();
            Sfx.Play(Sound.Cast, transform.position, 0.7f);
        }

        void Wear()
        {
            Looks.Apply(User, look);
            Session.Looks[User.Index] = look;
            page.Refresh(User, look, Selected);
        }
    }
}
