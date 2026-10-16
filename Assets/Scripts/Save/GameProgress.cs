using System;
using System.Collections.Generic;

[Serializable]
public class IntValue
{
    public string key;
    public int value;
}

[Serializable]
public class ProgressData
{
    public List<string> flags = new List<string>();
    public List<IntValue> values = new List<IntValue>();
}

/// <summary>
/// 게임 진행도. 씬과 무관하게 유지되며 세이브에 함께 저장됩니다.
/// 플래그: 일어났는지 여부 (보스 처치, 문 열림 등)
/// 값: 숫자 (현재 챕터, 수집 개수 등)
/// </summary>
public class GameProgress
{
    private readonly HashSet<string> flags = new HashSet<string>();
    private readonly Dictionary<string, int> values = new Dictionary<string, int>();

    /// <summary>
    /// 값이 바뀐 키 (진행도에 반응하는 UI/오브젝트용)
    /// </summary>
    public event Action<string> OnChanged;

    public bool HasFlag(string key) => flags.Contains(key);

    public void SetFlag(string key, bool on = true)
    {
        bool changed = on ? flags.Add(key) : flags.Remove(key);
        if (changed) OnChanged?.Invoke(key);
    }

    public int GetValue(string key, int defaultValue = 0) =>
        values.TryGetValue(key, out int value) ? value : defaultValue;

    public void SetValue(string key, int value)
    {
        if (values.TryGetValue(key, out int old) && old == value) return;
        values[key] = value;
        OnChanged?.Invoke(key);
    }

    public void AddValue(string key, int delta) => SetValue(key, GetValue(key) + delta);

    public void Clear()
    {
        flags.Clear();
        values.Clear();
    }

    public ProgressData ToData()
    {
        ProgressData data = new ProgressData();
        data.flags.AddRange(flags);
        foreach (var pair in values)
            data.values.Add(new IntValue { key = pair.Key, value = pair.Value });
        return data;
    }

    public void FromData(ProgressData data)
    {
        Clear();
        if (data == null) return;

        foreach (string flag in data.flags) flags.Add(flag);
        foreach (IntValue v in data.values) values[v.key] = v.value;
    }
}