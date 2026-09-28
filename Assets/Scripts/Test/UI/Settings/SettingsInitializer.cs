using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

public class SettingsInitializer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SettingsView settingsView;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private AudioMixer audioMixer;

    private SettingsPresenter presenter;

    private void Start()
    {
        SettingsModel model = new SettingsModel();

        presenter = new SettingsPresenter(model, settingsView, inputActions, audioMixer);

        settingsView.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 설정 체크용 임시
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleSettings();
        }
    }

    public void ToggleSettings()
    {
        if (settingsView.gameObject.activeInHierarchy)
        {
            settingsView.Hide();
        }
        else
        {
            settingsView.Show();
        }
    }

    public void OpenSettings()
    {
        if (!settingsView.gameObject.activeInHierarchy)
            settingsView.Show();
    }

    public void CloseSettings()
    {
        if (settingsView.gameObject.activeInHierarchy)
            settingsView.Hide();
    }
}
