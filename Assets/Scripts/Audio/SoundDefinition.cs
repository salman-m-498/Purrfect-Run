using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "Audio/Sound Definition")]
public class SoundDefinition : ScriptableObject
{
    public SoundID id;
    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Range(0.5f, 2f)]
    public float pitch = 1f;

    public bool loop;
    public AudioMixerGroup mixerGroup;

    public bool randomPitch;
    public float randomPitchRange = 0.1f;
}
