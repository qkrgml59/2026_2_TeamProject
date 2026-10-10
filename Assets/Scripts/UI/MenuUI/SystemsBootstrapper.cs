using UnityEngine;

public class SystemsBootstrapper
{
    private const string PrefabPath = "Prefabs/Managers/Systems"; // Assets/Resources/Prefab/Systems.prefab


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (GameManager.HasInstance) return;

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[Bootstrapper] Resources/{PrefabPath} 프리팹을 찾을 수 없습니다.");
            return;
        }

        GameObject systems = Object.Instantiate(prefab);
        systems.name = prefab.name;
    }
}
