namespace Unidad.Audio
{
    /// <summary>
    /// Sound by id. Engine-free: every verb publishes an event the adapter's SoundHost plays. An id without a row is logged once
    /// and stays silent; a silent row plays nothing.
    /// </summary>
    public interface ISoundService
    {
        /// <summary>On the bus its row names.</summary>
        void Play(SoundId id);

        void PlaySfx(SoundId id);

        void PlayUi(SoundId id);

        void PlayMusic(MusicId id);

        void StopMusic();

        void PlayVoice(VoiceBlip blip);

        void StopVoice();

        MusicId? CurrentMusic { get; }
    }
}
