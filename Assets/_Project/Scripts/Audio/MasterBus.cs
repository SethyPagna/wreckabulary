using UnityEngine;
using Wreckabulary.Rules.Sound;

namespace Wreckabulary.Audio
{
    [DisallowMultipleComponent]
    public sealed class MasterBus : MonoBehaviour
    {
        static volatile MixRecorder recorder;
        readonly MasterDynamics dynamics = new MasterDynamics();
        long blocks;

        public static MixRecorder Recorder { get => recorder; set => recorder = value; }
        public MasterDynamics Dynamics => dynamics;
        public long Blocks => System.Threading.Interlocked.Read(ref blocks);

        public float Duck
        {
            get => dynamics.Duck;
            set => dynamics.Duck = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => recorder = null;

        void Awake() => dynamics.Configure(AudioSettings.outputSampleRate);

        void OnEnable() => AudioSettings.OnAudioConfigurationChanged += Reconfigure;

        void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= Reconfigure;

        void Reconfigure(bool deviceChanged) => dynamics.Configure(AudioSettings.outputSampleRate);

        void OnAudioFilterRead(float[] data, int channels)
        {
            dynamics.Process(data, channels);
            recorder?.Write(data, channels);
            System.Threading.Interlocked.Increment(ref blocks);
        }
    }
}
