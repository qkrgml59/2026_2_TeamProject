using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

public class SettingsPresenter
{
    // 슬라이더 드래그 중 매 프레임 디스크에 쓰지 않도록, 마지막 변경 후 이 시간이 지나면 저장
    private const float VolumeSaveDelay = 0.5f;

    private SettingsModel model;
    private SettingsView view;

    private InputActionAsset inputActions;
    private AudioMixer audioMixer;

    private List<Vector2Int> uniqueResolutions;

    private Tween pendingSave;

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
        view.OnHidden += FlushSave;

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
            if (uniqueResolutions.Contains(size)) continue;

            uniqueResolutions.Add(size);
            resOptions.Add($"{size.x} x {size.y}");

            if (size.x == Screen.currentResolution.width && size.y == Screen.currentResolution.height)
                currentResIndex = uniqueResolutions.Count - 1;
        }

        // 저장값이 없거나(-1), 모니터가 바뀌어 범위를 벗어나면 현재 해상도로 대체
        if (model.resolutionIndex < 0 || model.resolutionIndex >= uniqueResolutions.Count)
            model.resolutionIndex = currentResIndex;

        view.InitializeResolutionOptions(resOptions);
        view.UpdateDisplayUI(model.resolutionIndex, model.isFullscreen);

        List<string> frameRateOptions = new List<string> { "30 FPS", "60 FPS", "120 FPS", "무제한" };
        view.InitializeFrameRateOptions(frameRateOptions);
        view.UpdateFrameRateUI(model.frameRateIndex);
        ApplyTargetFrameRate(model.frameRateIndex);

        view.UpdateVolumeUI(model.masterVolume, model.bgmVolume, model.sfxVolume);
        ApplyAudioMixer("MasterVolumeParam", model.masterVolume);
        ApplyAudioMixer("BGMVolumeParam", model.bgmVolume);
        ApplyAudioMixer("SFXVolumeParam", model.sfxVolume);
    }

    // ───────── Display: 즉시 적용 + 즉시 저장 ─────────

    private void HandleResolutionChanged(int index)
    {
        model.resolutionIndex = index;
        ApplyDisplay();
        SaveNow();
    }

    private void HandleFullscreenChanged(bool isFull)
    {
        model.isFullscreen = isFull;
        ApplyDisplay();
        SaveNow();
    }

    private void HandleFrameRateChanged(int index)
    {
        model.frameRateIndex = index;
        ApplyTargetFrameRate(index);
        SaveNow();
    }

    private void ApplyDisplay()
    {
        Vector2Int size = uniqueResolutions[model.resolutionIndex];
        Screen.SetResolution(size.x, size.y, model.isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
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

    // ───────── Audio: 즉시 적용 + 지연 저장 ─────────

    private void HandleMasterVolumeChanged(float volume)
    {
        model.masterVolume = volume;
        view.UpdateVolumeUI(model.masterVolume, model.bgmVolume, model.sfxVolume);
        ApplyAudioMixer("MasterVolumeParam", volume);
        ScheduleSave();
    }

    private void HandleBGMVolumeChanged(float volume)
    {
        model.bgmVolume = volume;
        view.UpdateVolumeUI(model.masterVolume, model.bgmVolume, model.sfxVolume);
        ApplyAudioMixer("BGMVolumeParam", volume);
        ScheduleSave();
    }

    private void HandleSFXVolumeChanged(float volume)
    {
        model.sfxVolume = volume;
        view.UpdateVolumeUI(model.masterVolume, model.bgmVolume, model.sfxVolume);
        ApplyAudioMixer("SFXVolumeParam", volume);
        ScheduleSave();
    }

    private void ApplyAudioMixer(string paramName, float volume)
    {
        if (volume <= 0.0001f) volume = 0.0001f;
        float dbVolume = Mathf.Log10(volume) * 20;
        audioMixer.SetFloat(paramName, dbVolume);
    }

    // ───────── 저장 ─────────

    private void ScheduleSave()
    {
        pendingSave?.Kill();
        pendingSave = DOVirtual.DelayedCall(VolumeSaveDelay, () =>
        {
            pendingSave = null;
            model.SaveSettings();
        }).SetUpdate(true); // 일시정지(timeScale 0) 중에도 타이머 진행
    }

    /// <summary>저장 대기 중인 변경이 있으면 즉시 저장 (설정창 닫기, 게임 종료 시)</summary>
    public void FlushSave()
    {
        if (pendingSave != null)
            SaveNow();
    }

    private void SaveNow()
    {
        pendingSave?.Kill();
        pendingSave = null;
        model.SaveSettings();
    }
}