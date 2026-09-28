using System.Collections.Generic;
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
    private List<Vector2Int> uniqueResolutions;      
    private List<RefreshRate> supportedRefreshRates;

    public SettingsPresenter(SettingsModel model, SettingsView view, InputActionAsset inputActions, AudioMixer audioMixer)
    {
        this.model = model;
        this.view = view;
        this.inputActions = inputActions;
        this.audioMixer = audioMixer;

        // Display
        view.OnResolutionChanged += HandleResolutionChanged;
        view.OnFrameRateChanged += HandleFrameRateChanged;
        view.OnFullscreenChanged += HandleFullscreenChanged;

        // Audio
        view.OnMasterVolumeChanged += HandleMasterVolumeChanged;
        view.OnBGMVolumeChanged += HandleBGMVolumeChanged;
        view.OnSFXVolumeChanged += HandleSFXVolumeChanged;

        // KeyBindings

        // System
        view.OnApplyClicked += HandleApplyClicked;
        view.OnCloseClicked += HandleCloseClicked;                                         // (기존 코드 생략)

        InitializeSystem();
    }

    private void InitializeSystem()
    {
        model.LoadSettings();

        uniqueResolutions = new List<Vector2Int>();
        List<string> resOptions = new List<string>();
        int currentResIndex = 0;

        foreach (var res in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(res.width, res.height);
            if (!uniqueResolutions.Contains(size))
            {
                uniqueResolutions.Add(size);
                resOptions.Add($"{size.x} x {size.y}");

                if (model.resolutionIndex == -1 && size.x == Screen.currentResolution.width && size.y == Screen.currentResolution.height)
                {
                    currentResIndex = uniqueResolutions.Count - 1;
                    model.resolutionIndex = currentResIndex;
                }
            }
        }

        if (model.resolutionIndex != -1) currentResIndex = model.resolutionIndex;

        view.InitializeResolutionOptions(resOptions);
        view.UpdateDisplayUI(currentResIndex, model.isFullscreen);


        List<string> frameRateOptions = new List<string> { "30 FPS", "60 FPS", "120 FPS", "무제한" };
        view.InitializeFrameRateOptions(frameRateOptions);
        view.UpdateFrameRateUI(model.frameRateIndex);
        ApplyTargetFrameRate(model.frameRateIndex); 

        view.UpdateVolumeUI(model.masterVolume, model.bgmVolume, model.sfxVolume);
        ApplyAudioMixer("MasterVolumeParam", model.masterVolume);
        ApplyAudioMixer("BGMVolumeParam", model.bgmVolume);
        ApplyAudioMixer("SFXVolumeParam", model.sfxVolume);
    }

    private void HandleResolutionChanged(int index)
    {
        model.resolutionIndex = index;
    }

    private void HandleFrameRateChanged(int index)
    {
        model.frameRateIndex = index;
    }

    private void HandleFullscreenChanged(bool isFull)
    { 
        model.isFullscreen = isFull; 
    }

    private void ApplyTargetFrameRate(int index)
    {
        QualitySettings.vSyncCount = 0;

        int targetFPS = index switch
        {
            0 => 30,
            1 => 60,
            2 => 120,
            _ => -1 // -1은 무제한
        };

        Application.targetFrameRate = targetFPS;
    }

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

        Vector2Int size = uniqueResolutions[model.resolutionIndex];
        Screen.SetResolution(size.x, size.y, model.isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

        ApplyTargetFrameRate(model.frameRateIndex);

        Debug.Log("설정 적용 완료");
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
