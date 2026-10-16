using UnityEngine;
using System.Collections;

namespace Gimmick.MazePuzzle
{
    public enum MoveAxis
    {
        Horizontal,
        Vertical
    }

    public class MazePuzzle : MonoBehaviour
    {
        [Header("자동 이동 오브젝트 설정")]
        [Tooltip("미로 안에서 자동으로 움직일 오브젝트 Transform")]
        [SerializeField] private Transform movingObject;

        [Tooltip("이동 속도")]
        [SerializeField] private float moveSpeed = 3f;

        [Tooltip("시작시 이동 축")]
        [SerializeField] private MoveAxis currentAxis = MoveAxis.Horizontal;

        [Tooltip("초기 이동 방향 (1 : 오른쪽/위, -1 : 왼쪽/아래)")]
        [SerializeField] private int moveDirection = 1;

        [Header("방향 전환 발판")]
        [Tooltip("밟으면 이동 축이 전환되는 발판1 Collider")]
        [SerializeField] private Collider2D switchPad1;

        [Tooltip("밟으면 이동 축이 전환되는 발판2 Collider")]
        [SerializeField] private Collider2D switchPad2;

        [Header("발판 버튼 시각 연출 설정")]
        [Tooltip("발판이 눌릴 때 눌려 내려가는 깊이")]
        [SerializeField] private float pressDepth = 0.1f;

        [Tooltip("발판1의 시각적 그래픽 Transform (누름 효과용, 미지정시 switchPad1 Transform 사용)")]
        [SerializeField] private Transform pad1VisualTransform;

        [Tooltip("발판2의 시각적 그래픽 Transform (누름 효과용, 미지정시 switchPad2 Transform 사용)")]
        [SerializeField] private Transform pad2VisualTransform;

        [Header("목표 버튼 및 연결된 문")]
        [Tooltip("이동 오브젝트가 도달해야 하는 목표 버튼 Collider")]
        [SerializeField] private Collider2D targetButton;

        [Tooltip("목표 도달 시 열릴 문")]
        [SerializeField] private Door targetDoor;

        private Rigidbody2D movingRb;
        private bool isSolved = false;

        //발판 중복 입력 및 시각적 연출용 변수
        private bool isStandingOnPad1 = false;
        private bool isStandingOnPad2 = false;

        private Vector3 pad1InitialPos;
        private Vector3 pad2InitialPos;

        private float lastBounceTime = 0f;

        private PermanentClear permanentClear;

        void Start()
        {
            if (movingObject != null)
            {
                movingRb = movingObject.GetComponent<Rigidbody2D>();
            }

            
            InitPadVisuals();

            ValidateSettings();

            permanentClear = GetComponent<PermanentClear>();
            if (permanentClear != null && permanentClear.IsCleared)
                RestoreSolved();
        }

        private void InitPadVisuals()
        {
            if (pad1VisualTransform == null && switchPad1 != null)
            {
                pad1VisualTransform = switchPad1.transform;
            }
            if (pad2VisualTransform == null && switchPad2 != null)
            {
                pad2VisualTransform = switchPad2.transform;
            }

            if (pad1VisualTransform != null) pad1InitialPos = pad1VisualTransform.localPosition;
            if (pad2VisualTransform != null) pad2InitialPos = pad2VisualTransform.localPosition;
        }

        private void ValidateSettings()
        {
            if (movingObject == null) Debug.LogWarning($"[{gameObject.name}] Moving Object가 연결되지 않았습니다!");
            if (switchPad1 == null || switchPad2 == null) Debug.LogWarning($"[{gameObject.name}] 방향 전환 발판(Switch Pad)이 연결되지 않았습니다!");
            if (targetButton == null) Debug.LogWarning($"[{gameObject.name}] Target Button이 연결되지 않았습니다!");
            if (targetDoor == null) Debug.LogWarning($"[{gameObject.name}] Target Door가 연결되지 않았습니다!");
        }

        private void FixedUpdate()
        {
            if (isSolved || movingObject == null || movingRb == null) return;

            //지정된 축과 방향으로 물리 이동 수행
            Vector2 velocity = Vector2.zero;
            if (currentAxis == MoveAxis.Horizontal)
            {
                velocity = new Vector2(moveDirection * moveSpeed, 0f);
            }
            else
            {
                velocity = new Vector2(0f, moveDirection * moveSpeed);
            }

            movingRb.linearVelocity = velocity;

            //이동 오브젝트 감지 및 충돌 검사
            CheckObjectCollisions();

            //플레이어 발판 밟기 검사
            CheckPadTriggers();
        }

        //이동 오브젝트가 벽에 부딪히거나 목표 버튼에 도달했는지 검사
        private void CheckObjectCollisions()
        {
            Collider2D movingCollider = movingObject.GetComponent<Collider2D>();
            if (movingCollider == null) return;

            //벽 충돌 검사
            if (Time.time - lastBounceTime > 0.1f)
            {
                ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
                RaycastHit2D[] hits = new RaycastHit2D[4];
                Vector2 checkDir = (currentAxis == MoveAxis.Horizontal) ? Vector2.right * moveDirection : Vector2.up * moveDirection;

                int count = movingCollider.Cast(checkDir, filter, hits, 0.1f);
                for (int i = 0; i < count; i++)
                {
                    //벽/장애물에 충돌한 경우 방향 반전
                    if (hits[i].collider != null && hits[i].collider.gameObject != movingObject.gameObject)
                    {
                        moveDirection *= -1; 
                        lastBounceTime = Time.time; 
                        break;
                    }
                }
            }

            //목표 버튼 도착 검사
            if (targetButton != null && movingCollider.IsTouching(targetButton))
            {
                SolvePuzzle();
            }
        }

        private void CheckPadTriggers()
        {
            //발판 1 처리
            if (switchPad1 != null)
            {
                bool isPlayerOnPad1 = CheckPlayerOnTrigger(switchPad1);

                //플레이어가 발판 1에 새로 올라왔을 때
                if (isPlayerOnPad1 && !isStandingOnPad1)
                {
                    ToggleMoveAxis();
                    SetPadPressedState(pad1VisualTransform, pad1InitialPos, true);
                }
                //플레이어가 발판 1에서 내려갔을 때
                else if (!isPlayerOnPad1 && isStandingOnPad1)
                {
                    SetPadPressedState(pad1VisualTransform, pad1InitialPos, false);
                }

                isStandingOnPad1 = isPlayerOnPad1;
            }

            //발판 2 처리
            if (switchPad2 != null)
            {
                bool isPlayerOnPad2 = CheckPlayerOnTrigger(switchPad2);

              
                if (isPlayerOnPad2 && !isStandingOnPad2)
                {
                    ToggleMoveAxis();
                    SetPadPressedState(pad2VisualTransform, pad2InitialPos, true);
                }

                else if (!isPlayerOnPad2 && isStandingOnPad2)
                {
                    SetPadPressedState(pad2VisualTransform, pad2InitialPos, false);
                }

                isStandingOnPad2 = isPlayerOnPad2;
            }
        }

        /// <summary>
        ///발판 시각 효과
        /// </summary>
        private void SetPadPressedState(Transform padTransform, Vector3 initialPos, bool isPressed)
        {
            if (padTransform == null) return;

            if (isPressed)
            {

                padTransform.localPosition = initialPos + new Vector3(0f, -pressDepth, 0f);
            }
            else
            {

                padTransform.localPosition = initialPos;
            }
        }

        private bool CheckPlayerOnTrigger(Collider2D padCollider)
        {
            if (padCollider == null) return false;

            Bounds bounds = padCollider.bounds;
            Collider2D[] hitColliders = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f);

            foreach (var hit in hitColliders)
            {
                if (hit == padCollider) continue;

                if (hit.CompareTag("Player") || hit.GetComponentInParent<Player.Move.PlayerMoveMent>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void ToggleMoveAxis()
        {
            currentAxis = (currentAxis == MoveAxis.Horizontal) ? MoveAxis.Vertical : MoveAxis.Horizontal;
            Debug.Log($"[{gameObject.name}] 발판 동작! 이동 축 변경됨 -> {currentAxis}");
        }

        private void SolvePuzzle()
        {
            if (isSolved) return;

            isSolved = true;
            movingRb.linearVelocity = Vector2.zero;
            Debug.Log($"[{gameObject.name}] 퍼즐 성공! 목표 버튼에 도달했습니다. 문을 엽니다.");

            if (targetDoor != null)
            {
                targetDoor.Open();
            }

            if (permanentClear != null)
                permanentClear.MarkCleared();
        }

        /// <summary> 저장된 진행도로 복원: 연출 없이 풀린 상태로 </summary>
        private void RestoreSolved()
        {
            isSolved = true; // FixedUpdate의 이동이 멈춤

            if (movingObject != null && targetButton != null)
                movingObject.position = targetButton.transform.position;

            if (movingRb != null)
                movingRb.linearVelocity = Vector2.zero;

            if (targetDoor != null)
                targetDoor.OpenImmediate();
        }
    }
}