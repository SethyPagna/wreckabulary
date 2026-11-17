using Photon.Voice.Unity;
using UnityEngine;

namespace Wreckabulary.Online
{
    public sealed class WordboundVoiceTuning : MonoBehaviour
    {
        public int Fec = PhotonVoiceTransport.FecInterval;
        public int AppliedFec { get; private set; }

        void PhotonVoiceCreated(PhotonVoiceCreatedParams created)
        {
            if (created?.Voice == null) return;
            created.Voice.FEC = Fec;
            AppliedFec = created.Voice.FEC;
        }
    }
}
