using System;
using Player.Move;
using UnityEngine;

/// <summary>
/// 플레이어 위치 저장. 땅을 밟고 있을 때의 위치만 기록하고,
/// 공중에 있는 동안에는 자동 저장을 착지할 때까지 미룹니다.
/// </summary>
[RequireComponent(typeof(PlayerMoveMent))]
public class PlayerSaveable : MonoBehaviour, ISaveable, ISaveCondition
{
    [Serializable]
    private struct State
    {
        public Vector2 position;
    }

    [Tooltip("이 레이어 바닥에 서 있을 때만 안전한 위치로 기록. Nothing이면 모든 바닥 허용")]
    [SerializeField] private LayerMask safeGroundLayer;

    private PlayerMoveMent movement;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private Vector2 lastSafePosition;

    public string SaveId => "Player";
    public bool IsSafeToSave => IsOnSafeGround();

    private void Awake()
    {
        movement = GetComponent<PlayerMoveMent>();
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        lastSafePosition = body.position; // 착지 전에 저장될 경우를 대비한 진입 위치
    }

    private void OnEnable()
    {
        SaveManager.Instance.RegisterCondition(this);
    }

    private void OnDisable()
    {
        if (SaveManager.HasInstance)
            SaveManager.Instance.UnregisterCondition(this);
    }

    private void Update()
    {
        if (IsOnSafeGround())
            lastSafePosition = body.position;
    }

    private bool IsOnSafeGround()
    {
        if (!movement.IsGrounded || movement.Action == WarriorAction.Death) return false;
        if (safeGroundLayer.value == 0) return true; // 레이어 미지정: 모든 바닥 허용

        // 발밑 바닥이 안전 레이어인지 확인 (가시, 움직이는 발판 등 제외용)
        Bounds b = bodyCollider.bounds;
        Vector2 feet = new Vector2(b.center.x, b.min.y);
        Vector2 size = new Vector2(b.size.x * 0.9f, 0.1f);
        return Physics2D.OverlapBox(feet, size, 0f, safeGroundLayer) != null;
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new State { position = lastSafePosition });
    }

    public void RestoreState(string json)
    {
        State state = JsonUtility.FromJson<State>(json);

        body.position = state.position;
        transform.position = state.position;
        body.linearVelocity = Vector2.zero;
        lastSafePosition = state.position;
    }
}