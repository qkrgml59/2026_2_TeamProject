using System;
using UnityEngine;
using UnityEngine.UI;

public class ProfileSelectView : UIPanel
{
    [SerializeField] private SaveSlotUI[] slots = new SaveSlotUI[SaveManager.SlotCount];
    [SerializeField] private Button closeButton;

    /// <summary> 슬롯 선택됨 (새 게임 / 이어하기는 SaveManager가 판단) </summary>
    public event Action<int> ProfileSelected;

    private void Awake()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            int slot = i;
            slots[i].Selected += () => ProfileSelected?.Invoke(slot);
            slots[i].DeleteConfirmed += () =>
            {
                SaveManager.Instance.Delete(slot);
                Refresh();
            };
        }

        closeButton.onClick.AddListener(RequestClose);
    }

    public override void Show()
    {
        Refresh();
        base.Show();
    }

    private void Refresh()
    {
        for (int i = 0; i < slots.Length; i++)
            slots[i].Display(i, SaveManager.Instance.PeekSlot(i));
    }
}