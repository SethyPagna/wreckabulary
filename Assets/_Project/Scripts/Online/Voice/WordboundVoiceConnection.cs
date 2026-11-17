using Photon.Voice.Unity;

namespace Wreckabulary.Online
{
    public sealed class WordboundVoiceConnection : VoiceConnection
    {
        internal PhotonVoiceTransport Owner;

        protected override void Update()
        {
            base.Update();
            Owner?.Pump();
        }

        protected override Speaker InstantiateSpeakerForRemoteVoice(int playerId, byte voiceId, object userData) =>
            Owner?.CreateSpeaker(this, userData as string);
    }
}
