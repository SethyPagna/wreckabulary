using UnityEngine;

namespace Wreckabulary.Art
{
    [DisallowMultipleComponent]
    public sealed class BackdropLife : MonoBehaviour
    {
        const float PollSeconds = .5f, WindRiseSeconds = 1.5f;

        BackdropMover[] movers = System.Array.Empty<BackdropMover>();
        float nextPoll = float.NegativeInfinity, wind;
        bool allowed;

        void OnEnable()
        {
            movers = GetComponentsInChildren<BackdropMover>(true);
            foreach (var mover in movers) mover.Prepare();
            nextPoll = float.NegativeInfinity;
            wind = 0f;
            BackdropShader.SetWind(0f);
        }

        void OnDisable() => BackdropShader.SetWind(wind = 0f);

        void Update() => Step(Time.deltaTime, Time.unscaledTime);

        void Step(float deltaTime, float now)
        {
            if (now >= nextPoll)
            {
                allowed = BackdropShader.MotionAllowed;
                nextPoll = now + PollSeconds;
            }
            float target = allowed ? Mathf.MoveTowards(wind, 1f, Mathf.Max(0f, deltaTime) / WindRiseSeconds) : 0f;
            if (target != wind) BackdropShader.SetWind(wind = target);
            if (!allowed || deltaTime <= 0f) return;
            for (int i = 0; i < movers.Length; i++)
            {
                var mover = movers[i];
                if (mover && mover.isActiveAndEnabled) mover.Advance(deltaTime);
            }
        }
    }
}
