using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

public class SettingsInitializer : MonoBehaviour
{
    [SerializeField] private SettingsView settingsView;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private AudioMixer audioMixer;

    private SettingsPresenter presenter;

    private void Start()
    {
        SettingsModel model = new SettingsModel();
        presenter = new SettingsPresenter(model, settingsView, inputActions, audioMixer);
        // 패널 표시/숨김은 PauseMenuController가 담당
    }

    // 볼륨 조절 직후 0.5초 안에 게임을 종료해도 저장되도록
    private void OnApplicationQuit()
    {
        presenter?.FlushSave();
    }
}