using UnityEngine;

public class SettingsModel
{
    public float masterVolume;
    public float bgmVolume;
    public float sfxVolume;
    public int resolutionIndex;         // 해상도
    public bool isFullscreen;
    public string keyBindingOverrides;  // 키 지정

    public void LoadSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 1f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);

        resolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", -1);
        isFullscreen = PlayerPrefs.GetInt("IsFullscreen", 1) == 1;
        keyBindingOverrides = PlayerPrefs.GetString("KeyBindings", "");
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("BGMVolume", bgmVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);

        PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
        PlayerPrefs.SetInt("IsFullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.SetString("KeyBindings", keyBindingOverrides);
        PlayerPrefs.Save();
    }
}
