using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsView : UIPanel
{
    [Header("Display")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown frameRateDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumeText;

    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private TextMeshProUGUI bgmVolumeText;

    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI sfxVolumeText;

    //[Header("Key Binding")]

    [Header("System")]
    [SerializeField] private Button closeButton;

    public event Action<int> OnResolutionChanged;
    public event Action<int> OnFrameRateChanged;
    public event Action<bool> OnFullscreenChanged;

    public event Action<float> OnMasterVolumeChanged;
    public event Action<float> OnBGMVolumeChanged;
    public event Action<float> OnSFXVolumeChanged;

    // 설정창이 닫힐 때 (저장 대기 중인 값 저장용)
    public event Action OnHidden;

    private bool isInitializing = false;

    private void Awake()
    {
        resolutionDropdown.onValueChanged.AddListener(val => { if (!isInitializing) OnResolutionChanged?.Invoke(val); });
        frameRateDropdown.onValueChanged.AddListener(val => { if (!isInitializing) OnFrameRateChanged?.Invoke(val); });
        fullscreenToggle.onValueChanged.AddListener(val => { if (!isInitializing) OnFullscreenChanged?.Invoke(val); });

        masterVolumeSlider.onValueChanged.AddListener(val => { if (!isInitializing) OnMasterVolumeChanged?.Invoke(val); });
        bgmVolumeSlider.onValueChanged.AddListener(val => { if (!isInitializing) OnBGMVolumeChanged?.Invoke(val); });
        sfxVolumeSlider.onValueChanged.AddListener(val => { if (!isInitializing) OnSFXVolumeChanged?.Invoke(val); });

        // 닫기 처리는 패널을 연 쪽(PauseMenuController)이 결정
        // 메인메뉴: 설정창 닫기 / 인게임: 일시정지 메뉴로 돌아가기
        closeButton.onClick.AddListener(RequestClose);
    }

    public override void Hide()
    {
        base.Hide();
        OnHidden?.Invoke();
    }

    public void InitializeResolutionOptions(List<string> options)
    {
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
    }

    public void UpdateDisplayUI(int resIndex, bool isFull)
    {
        isInitializing = true;

        if (resIndex >= 0)
            resolutionDropdown.value = resIndex;

        fullscreenToggle.isOn = isFull;
        isInitializing = false;
    }

    public void InitializeFrameRateOptions(List<string> options)
    {
        frameRateDropdown.ClearOptions();
        frameRateDropdown.AddOptions(options);
    }

    public void UpdateFrameRateUI(int index)
    {
        isInitializing = true;
        frameRateDropdown.value = index;
        isInitializing = false;
    }

    public void UpdateVolumeUI(float master, float bgm, float sfx)
    {
        isInitializing = true;

        masterVolumeSlider.value = master;
        masterVolumeText.text = $"{Mathf.RoundToInt(master * 100)}%";

        bgmVolumeSlider.value = bgm;
        bgmVolumeText.text = $"{Mathf.RoundToInt(bgm * 100)}%";

        sfxVolumeSlider.value = sfx;
        sfxVolumeText.text = $"{Mathf.RoundToInt(sfx * 100)}%";

        isInitializing = false;
    }
}