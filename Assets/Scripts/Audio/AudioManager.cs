using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Sound Database")]
    public SoundDefinition[] sounds;

    [Header("Audio Sources")]
    public int sfxPoolSize = 10;

    [Header("Audio Mixer")]
    public AudioMixer masterMixer;
    
    // Exposed parameter names in your Audio Mixer
    [Header("Mixer Parameter Names")]
    public string masterVolumeParam = "MasterVolume";
    public string musicVolumeParam = "MusicVolume";
    public string sfxVolumeParam = "SFXVolume";

    private Dictionary<SoundID, SoundDefinition> soundLookup;
    private List<AudioSource> sfxSources;
    private AudioSource musicSource;
    private AudioSource ambientSource;

    // PlayerPrefs keys for saving volume settings
    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        soundLookup = new Dictionary<SoundID, SoundDefinition>();
        foreach (var s in sounds)
            soundLookup[s.id] = s;

        CreateSources();
        LoadVolumeSettings();
    }

    void CreateSources()
    {
        sfxSources = new List<AudioSource>();

        for (int i = 0; i < sfxPoolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            sfxSources.Add(src);
        }

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false; // IMPORTANT: Don't play on awake

        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.loop = true;
        ambientSource.playOnAwake = false; // IMPORTANT: Don't play on awake
    }

    // ------------------ PUBLIC API ------------------

    public void PlaySFX(SoundID id, Vector3? position = null)
    {
        if (!soundLookup.TryGetValue(id, out var sound))
            return;

        var source = GetFreeSFXSource();
        ConfigureSource(source, sound);

        if (position.HasValue)
        {
            source.transform.position = position.Value;
            source.spatialBlend = 1f;
        }
        else
        {
            source.spatialBlend = 0f;
        }

        source.Play();
    }

    public void PlayMusic(SoundID id)
    {
        // CRITICAL FIX: Stop current music before playing new music
        if (musicSource.isPlaying)
        {
            musicSource.Stop();
            Debug.Log($"AudioManager: Stopped previous music");
        }
        
        PlayLoop(musicSource, id);
        Debug.Log($"AudioManager: NOW PLAYING MUSIC - {id}\nStack Trace: {System.Environment.StackTrace}");
    }

    public void PlayAmbient(SoundID id)
    {
        // Stop current ambient before playing new ambient
        if (ambientSource.isPlaying)
        {
            ambientSource.Stop();
        }
        
        PlayLoop(ambientSource, id);
    }

    public void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop();
            Debug.Log("AudioManager: Music stopped");
        }
    }

    public void StopAmbient()
    {
        if (ambientSource != null && ambientSource.isPlaying)
        {
            ambientSource.Stop();
        }
    }

    // ------------------ VOLUME CONTROL ------------------

    /// <summary>
    /// Set master volume (0-1 range from slider)
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        float dbVolume = ConvertToDecibels(volume);
        masterMixer.SetFloat(masterVolumeParam, dbVolume);
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, volume);
    }

    /// <summary>
    /// Set music volume (0-1 range from slider)
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        float dbVolume = ConvertToDecibels(volume);
        masterMixer.SetFloat(musicVolumeParam, dbVolume);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, volume);
    }

    /// <summary>
    /// Set SFX volume (0-1 range from slider)
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        float dbVolume = ConvertToDecibels(volume);
        masterMixer.SetFloat(sfxVolumeParam, dbVolume);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, volume);
    }

    /// <summary>
    /// Get current master volume (0-1 range)
    /// </summary>
    public float GetMasterVolume()
    {
        return PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, 1f);
    }

    /// <summary>
    /// Get current music volume (0-1 range)
    /// </summary>
    public float GetMusicVolume()
    {
        return PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 1f);
    }

    /// <summary>
    /// Get current SFX volume (0-1 range)
    /// </summary>
    public float GetSFXVolume()
    {
        return PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
    }

    /// <summary>
    /// Load saved volume settings from PlayerPrefs
    /// </summary>
    private void LoadVolumeSettings()
    {
        float master = GetMasterVolume();
        float music = GetMusicVolume();
        float sfx = GetSFXVolume();

        SetMasterVolume(master);
        SetMusicVolume(music);
        SetSFXVolume(sfx);

        Debug.Log($"AudioManager: Loaded volumes - Master: {master:F2}, Music: {music:F2}, SFX: {sfx:F2}");
    }

    /// <summary>
    /// Convert linear volume (0-1) to decibels (-80 to 0)
    /// Audio mixers work in decibels, not linear scale
    /// </summary>
    private float ConvertToDecibels(float linearVolume)
    {
        // Clamp to prevent log of zero
        linearVolume = Mathf.Clamp(linearVolume, 0.0001f, 1f);
        
        // Convert to decibels: 20 * log10(volume)
        // Range: -80db (silent) to 0db (full volume)
        float db = 20f * Mathf.Log10(linearVolume);
        
        // Clamp to reasonable range
        return Mathf.Clamp(db, -80f, 0f);
    }

    // ------------------ INTERNAL ------------------

    AudioSource GetFreeSFXSource()
    {
        foreach (var s in sfxSources)
            if (!s.isPlaying)
                return s;

        return sfxSources[0]; // fallback (never silent)
    }

    void PlayLoop(AudioSource source, SoundID id)
    {
        if (!soundLookup.TryGetValue(id, out var sound))
        {
            Debug.LogWarning($"AudioManager: Sound '{id}' not found in soundLookup!");
            return;
        }

        ConfigureSource(source, sound);
        source.loop = true;
        source.Play();
    }

    void ConfigureSource(AudioSource source, SoundDefinition sound)
    {
        source.clip = sound.clip;
        source.volume = sound.volume;

        float pitch = sound.pitch;
        if (sound.randomPitch)
            pitch += Random.Range(-sound.randomPitchRange, sound.randomPitchRange);

        source.pitch = pitch;
        source.outputAudioMixerGroup = sound.mixerGroup;
    }
}