using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveSlotUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI slotNameText;
    [SerializeField] private TextMeshProUGUI infoText;

    [Header("삭제")]
    [SerializeField] private Button deleteButton;
    [SerializeField] private TextMeshProUGUI deleteButtonText;

    public event Action Selected;
    public event Action DeleteConfirmed;

    private bool awaitingDeleteConfirm;

    private void Awake()
    {
        button.onClick.AddListener(() => Selected?.Invoke());
        deleteButton.onClick.AddListener(OnDeleteClicked);
    }

    public void Display(int slot, SaveData data)
    {
        slotNameText.text = $"SLOT {slot + 1}";

        if (data == null)
        {
            infoText.text = "새 게임";
        }
        else
        {
            TimeSpan time = TimeSpan.FromSeconds(data.playTime);
            infoText.text = $"{data.sceneName}\n" +
                            $"{data.SavedAt:yyyy-MM-dd HH:mm}\n" +
                            $"플레이 시간 {(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        }

        deleteButton.gameObject.SetActive(data != null);
        ResetDeleteConfirm();
    }

    // 실수로 지우지 않도록 두 번 눌러야 삭제
    private void OnDeleteClicked()
    {
        if (!awaitingDeleteConfirm)
        {
            awaitingDeleteConfirm = true;
            deleteButtonText.text = "정말 삭제하시겠습니까?";
            return;
        }

        ResetDeleteConfirm();
        DeleteConfirmed?.Invoke();
    }

    private void ResetDeleteConfirm()
    {
        awaitingDeleteConfirm = false;
        deleteButtonText.text = "삭제";
    }
}