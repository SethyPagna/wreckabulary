using UnityEngine;
using Wreckabulary.Rules;
using System.Linq;

namespace Wreckabulary
{
    /// <summary>Mostly fixed couch-game camera that drifts a little towards the action and shakes on big hits.</summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        [SerializeField] float follow = 0.12f;
        [SerializeField] float soloDistance = .5f;
        [SerializeField] float soloFieldOfView = 40f;

        Vector3 basePosition, smooth;
        float shake;
        bool framesLayout;
        Vector3 layoutCentre;
        float wholeHouseSize;
        float layoutWidth, layoutDepth;
        Camera lens;

        public void FrameLayout(HouseLayout layout)
        {
            var camera = lens = GetComponent<Camera>();
            float minX = layout.Rooms.Min(r => r.MinX), maxX = layout.Rooms.Max(r => r.MaxX);
            float minZ = layout.Rooms.Min(r => r.MinZ), maxZ = layout.Rooms.Max(r => r.MaxZ);
            var centre = new Vector3((minX + maxX) * .5f, 0f, (minZ + maxZ) * .5f);
            layoutWidth = maxX - minX;
            layoutDepth = maxZ - minZ;
            camera.orthographic = true;
            float aspect = Mathf.Max(.5f, camera.aspect);
            camera.orthographicSize = HouseSize(aspect);
            layoutCentre = centre;
            wholeHouseSize = camera.orthographicSize;
            basePosition = smooth = centre + new Vector3(0f, 30f, -22f);
            transform.position = basePosition;
            transform.LookAt(centre);
            framesLayout = true;
        }

        void Awake()
        {
            Instance = this;
            basePosition = smooth = transform.position;
            // The browser edition's deep teal surrounds the house, so the edges blend into the HUD instead of a void.
            if (TryGetComponent<Camera>(out var camera)) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(0x17, 0x3a, 0x3d, 0xff); }
        }

        /// <summary>Orthographic size that shows the whole house with a slim margin.</summary>
        float HouseSize(float aspect) => Mathf.Max(layoutWidth / aspect, layoutDepth * .85f) * .5f + .8f;

        public static void Shake(float amount)
        {
            if (Instance) Instance.shake = Mathf.Max(Instance.shake, amount);
        }

        void LateUpdate()
        {
            var centre = Vector3.zero;
            int n = 0;
            int localCount = 0;
            PlayerController localPlayer = null;
            Vector3 low = Vector3.positiveInfinity, high = Vector3.negativeInfinity;
            foreach (var p in World.Players)
            {
                if (!p || p.IsEliminated) continue;
                centre += p.transform.position;
                n++;
                if (p.Binding is not BotBinding)
                {
                    localCount++; localPlayer = p;
                    low = Vector3.Min(low, p.transform.position); high = Vector3.Max(high, p.transform.position);
                }
            }
            var target = basePosition;
            if (framesLayout)
            {
                // One local player gets the browser edition's close follow camera. Couch players share a view
                // that frames them all, close when they're together and never wider than the whole house.
                bool followLocal = localCount == 1;
                float aspect = lens ? Mathf.Max(.5f, lens.aspect) : 16f / 9f;
                wholeHouseSize = HouseSize(aspect);
                var focus = followLocal ? localPlayer.transform.position : layoutCentre;
                float size = wholeHouseSize;
                if (localCount > 1)
                {
                    var spread = high - low;
                    // Room for the HUD columns and a step of space around everyone.
                    size = Mathf.Clamp(Mathf.Max((spread.x + 9f) / aspect, (spread.z + 7f) * .85f) * .5f + 1f, 7.5f, wholeHouseSize);
                    var middle = (low + high) * .5f;
                    // Near the whole-house size, settle on the house centre so the edges don't wobble.
                    focus = Vector3.Lerp(middle, layoutCentre, Mathf.InverseLerp(wholeHouseSize * .7f, wholeHouseSize, size));
                }
                focus.y = 0f;
                // Solo play gets a closer perspective view along the same direction, so
                // world-aligned controls and pointer aim read the same as the couch view.
                target = focus + new Vector3(0f, 30f, -22f) * (followLocal ? soloDistance : 1f);
                if (lens)
                {
                    lens.orthographic = !followLocal;
                    if (followLocal) lens.fieldOfView = soloFieldOfView;
                    lens.orthographicSize = Mathf.Lerp(lens.orthographicSize, size,
                        1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
                }
            }
            else if (n > 0)
            {
                centre /= n;
                target += new Vector3(centre.x * follow, 0f, centre.z * follow * 0.6f);
            }
            smooth = Vector3.Lerp(smooth, target, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            transform.position = smooth + Random.insideUnitSphere * shake;
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.5f);
        }
    }
}
