using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// 불러오기 시에만 복원되는 데이터 (플레이어 위치 등).
/// 퍼즐 오브젝트에는 구현하지 마세요 → 맵에 다시 들어오면 초기화되어야 함.
/// </summary>
public interface ISaveable
{
    string SaveId { get; }
    string CaptureState();
    void RestoreState(string json);
}

/// <summary>
/// 지금 저장해도 안전한지 판단 (공중에 떠 있을 때는 저장 X)
/// </summary>
public interface ISaveCondition
{
    bool IsSafeToSave { get; }
}

[Serializable]
public class SaveEntry
{
    public string id;
    public string json;
}

[Serializable]
public class SaveData
{
    public int version = 3;
    public string sceneName;      // 이어하기 시작 맵
    public string savedAt;
    public float playTime;

    public ProgressData progress = new ProgressData();
    public List<SaveEntry> sessionEntries = new List<SaveEntry>(); // 맵 입구 시점의 플레이어 상태

    public DateTime SavedAt =>
        DateTime.TryParse(savedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime t)
            ? t : DateTime.MinValue;
}