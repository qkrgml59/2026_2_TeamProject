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

    public void OpenSettings()
    {
        settingsView.Show();
    }

    public void CloseSettings()
    {
        if (settingsView.gameObject.activeInHierarchy)
        {
            settingsView.Hide();
        }
    }
}
