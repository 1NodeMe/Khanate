using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeSettings : MonoBehaviour
{
    public AudioMixer mixer;
    public Slider musicSlider, soundSlider, voiceSlider;

    const string MusicKey = "vol_music";
    const string SoundKey = "vol_sound";
    const string VoiceKey = "vol_voice";

    void Start()
    {
        float m = PlayerPrefs.GetFloat(MusicKey, 0.8f);
        float s = PlayerPrefs.GetFloat(SoundKey, 0.8f);
        float v = PlayerPrefs.GetFloat(VoiceKey, 0.8f);
        musicSlider.SetValueWithoutNotify(m);
        soundSlider.SetValueWithoutNotify(s);
        voiceSlider.SetValueWithoutNotify(v);
        Apply("MusicVol", m);
        Apply("SoundVol", s);
        Apply("VoiceVol", v);
        musicSlider.onValueChanged.AddListener(SetMusic);
        soundSlider.onValueChanged.AddListener(SetSound);
        voiceSlider.onValueChanged.AddListener(SetVoice);
    }

    void SetMusic(float v) { Apply("MusicVol", v); PlayerPrefs.SetFloat(MusicKey, v); }
    void SetSound(float s) { Apply("SoundVol", s); PlayerPrefs.SetFloat(SoundKey, s); }
    void SetVoice(float v) { Apply("VoiceVol", v); PlayerPrefs.SetFloat(VoiceKey, v); }

    void Apply(string param, float linear)
    {
        mixer.SetFloat(param, Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20f);
    }
}