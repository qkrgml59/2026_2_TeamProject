using UnityEngine;
using UnityEngine.InputSystem;

namespace FourGuardians.CourseContent.Movement
{
    // 플레이어가 지금 어떤 행동을 하고 있는지 한 곳에서 표현한다.
    // Animator는 이 값을 읽어서 알맞은 애니메이션을 재생한다.
    public enum WarriorAction
    {
        Idle, Run, Attack1, Attack2, Death, Hurt, JumpUp, JumpToFall, Fall,
        Dash, DashAttack, EdgeGrab, EdgeIdle, Crouch, WallSlide, LadderGrab, Slide
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class BasicPlayerMovement2D : MonoBehaviour
    {
        [Header("기본 이동")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpSpeed = 9f;

        [Header("특수 이동")]
        [SerializeField] private float dashSpeed = 11f;
        [SerializeField] private float slideSpeed = 8f;
        [SerializeField] private float ladderSpeed = 3f;
        [SerializeField] private float wallSlideSpeed = 2f;
        [SerializeField] private float wallJumpHorizontalSpeed = 6f;
        [SerializeField] private float wallJumpInputLockDuration = 0.18f;

        // 매번 배열을 새로 만들지 않고 재사용하여 불필요한 가비지 생성을 막는다.
        private readonly RaycastHit2D[] hits = new RaycastHit2D[4];
        private Rigidbody2D body;
        private CapsuleCollider2D bodyCollider;
        private float horizontal;
        private float actionTimer;
        private int facing = 1;
        private bool jumpRequested;
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
        public bool IsGrounded { get; private set; }

        private bool IsLocked => Action is WarriorAction.Attack1 or WarriorAction.Attack2 or WarriorAction.Hurt
            or WarriorAction.Dash or WarriorAction.DashAttack or WarriorAction.Slide
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
            if (keyboard == null || Action == WarriorAction.Death) return;

            horizontal = Axis(keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
                keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
            if (wallJumpInputLock <= 0f && Mathf.Abs(horizontal) > 0.01f)
            {
                facing = horizontal > 0f ? 1 : -1;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                if (isClimbing) ladderJumpRequested = true;
                else jumpRequested = true;
            }
            else if (!nearLadder && (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame))
            {
                jumpRequested = true;
            }

            if (keyboard.jKey.wasPressedThisFrame) RequestAttack();
            if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.kKey.wasPressedThisFrame)
            {
                Begin(WarriorAction.Dash, 0.7f);
            }
            if (keyboard.hKey.wasPressedThisFrame) Begin(WarriorAction.Hurt, 0.2f);
            if (keyboard.lKey.wasPressedThisFrame)
            {
                Action = WarriorAction.Death;
                body.linearVelocity = Vector2.zero;
            }
        }

        // FixedUpdate에서는 Rigidbody2D를 움직인다. 물리 계산은 일정한 시간 간격으로 해야 안정적이다.
        private void FixedUpdate()
        {
            ladderReentryCooldown = Mathf.Max(0f, ladderReentryCooldown - Time.fixedDeltaTime);
            wallJumpInputLock = Mathf.Max(0f, wallJumpInputLock - Time.fixedDeltaTime);
            UpdateGrounded();
            if (Action == WarriorAction.Death) return;

            float vertical = ReadVertical();

            if (isClimbing)
            {
                UpdateLadderMovement(vertical);
                jumpRequested = false;
                return;
            }

            if (IsLocked)
            {
                UpdateTimedAction();
                jumpRequested = false;
                return;
            }

            if (nearLadder && ladderReentryCooldown <= 0f && Mathf.Abs(vertical) > 0.01f)
            {
                EnterLadder();
                UpdateLadderMovement(vertical);
                return;
            }

            body.gravityScale = 3f;
            if (jumpRequested && !IsGrounded && IsTouchingWall())
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
                if (down && Mathf.Abs(horizontal) > 0.01f) Begin(WarriorAction.Slide, 0.5f);
                else Action = down ? WarriorAction.Crouch : Mathf.Abs(horizontal) > 0.01f ? WarriorAction.Run : WarriorAction.Idle;
                return;
            }

            // 벽을 향해 공중 이동 중이면 상승/하강과 관계없이 자동으로 벽 타기 상태가 된다.
            if (IsTouchingWall())
            {
                Action = WarriorAction.WallSlide;
                body.linearVelocity = new Vector2(body.linearVelocity.x, Mathf.Max(body.linearVelocity.y, -wallSlideSpeed));
            }
            else if (body.linearVelocity.y > 1f) Action = WarriorAction.JumpUp;
            else if (body.linearVelocity.y > -1f) Action = WarriorAction.JumpToFall;
            else Action = WarriorAction.Fall;
        }

        private void RequestAttack()
        {
            // 대시 도중 공격하면 별도의 대시 공격으로 연결한다.
            if (Action == WarriorAction.Dash) Begin(WarriorAction.DashAttack, 1f);
            // 원본 Attack 클립은 12프레임, 총 1.2초이므로 끝까지 재생한다.
            else if (!IsLocked && IsGrounded) Begin(WarriorAction.Attack1, 1.2f);
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
            if (Action is WarriorAction.Dash or WarriorAction.DashAttack) body.linearVelocity = new Vector2(facing * dashSpeed, 0f);
            else if (Action == WarriorAction.Slide) body.linearVelocity = new Vector2(facing * slideSpeed, body.linearVelocity.y);
            else body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

            if (actionTimer > 0f) return;
            if (Action == WarriorAction.EdgeGrab)
            {
                Begin(WarriorAction.EdgeIdle, 0.6f);
                return;
            }
            Action = IsGrounded ? WarriorAction.Idle : WarriorAction.Fall;
        }

        private void UpdateGrounded()
        {
            // Collider를 아래로 조금 투사해 발밑 바닥을 확인한다.
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            int count = bodyCollider.Cast(Vector2.down, filter, hits, 0.08f);
            IsGrounded = false;
            for (int index = 0; index < count; index++)
            {
                if (hits[index].normal.y >= 0.6f) { IsGrounded = true; return; }
            }
        }

        private bool IsTouchingWall()
        {
            // 바라보는 방향으로 Collider를 조금 투사해 벽을 확인한다.
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            int count = bodyCollider.Cast(Vector2.right * facing, filter, hits, 0.08f);

            for (int index = 0; index < count; index++)
            {
                // 진행 방향의 반대쪽을 바라보는 수직면만 벽으로 취급한다.
                if (hits[index].normal.x * facing <= -0.6f)
                {
                    return true;
                }
            }

            return false;
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
            if (!other.name.Contains("Ladder")) return;
            nearLadder = true;
            currentLadder = other;
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
