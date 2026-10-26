using System.Collections.Generic;
using DG.Tweening;
using Puzzle.Sequence;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FourGuardians.CourseContent.Movement
{
    // 플레이어가 지금 어떤 행동을 하고 있는지 한 곳에서 표현한다.
    // Animator는 이 값을 읽어서 알맞은 애니메이션을 재생한다.
    public enum WarriorAction
    {
        Idle, Run, BasicAttack, Attack1, Attack2, Death, Hurt, JumpUp, JumpToFall, Fall,
        Dash, DashAttack, JumpAttack, EdgeGrab, EdgeIdle, Crouch, WallSlide, LadderGrab, Slide
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class BasicPlayerMovement2D : MonoBehaviour
    {
        [Header("기본 이동")]
        [Tooltip("좌우 입력이 있을 때 적용하는 최대 수평 속도입니다.")]
        [SerializeField] private float moveSpeed = 5f;
        [Tooltip("지상 점프와 벽 점프에 사용하는 위쪽 초기 속도입니다.")]
        [SerializeField] private float jumpSpeed = 9f;

        [Header("특수 이동")]
        [Tooltip("대시가 진행되는 동안 유지하는 수평 속도입니다.")]
        [SerializeField] private float dashSpeed = 11f;
        [Tooltip("기존 씬은 대시를 유지하고, 획득형 맵에서만 시작 시 잠급니다.")]
        [SerializeField] private bool dashUnlocked = true;
        [Tooltip("아래 방향과 이동 방향을 함께 눌렀을 때의 슬라이드 속도입니다.")]
        [SerializeField] private float slideSpeed = 8f;
        [Tooltip("사다리에서 위·아래 입력으로 움직이는 속도입니다.")]
        [SerializeField] private float ladderSpeed = 3f;
        [Tooltip("벽을 타고 내려올 때 허용하는 최대 하강 속도입니다.")]
        [SerializeField] private float wallSlideSpeed = 2f;
        [Tooltip("벽 점프 시 벽 반대 방향으로 밀어내는 수평 속도입니다.")]
        [SerializeField] private float wallJumpHorizontalSpeed = 6f;
        [Tooltip("벽 점프 직후 반대 입력 때문에 다시 벽으로 붙는 것을 막는 시간입니다.")]
        [SerializeField] private float wallJumpInputLockDuration = 0.18f;
        [Tooltip("벽 동작 중 Visual을 Collider 반지름의 몇 배만큼 벽 쪽으로 보정할지 정합니다.")]
        [SerializeField, Range(0f, 1f)] private float wallVisualInsetRatio = 0.5f;
        [Tooltip("Collider가 벽 안으로 파고들지 않도록 남겨두는 아주 작은 간격입니다.")]
        [SerializeField, Min(0f)] private float wallContactSkin = 0.01f;

        [Header("일방향 발판 통과")]
        [Tooltip("아래 방향과 점프를 함께 눌러 현재 발판만 통과합니다. 기존 씬은 기본적으로 꺼져 있습니다.")]
        [SerializeField] private bool allowDropThrough;
        [SerializeField, Min(0.1f)] private float dropInitialSpeed = 2f;

        private bool dropRequested;
        private readonly List<Collider2D> ignoredOneWays = new List<Collider2D>();
        private float dropTimer;
        private float externalInputLock;
        public bool IsDroppingThrough => ignoredOneWays.Count > 0;

        // 매번 배열을 새로 만들지 않고 재사용하여 불필요한 가비지 생성을 막는다.
        private readonly RaycastHit2D[] hits = new RaycastHit2D[4];
        private Rigidbody2D body;
        private CapsuleCollider2D bodyCollider;
        private float horizontal;
        private float actionTimer;
        private int facing = 1;
        private bool jumpRequested;
        private bool slideEnabled = true;
        private bool nearLadder;
        private bool isClimbing;
        private bool atLadderTop;
        private bool ladderJumpRequested;
        private float ladderReentryCooldown;
        private float wallJumpInputLock;
        private Collider2D currentLadder;

        public WarriorAction Action { get; private set; } = WarriorAction.Idle;
        public Vector2 Velocity => body == null ? Vector2.zero : body.linearVelocity;
        public int Facing => facing;
        public bool DashUnlocked => dashUnlocked;
        // 고정 픽셀값 대신 현재 Collider 반지름에 비례해 벽 동작의 그림 위치를 보정한다.
        public float WallVisualOffset => bodyCollider == null
            ? 0f
            : bodyCollider.bounds.extents.x * wallVisualInsetRatio;
        public bool IsGrounded { get; private set; }
        // Animator가 사다리 입력 여부를 확인할 수 있도록 읽기 전용으로 공개한다.
        public float VerticalInput { get; private set; }

        private bool IsLocked => Action is WarriorAction.BasicAttack
            or WarriorAction.Attack1 or WarriorAction.Attack2 or WarriorAction.Hurt
            or WarriorAction.Dash or WarriorAction.DashAttack or WarriorAction.JumpAttack or WarriorAction.Slide
            or WarriorAction.EdgeGrab or WarriorAction.EdgeIdle;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<CapsuleCollider2D>();
        }

        // Update에서는 키 입력을 읽는다. wasPressedThisFrame 입력을 놓치지 않기 위해서다.
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || Action == WarriorAction.Death || externalInputLock > 0f) return;

            horizontal = Axis(keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
                keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
            // 대시는 시작 순간의 방향을 끝까지 유지한다.
            // 입력값은 미리 읽어두되 대시가 끝나기 전에는 facing을 변경하지 않는다.
            bool directionLocked = Action is WarriorAction.Dash or WarriorAction.DashAttack;
            if (!directionLocked && wallJumpInputLock <= 0f && Mathf.Abs(horizontal) > 0.01f)
            {
                facing = horizontal > 0f ? 1 : -1;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                if (isClimbing) ladderJumpRequested = true;
                else
                {
                    jumpRequested = true;
                    dropRequested = allowDropThrough && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
                }
            }
            else if (!nearLadder && (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame))
            {
                jumpRequested = true;
            }

            if (keyboard.jKey.wasPressedThisFrame) RequestAttack();
            if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.kKey.wasPressedThisFrame)
            {
                // 짧은 시간에 빠르게 이동해야 대시가 걷기와 확실히 구분된다.
                RequestDash();
            }
            if (keyboard.hKey.wasPressedThisFrame) Begin(WarriorAction.Hurt, 0.2f);
            if (keyboard.lKey.wasPressedThisFrame)
            {
                EnterDeathState();
            }
        }

        /// <summary>MapTest의 획득 진행도를 연결합니다. 기본값 true는 기존 씬의 대시 사용을 유지합니다.</summary>
        public void SetDashUnlocked(bool unlocked) => dashUnlocked = unlocked;

        /// <summary>MapTest에서는 불필요한 슬라이딩 진입만 막고 다른 씬은 기존 동작을 유지합니다.</summary>
        public void SetSlideEnabled(bool allow) => slideEnabled = allow;

        /// <summary>키 입력과 능력 해금이 같은 대시 진입 조건을 사용합니다.</summary>
        public bool RequestDash()
        {
            if (!dashUnlocked || externalInputLock > 0f || Action is WarriorAction.Death or WarriorAction.Hurt) return false;
            Begin(WarriorAction.Dash, 0.28f);
            return true;
        }
        /// <summary>기존 사망 상태와 정지 처리를 체력 이벤트에서도 사용할 수 있게 한다. 부활이나 저장은 처리하지 않는다.</summary>
        public void EnterDeathState()
        {
            Action = WarriorAction.Death;
            body.linearVelocity = Vector2.zero;
        }

        // FixedUpdate에서는 Rigidbody2D를 움직인다. 물리 계산은 일정한 시간 간격으로 해야 안정적이다.
        private void FixedUpdate()
        {
            ladderReentryCooldown = Mathf.Max(0f, ladderReentryCooldown - Time.fixedDeltaTime);
            wallJumpInputLock = Mathf.Max(0f, wallJumpInputLock - Time.fixedDeltaTime);
            UpdateIgnoredPlatform();
            UpdateGrounded();
            if (externalInputLock > 0f)
            {
                externalInputLock = Mathf.Max(0f, externalInputLock - Time.fixedDeltaTime);
                body.linearVelocity = Vector2.zero;
                jumpRequested = dropRequested = false;
                return;
            }
            if (Action == WarriorAction.Death) return;
            bool wantsDrop = dropRequested;
            dropRequested = false;
            if (wantsDrop && !isClimbing && (!IsLocked || Action == WarriorAction.Slide) && TryDropThrough()) return;

            float vertical = ReadVertical();
            VerticalInput = vertical;

            if (isClimbing)
            {
                // 사다리는 중력과 일반 이동을 사용하지 않으므로 가장 먼저 별도 처리한다.
                UpdateLadderMovement(vertical);
                jumpRequested = false;
                return;
            }

            if (IsLocked)
            {
                // 공격과 대시는 정해진 시간이 끝나기 전까지 일반 이동이 상태를 덮어쓰지 않게 한다.
                UpdateTimedAction();
                jumpRequested = false;
                return;
            }

            if (nearLadder && ladderReentryCooldown <= 0f && Mathf.Abs(vertical) > 0.01f)
            {
                // 사다리 영역에 닿기만 해서는 올라가지 않고, 세로 입력이 있을 때 진입한다.
                EnterLadder();
                UpdateLadderMovement(vertical);
                return;
            }

            body.gravityScale = 3f;
            if (jumpRequested && !IsGrounded && TryGetWallContact(out _))
            {
                // 벽의 반대 방향으로 밀어내면서 위로 점프한다.
                int wallDirection = facing;
                facing = -wallDirection;
                wallJumpInputLock = wallJumpInputLockDuration;
                body.linearVelocity = new Vector2(-wallDirection * wallJumpHorizontalSpeed, jumpSpeed);
                Action = WarriorAction.JumpUp;
            }
            else if (jumpRequested && IsGrounded)
            {
                body.linearVelocity = new Vector2(horizontal * moveSpeed, jumpSpeed);
                Action = WarriorAction.JumpUp;
                IsGrounded = false;
            }
            else
            {
                if (wallJumpInputLock <= 0f)
                {
                    body.linearVelocity = new Vector2(horizontal * moveSpeed, body.linearVelocity.y);
                }

                SelectFreeAction();
            }
            jumpRequested = false;
        }

        private void SelectFreeAction()
        {
            Keyboard keyboard = Keyboard.current;
            bool down = keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
            if (IsGrounded)
            {
                if (slideEnabled && down && Mathf.Abs(horizontal) > 0.01f) Begin(WarriorAction.Slide, 0.5f);
                else Action = down ? WarriorAction.Crouch : Mathf.Abs(horizontal) > 0.01f ? WarriorAction.Run : WarriorAction.Idle;
                return;
            }

            // 벽을 향해 공중 이동 중이면 상승/하강과 관계없이 자동으로 벽 타기 상태가 된다.
            if (TryGetWallContact(out RaycastHit2D wallHit))
            {
                AlignColliderToWall(wallHit);
                Action = WarriorAction.WallSlide;
                body.linearVelocity = new Vector2(0f, Mathf.Max(body.linearVelocity.y, -wallSlideSpeed));
            }
            else if (body.linearVelocity.y > 1f) Action = WarriorAction.JumpUp;
            else if (body.linearVelocity.y > -1f) Action = WarriorAction.JumpToFall;
            else Action = WarriorAction.Fall;
        }

        private void RequestAttack()
        {
            // 대시 공격은 지상 대시 중에만 허용한다.
            // 공중 대시 중 J 입력은 무시하여 공중에서 공격으로 궤도가 바뀌지 않게 한다.
            if (Action == WarriorAction.Dash && IsGrounded)
            {
                Begin(WarriorAction.DashAttack, 0.45f);
            }
            // 공중에서는 별도 JumpAttack 상태를 사용하되, 원본 에셋의 Dash-Attack 모션을 재사용한다.
            // 상태를 분리해야 공중 공격이 실제 대시 속도를 적용받지 않는다.
            else if (!IsLocked && !IsGrounded)
            {
                Begin(WarriorAction.JumpAttack, 0.45f);
            }
            // 지상 기본 공격도 Dash-Attack 모션을 사용하지만 실제 대시 이동은 적용하지 않는다.
            // 기존 Attack1/Attack2 상태는 이후 원소 스킬용으로 남겨둔다.
            else if (!IsLocked && IsGrounded)
            {
                Begin(WarriorAction.BasicAttack, 0.45f);
            }
        }

        private void Begin(WarriorAction nextAction, float duration)
        {
            if (Action is WarriorAction.Death or WarriorAction.Hurt) return;
            Action = nextAction;
            actionTimer = duration;
        }

        private void UpdateTimedAction()
        {
            // 공격처럼 도중에 이동할 수 없는 행동은 타이머가 끝날 때까지 현재 상태를 유지한다.
            actionTimer -= Time.fixedDeltaTime;
            if (Action is WarriorAction.Dash or WarriorAction.DashAttack)
            {
                // 대시 방향이 벽으로 막히면 남은 시간을 기다리지 않고 즉시 기본 자세로 돌아간다.
                if (IsDashPathBlocked())
                {
                    CancelDashToIdle();
                    return;
                }

                body.linearVelocity = new Vector2(facing * dashSpeed, 0f);
            }
            else if (Action == WarriorAction.Slide)
            {
                body.linearVelocity = new Vector2(facing * slideSpeed, body.linearVelocity.y);
            }
            else if (Action == WarriorAction.JumpAttack)
            {
                // 점프 공격은 현재 공중 궤도를 유지한다. 대시 공격처럼 강제 수평 속도를 주지 않는다.
                body.linearVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.y);
            }
            else
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            }

            if (actionTimer > 0f) return;
            if (Action == WarriorAction.EdgeGrab)
            {
                Begin(WarriorAction.EdgeIdle, 0.6f);
                return;
            }
            Action = IsGrounded ? WarriorAction.Idle : WarriorAction.Fall;
        }

        private bool IsDashPathBlocked()
        {
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            // 한 물리 프레임 동안 이동할 거리만큼 미리 검사하면 빠른 대시의 벽 관통을 줄일 수 있다.
            float checkDistance = Mathf.Max(0.08f, dashSpeed * Time.fixedDeltaTime);
            int count = bodyCollider.Cast(Vector2.right * facing, filter, hits, checkDistance);

            for (int index = 0; index < count; index++)
            {
                if (!ignoredOneWays.Contains(hits[index].collider) && hits[index].normal.x * facing <= -0.6f)
                {
                    return true;
                }
            }

            return false;
        }

        private void CancelDashToIdle()
        {
            // 속도와 타이머를 함께 초기화해야 다음 입력이 이전 대시 상태의 영향을 받지 않는다.
            actionTimer = 0f;
            body.linearVelocity = Vector2.zero;
            Action = WarriorAction.Idle;
        }

        private void UpdateGrounded()
        {
            // Collider를 아래로 조금 투사해 발밑 바닥을 확인한다.
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            int count = bodyCollider.Cast(Vector2.down, filter, hits, 0.08f);
            IsGrounded = false;

            for (int index = 0; index < count; index++)
            {
                if (!ignoredOneWays.Contains(hits[index].collider) && hits[index].normal.y >= 0.6f)
                {
                    IsGrounded = true;
                    return;
                }
            }
        }

        private bool TryGetWallContact(out RaycastHit2D wallHit)
        {
            // CapsuleCollider 전체를 투사하므로 캐릭터 크기가 바뀌어도 몸 바깥쪽부터 벽을 검사한다.
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            int count = bodyCollider.Cast(Vector2.right * facing, filter, hits, 0.08f);

            for (int index = 0; index < count; index++)
            {
                // 진행 방향의 반대쪽을 바라보는 수직면만 벽으로 취급한다.
                if (!ignoredOneWays.Contains(hits[index].collider) && hits[index].normal.x * facing <= -0.6f)
                {
                    wallHit = hits[index];
                    return true;
                }
            }

            wallHit = default;
            return false;
        }

        private void AlignColliderToWall(RaycastHit2D wallHit)
        {
            Bounds colliderBounds = bodyCollider.bounds;
            float colliderCenterOffset = colliderBounds.center.x - body.position.x;

            // 벽 표면에서 Collider 반지름만큼 떨어진 위치가 플레이어 중심이 된다.
            // 따라서 캐릭터 크기나 Collider Offset이 달라져도 빈 공간을 고정값으로 추측하지 않는다.
            float desiredBodyX = wallHit.point.x
                - facing * (colliderBounds.extents.x + wallContactSkin)
                - colliderCenterOffset;

            body.position = new Vector2(desiredBodyX, body.position.y);
        }

        private float ReadVertical()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard == null ? 0f : Axis(keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
                keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
        }

        private static float Axis(bool negative, bool positive) => (positive ? 1f : 0f) - (negative ? 1f : 0f);

        private void EnterLadder()
        {
            if (currentLadder == null) return;
            // 사다리에서는 중력을 끄고 입력으로 Y 속도를 직접 제어한다.
            isClimbing = true;
            Action = WarriorAction.LadderGrab;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            jumpRequested = false;
        }

        private void UpdateLadderMovement(float vertical)
        {
            if (currentLadder == null)
            {
                ExitLadder();
                return;
            }

            if (ladderJumpRequested)
            {
                ladderJumpRequested = false;
                ExitLadder(0.35f);
                body.linearVelocity = new Vector2(facing * 2f, jumpSpeed);
                Action = WarriorAction.JumpUp;
                return;
            }

            if (Mathf.Abs(horizontal) > 0.01f)
            {
                facing = horizontal > 0f ? 1 : -1;
                ExitLadder(0.3f);
                body.linearVelocity = new Vector2(horizontal * moveSpeed, 0f);
                Action = WarriorAction.Fall;
                return;
            }

            if (atLadderTop)
            {
                if (vertical < -0.01f)
                {
                    atLadderTop = false;
                }
                else
                {
                    body.gravityScale = 0f;
                    body.linearVelocity = Vector2.zero;
                    Action = WarriorAction.EdgeIdle;
                    return;
                }
            }

            Bounds ladderBounds = currentLadder.bounds;
            // 사다리 중앙으로 조금씩 이동시켜 진입 순간에 좌우로 순간이동하는 느낌을 줄인다.
            float centeredX = Mathf.MoveTowards(body.position.x, ladderBounds.center.x, 8f * Time.fixedDeltaTime);
            body.position = new Vector2(centeredX, body.position.y);
            body.gravityScale = 0f;
            body.linearVelocity = new Vector2(0f, vertical * ladderSpeed);
            Action = WarriorAction.LadderGrab;

            if (vertical > 0f && bodyCollider.bounds.min.y >= ladderBounds.max.y - 0.08f)
            {
                body.position = new Vector2(ladderBounds.center.x, ladderBounds.max.y + 0.04f);
                body.linearVelocity = Vector2.zero;
                ExitLadder(0.35f);
                Action = WarriorAction.Idle;
            }
        }

        private void ExitLadder(float reentryCooldown = 0f)
        {
            isClimbing = false;
            atLadderTop = false;
            ladderReentryCooldown = Mathf.Max(ladderReentryCooldown, reentryCooldown);
            body.gravityScale = 3f;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Debug.Log($"[로그 1] 충돌 발생! 충돌 대상: {other.gameObject.name}");

            // 사다리 진입은 공격 상태와 관계없이 처리한다.
            if (other.name.Contains("Ladder"))
            {
                nearLadder = true;
                currentLadder = other;
            }

            bool isAttacking = Action is WarriorAction.BasicAttack or WarriorAction.JumpAttack or WarriorAction.DashAttack;

            if (!isAttacking)
            {
                Debug.Log($"[로그 2] 충돌은 했지만 플레이어가 공격 상태가 아닙니다. (현재 상태: {Action})");
                return;
            }

            Debug.Log("[로그 2] 플레이어 공격 상태 확인 완료!");

            //기존 순서 퍼즐 피격
            if (other.TryGetComponent<Puzzle.Sequence.SequencePuzzleObject>(out var seqObj))
            {
                seqObj.OnAttacked();
            }

            //회전 퍼즐 피격 (부모에 있는 RotationPuzzle 탐색)
            var rotationPuzzle = other.GetComponentInParent<Gimmick.RotationPuzzle.RotationPuzzle>();
            if (rotationPuzzle != null)
            {
                Debug.Log($"[로그 3] {other.gameObject.name}에서 RotationPuzzle을 찾았습니다! CheckPuzzle 호출");
                rotationPuzzle.CheckPuzzle(other.transform);
            }
            else
            {
                Debug.LogWarning($"[로그 3 실패] {other.gameObject.name} 또는 그 부모에서 RotationPuzzle 스크립트를 찾지 못했습니다.");
            }

            //시간 제한 스위치 퍼즐 피격
            var attackSwitchPuzzle = other.GetComponentInParent<Gimmick.TimedAttackSwitch.TimedAttackSwitchPuzzle>();
            if(attackSwitchPuzzle !=null)
            {
                attackSwitchPuzzle.OnAttacked(other.transform);
            }

        }

        /// <summary>발밑의 독립된 일방향 발판 하나만 잠시 통과한다. 지역 전체 Composite는 무시하지 않는다.</summary>
        private bool TryDropThrough()
        {
            if (!IsGrounded) return false;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            int count = bodyCollider.Cast(Vector2.down, filter, hits, 0.08f);
            float top = float.NegativeInfinity;
            for (int index = 0; index < count; index++)
            {
                Collider2D platform = hits[index].collider;
                if (platform == null || platform is CompositeCollider2D || hits[index].normal.y < 0.6f) continue;
                PlatformEffector2D effector = platform.GetComponent<PlatformEffector2D>();
                if (effector == null || !effector.useOneWay || !platform.usedByEffector) continue;
                if (ignoredOneWays.Count == 0) top = platform.bounds.max.y;
                // 편집 구역의 이음매에서는 같은 높이로 발을 받치는 조각만 함께 통과한다.
                if (Mathf.Abs(platform.bounds.max.y - top) > 0.025f) continue;
                ignoredOneWays.Add(platform);
                Physics2D.IgnoreCollision(bodyCollider, platform, true);
            }
            if (ignoredOneWays.Count == 0) return false;
            dropTimer = 0.8f;
            jumpRequested = false;
            IsGrounded = false;
            body.gravityScale = 3f;
            body.linearVelocity = new Vector2(horizontal * moveSpeed, -dropInitialSpeed);
            Action = WarriorAction.Fall;
            return true;
        }

        private void UpdateIgnoredPlatform()
        {
            if (ignoredOneWays.Count == 0) return;
            dropTimer -= Time.fixedDeltaTime;
            for (int index = ignoredOneWays.Count - 1; index >= 0; index--)
            {
                Collider2D platform = ignoredOneWays[index];
                if (platform != null && bodyCollider.bounds.center.y >= platform.bounds.min.y - 0.01f && dropTimer > 0f) continue;
                if (platform != null) Physics2D.IgnoreCollision(bodyCollider, platform, false);
                ignoredOneWays.RemoveAt(index);
            }
        }

        private void RestoreIgnoredPlatform()
        {
            foreach (Collider2D platform in ignoredOneWays)
                if (platform != null && bodyCollider != null) Physics2D.IgnoreCollision(bodyCollider, platform, false);
            ignoredOneWays.Clear();
            dropTimer = 0f;
        }

        /// <summary>안전 지점 복귀 시 이동·대시·사다리·통과 상태를 함께 정리하고 짧게 입력을 잠근다.</summary>
        public void ResetAfterTeleport(Vector2 position, float inputLockDuration = 0.2f)
        {
            RestoreIgnoredPlatform();
            horizontal = actionTimer = wallJumpInputLock = ladderReentryCooldown = 0f;
            jumpRequested = dropRequested = nearLadder = isClimbing = atLadderTop = ladderJumpRequested = false;
            currentLadder = null;
            VerticalInput = 0f;
            externalInputLock = Mathf.Max(0f, inputLockDuration);
            body.position = position;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = 3f;
            IsGrounded = false;
            Action = WarriorAction.Idle;
        }

        private void OnDisable()
        {
            RestoreIgnoredPlatform();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != currentLadder) return;
            nearLadder = false;
            currentLadder = null;
            ExitLadder();
        }
    }
}
