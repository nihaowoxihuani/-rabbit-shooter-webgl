using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Sound Effects")]
    [SerializeField] private AudioClip startClip;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip loseClip;
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private AudioClip playerHitClip;
    [SerializeField] private AudioClip enemyHitClip;
    [SerializeField] private AudioClip pickupClip;

    [Header("Background Music")]
    [SerializeField] private AudioClip bgmClip;

    const int sampleRate = 44100;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureAudioSources();
        LoadAudioClips();
    }

    void EnsureAudioSources()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
    }

    void LoadAudioClips()
    {
        if (startClip == null) startClip = Resources.Load<AudioClip>("Audio/Start");
        if (winClip == null) winClip = Resources.Load<AudioClip>("Audio/Win");
        if (loseClip == null) loseClip = Resources.Load<AudioClip>("Audio/Lose");
        if (shootClip == null) shootClip = Resources.Load<AudioClip>("Audio/Shoot");
        if (playerHitClip == null) playerHitClip = Resources.Load<AudioClip>("Audio/PlayerHit");
        if (enemyHitClip == null) enemyHitClip = Resources.Load<AudioClip>("Audio/EnemyHit");
        if (pickupClip == null) pickupClip = Resources.Load<AudioClip>("Audio/Pickup");
        if (bgmClip == null) bgmClip = Resources.Load<AudioClip>("Audio/BGM");

        // 如果没有外部音频文件，使用程序生成的音效
        if (startClip == null) startClip = GenerateStartClip();
        if (winClip == null) winClip = GenerateWinClip();
        if (loseClip == null) loseClip = GenerateLoseClip();
        if (shootClip == null) shootClip = GenerateShootClip();
        if (playerHitClip == null) playerHitClip = GeneratePlayerHitClip();
        if (enemyHitClip == null) enemyHitClip = GenerateEnemyHitClip();
        if (pickupClip == null) pickupClip = GeneratePickupClip();
        if (bgmClip == null) bgmClip = GenerateBGMClip();
    }

    #region Procedural Audio Generation

    AudioClip CreateClip(int samples, bool stream = false)
    {
        return AudioClip.Create("Generated", samples, 1, sampleRate, stream);
    }

    float[] GenerateSineWave(float frequency, float duration, float volume, float attack = 0.01f, float decay = 0.1f)
    {
        int samples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[samples];
        int attackSamples = Mathf.RoundToInt(sampleRate * attack);
        int decaySamples = Mathf.RoundToInt(sampleRate * decay);

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float env = 1f;
            if (i < attackSamples)
                env = i / (float)attackSamples;
            else if (i > samples - decaySamples)
                env = (samples - i) / (float)decaySamples;

            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * env;
        }
        return data;
    }

    float[] GenerateSquareWave(float frequency, float duration, float volume, float attack = 0.01f, float decay = 0.1f)
    {
        int samples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[samples];
        int attackSamples = Mathf.RoundToInt(sampleRate * attack);
        int decaySamples = Mathf.RoundToInt(sampleRate * decay);

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float env = 1f;
            if (i < attackSamples)
                env = i / (float)attackSamples;
            else if (i > samples - decaySamples)
                env = (samples - i) / (float)decaySamples;

            float val = Mathf.Sin(2f * Mathf.PI * frequency * t);
            data[i] = (val >= 0 ? 1f : -1f) * volume * env;
        }
        return data;
    }

    float[] GenerateNoise(float duration, float volume)
    {
        int samples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[samples];
        int decaySamples = Mathf.RoundToInt(sampleRate * duration);

        for (int i = 0; i < samples; i++)
        {
            float env = (samples - i) / (float)decaySamples;
            data[i] = (Random.value * 2f - 1f) * volume * env;
        }
        return data;
    }

    AudioClip GenerateShootClip()
    {
        float duration = 0.1f;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = GenerateNoise(duration, 0.4f);
        AudioClip clip = CreateClip(samples);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GeneratePlayerHitClip()
    {
        float duration = 0.25f;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = GenerateSquareWave(120f, duration, 0.5f, 0.01f, 0.2f);
        AudioClip clip = CreateClip(samples);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateEnemyHitClip()
    {
        float duration = 0.15f;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = GenerateSineWave(500f, duration, 0.4f, 0.01f, 0.1f);
        AudioClip clip = CreateClip(samples);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GeneratePickupClip()
    {
        float duration = 0.2f;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float freq = Mathf.Lerp(880f, 1320f, t / duration);
            float env = 1f - (i / (float)samples);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f * env;
        }

        AudioClip clip = CreateClip(samples);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateStartClip()
    {
        float noteDuration = 0.15f;
        float[] notes = new float[] { 523f, 659f };
        int totalSamples = Mathf.RoundToInt(sampleRate * noteDuration * notes.Length);
        float[] data = new float[totalSamples];

        for (int n = 0; n < notes.Length; n++)
        {
            int offset = Mathf.RoundToInt(sampleRate * noteDuration * n);
            float[] note = GenerateSineWave(notes[n], noteDuration, 0.4f, 0.01f, 0.05f);
            for (int i = 0; i < note.Length && offset + i < totalSamples; i++)
            {
                data[offset + i] += note[i];
            }
        }

        AudioClip clip = CreateClip(totalSamples);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateWinClip()
    {
        float noteDuration = 0.2f;
        float[] notes = new float[] { 523f, 659f, 784f };
        int totalSamples = Mathf.RoundToInt(sampleRate * noteDuration * notes.Length);
        float[] data = new float[totalSamples];

        for (int n = 0; n < notes.Length; n++)
        {
            int offset = Mathf.RoundToInt(sampleRate * noteDuration * n);
            float[] note = GenerateSineWave(notes[n], noteDuration, 0.4f, 0.01f, 0.05f);
            for (int i = 0; i < note.Length && offset + i < totalSamples; i++)
            {
                data[offset + i] += note[i];
            }
        }

        AudioClip clip = CreateClip(totalSamples);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateLoseClip()
    {
        float noteDuration = 0.3f;
        float[] notes = new float[] { 300f, 150f };
        int totalSamples = Mathf.RoundToInt(sampleRate * noteDuration * notes.Length);
        float[] data = new float[totalSamples];

        for (int n = 0; n < notes.Length; n++)
        {
            int offset = Mathf.RoundToInt(sampleRate * noteDuration * n);
            float[] note = GenerateSquareWave(notes[n], noteDuration, 0.4f, 0.01f, 0.15f);
            for (int i = 0; i < note.Length && offset + i < totalSamples; i++)
            {
                data[offset + i] += note[i];
            }
        }

        AudioClip clip = CreateClip(totalSamples);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateBGMClip()
    {
        float loopDuration = 3.2f;
        int samples = Mathf.RoundToInt(sampleRate * loopDuration);
        float[] data = new float[samples];

        float[] bassNotes = new float[] { 65.4f, 98f, 65.4f, 98f };
        float noteLen = loopDuration / bassNotes.Length;

        for (int n = 0; n < bassNotes.Length; n++)
        {
            int offset = Mathf.RoundToInt(sampleRate * noteLen * n);
            int noteSamples = Mathf.RoundToInt(sampleRate * noteLen);
            for (int i = 0; i < noteSamples && offset + i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float env = 1f;
                if (i < sampleRate * 0.05f)
                    env = i / (sampleRate * 0.05f);
                else if (i > noteSamples - sampleRate * 0.2f)
                    env = (noteSamples - i) / (sampleRate * 0.2f);

                float bass = Mathf.Sin(2f * Mathf.PI * bassNotes[n] * t) * 0.25f * env;
                float kick = 0f;
                if (i < sampleRate * 0.05f)
                    kick = Mathf.Sin(2f * Mathf.PI * 60f * t) * (1f - i / (sampleRate * 0.05f)) * 0.3f;

                data[offset + i] += bass + kick;
            }
        }

        AudioClip clip = CreateClip(samples, true);
        clip.SetData(data, 0);
        return clip;
    }

    #endregion

    public void PlayStartSound()
    {
        PlaySFX(startClip);
    }

    public void PlayWinSound()
    {
        PlaySFX(winClip);
        StopBGM();
    }

    public void PlayLoseSound()
    {
        PlaySFX(loseClip);
        StopBGM();
    }

    public void PlayShootSound()
    {
        PlaySFX(shootClip);
    }

    public void PlayPlayerHitSound()
    {
        PlaySFX(playerHitClip);
    }

    public void PlayEnemyHitSound()
    {
        PlaySFX(enemyHitClip);
    }

    public void PlayPickupSound()
    {
        PlaySFX(pickupClip);
    }

    public void PlayBGM()
    {
        if (bgmClip != null && bgmSource != null && !bgmSource.isPlaying)
        {
            bgmSource.clip = bgmClip;
            bgmSource.volume = 0.4f;
            bgmSource.Play();
        }
    }

    public void StopBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }
    }

    void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, 0.6f);
        }
    }

    public static void ResetInstance()
    {
        if (Instance != null)
        {
            Instance.StopBGM();
            Destroy(Instance.gameObject);
            Instance = null;
        }
    }
}
