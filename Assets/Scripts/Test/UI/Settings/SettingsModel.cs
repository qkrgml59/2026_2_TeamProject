using UnityEngine;

public class SettingsModel
{
    public float masterVolume;
    public float bgmVolume;
    public float sfxVolume;

    public int resolutionIndex;         // 해상도
    public int frameRateIndex = 3;      // 프레임 제한
    public bool isFullscreen;

    public string keyBindingOverrides;  // 키 지정

    public void LoadSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 1f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);

        resolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", -1);
        frameRateIndex = PlayerPrefs.GetInt("FrameRateIndex", 3);
        isFullscreen = PlayerPrefs.GetInt("IsFullscreen", 1) == 1;

        keyBindingOverrides = PlayerPrefs.GetString("KeyBindings", "");
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("BGMVolume", bgmVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);

        PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
        PlayerPrefs.SetInt("FrameRateIndex", frameRateIndex);
        PlayerPrefs.SetInt("IsFullscreen", isFullscreen ? 1 : 0);

        PlayerPrefs.SetString("KeyBindings", keyBindingOverrides);

        PlayerPrefs.Save();
    }
}
