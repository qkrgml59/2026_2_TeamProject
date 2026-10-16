using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary> 공용 확인 팝업. 취소 / ESC → 닫기, 확인 → 닫은 뒤 콜백 실행 </summary>
public class ConfirmPopup : UIPanel
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action onConfirm;

    private void Awake()
    {
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(RequestClose);
    }

    public void Setup(string message, Action onConfirm)
    {
        messageText.text = message;
        this.onConfirm = onConfirm;
    }

    private void Confirm()
    {
        Action action = onConfirm;
        onConfirm = null;

        RequestClose();
        action?.Invoke();
    }
}