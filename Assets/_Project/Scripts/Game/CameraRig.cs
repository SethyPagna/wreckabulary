using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Mostly fixed couch-game camera that drifts a little towards the action and shakes on big hits.</summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        [SerializeField] float follow = 0.12f;

        Vector3 basePosition, smooth;
        float shake;

        void Awake()
        {
            Instance = this;
            basePosition = smooth = transform.position;
        }

        public static void Shake(float amount)
        {
            if (Instance) Instance.shake = Mathf.Max(Instance.shake, amount);
        }

        void LateUpdate()
        {
            var centre = Vector3.zero;
            int n = 0;
            foreach (var p in World.Players)
            {
                if (!p || p.IsKnockedOut) continue;
                centre += p.transform.position;
                n++;
            }
            var target = basePosition;
            if (n > 0)
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
