using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SettingsView : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
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
    [SerializeField] private Button applyButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private CanvasGroup canvasGroup;


    public event Action<int> OnResolutionChanged;
    public event Action<bool> OnFullscreenChanged;
    public event Action<float> OnMasterVolumeChanged;
    public event Action<float> OnBGMVolumeChanged;
    public event Action<float> OnSFXVolumeChanged;
    public event Action OnApplyClicked;
    public event Action OnCloseClicked;

    private bool isInitializing = false;

    private void Awake()
    {
        resolutionDropdown.onValueChanged.AddListener(val => { if (!isInitializing) OnResolutionChanged?.Invoke(val); });
        fullscreenToggle.onValueChanged.AddListener(val => { if (!isInitializing) OnFullscreenChanged?.Invoke(val); });

        masterVolumeSlider.onValueChanged.AddListener(val => { if (!isInitializing) OnMasterVolumeChanged?.Invoke(val); });
        bgmVolumeSlider.onValueChanged.AddListener(val => { if (!isInitializing) OnBGMVolumeChanged?.Invoke(val); });
        sfxVolumeSlider.onValueChanged.AddListener(val => { if (!isInitializing) OnSFXVolumeChanged?.Invoke(val); });

        applyButton.onClick.AddListener(() => OnApplyClicked?.Invoke());
        closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());
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
        {
            resolutionDropdown.value = resIndex;
        }

        fullscreenToggle.isOn = isFull;
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

    public void Show()
    {
        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        transform.localScale = Vector3.one * .9f;

        canvasGroup.DOFade(1f, .25f);
        transform.DOScale(1f, .25f).SetEase(Ease.OutBack);
    }

    public void Hide()
    {
        canvasGroup.DOFade(0f, .2f);
        transform.DOScale(.9f, .2f).SetEase(Ease.InBack).OnComplete(() => gameObject.SetActive(false));
    }
}
