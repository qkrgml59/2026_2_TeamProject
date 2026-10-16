using UnityEngine;
using UnityEngine.Events;

/// <summary> 보스·퍼즐: 1회 클리어 시 영구적으로 클리어 상태 유지 </summary>
[RequireComponent(typeof(UniqueId))]
public class PermanentClear : MonoBehaviour
{
    public enum Category { Boss, Gimmick }

    [SerializeField] private Category category = Category.Gimmick;

    [Tooltip("비워두면 UniqueId 사용. 다른 곳에서 조회할 보스는 이름을 지정 (예: ForestBoss)")]
    [SerializeField] private string customKey;

    [Header("이벤트")]
    [Tooltip("씬 진입 시 이미 클리어된 상태 (예: 문 열린 상태로)")]
    [SerializeField] private UnityEvent onAlreadyCleared;

    [Tooltip("이번에 처음 클리어 (예: 문 열리는 연출)")]
    [SerializeField] private UnityEvent onCleared;

    private string FlagKey
    {
        get
        {
            string prefix = category == Category.Boss ? SaveKeys.BossPrefix : SaveKeys.GimmickPrefix;
            string id = string.IsNullOrEmpty(customKey)
                ? $"{gameObject.scene.name}/{GetComponent<UniqueId>().Id}"
                : customKey;
            return prefix + id;
        }
    }

    public bool IsCleared => SaveManager.Instance.Progress.HasFlag(FlagKey);

    private void Start()
    {
        // 불러온 진행도는 Start 이전에 적용되므로 여기서 확인
        if (IsCleared)
            onAlreadyCleared.Invoke();
    }

    /// <summary> 보스 처치 / 퍼즐 해결 시 호출 </summary>
    public void MarkCleared()
    {
        if (IsCleared) return;

        SaveManager.Instance.Progress.SetFlag(FlagKey);
        onCleared.Invoke();
    }
}