using UnityEngine;

[DisallowMultipleComponent]
public class UniqueId : MonoBehaviour
{
    [SerializeField] private string id;
    public string Id => id;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 프리팹 원본에는 ID를 두지 않음 → 씬에 배치된 인스턴스마다 따로 생성
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this) ||
            UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null)
        {
            id = string.Empty;
            return;
        }

        if (string.IsNullOrEmpty(id) || HasDuplicate())
        {
            id = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }

    private bool HasDuplicate()
    {
        foreach (UniqueId other in FindObjectsByType<UniqueId>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other != this && other.id == id && other.gameObject.scene == gameObject.scene)
                return true;
        }
        return false;
    }
#endif
}