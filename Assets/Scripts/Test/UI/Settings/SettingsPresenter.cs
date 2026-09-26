using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

public class SettingsPresenter
{
    private SettingsModel model;
    private SettingsView view;

    private InputActionAsset inputActions;
    private AudioMixer audioMixer;
    private Resolution[] availableResolutions;

    public SettingsPresenter(SettingsModel model, SettingsView view, InputActionAsset inputActions, AudioMixer audioMixer)
    {
        this.model = model;
        this.view = view;
        this.inputActions = inputActions;
        this.audioMixer = audioMixer;

        // Display
        view.OnResolutionChanged += HandleResolutionChanged;
        view.OnFullscreenChanged += HandleFullscreenChanged;

        // Audio
        view.OnMasterVolumeChanged += HandleMasterVolumeChanged;
        view.OnBGMVolumeChanged += HandleBGMVolumeChanged;
        view.OnSFXVolumeChanged += HandleSFXVolumeChanged;

        // KeyBindings

        // System
        view.OnApplyClicked += HandleApplyClicked;
        view.OnCloseClicked += HandleCloseClicked;                                         // (기존 코드 생략)
    }


    private void HandleResolutionChanged(int index) { model.resolutionIndex = index; }
    private void HandleFullscreenChanged(bool isFull) { model.isFullscreen = isFull; }

    private void HandleMasterVolumeChanged(float volume)
    {
        model.masterVolume = volume;
        view.UpdateVolumeUI(model.masterVolume, model.bgmVolume, model.sfxVolume);
        ApplyAudioMixer("MasterVolumeParam", volume);
    }

    private void HandleBGMVolumeChanged(float volume)
    {
        model.bgmVolume = volume;
        view.UpdateVolumeUI(model.masterVolume, model.bgmVolume, model.sfxVolume);
        ApplyAudioMixer("BGMVolumeParam", volume);
    }

    private void HandleSFXVolumeChanged(float volume)
    {
        model.sfxVolume = volume;
        view.UpdateVolumeUI(model.masterVolume, model.bgmVolume, model.sfxVolume);
        ApplyAudioMixer("SFXVolumeParam", volume);
    }

    private void HandleApplyClicked()
    {
        model.SaveSettings();

        Resolution res = availableResolutions[model.resolutionIndex];
        Screen.SetResolution(res.width, res.height, model.isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

        Debug.Log("설정 적용 및 저장 완료");
    }

    private void HandleCloseClicked()
    {
        view.Hide();
    }


    private void ApplyAudioMixer(string paramName, float volume)
    {
        if (volume <= 0.0001f) volume = 0.0001f;
        float dbVolume = Mathf.Log10(volume) * 20;
        audioMixer.SetFloat(paramName, dbVolume);
    }
}
