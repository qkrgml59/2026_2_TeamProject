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

        [Tooltip("초기 이동 방향 (1 : 오른쪽/위, -1 : 왼쪽/아래")]
        [SerializeField] private int moveDirection = 1;

        [Header("방향 전환 발판 ")]
        [Tooltip("밝으면 이동 축이 전되는 발판1")]
        [SerializeField] private Collider2D switchPad1;

        [Tooltip("밝으면 이동 축이 전되는 발판2")]
        [SerializeField] private Collider2D switchPad2;

        [Header("목표 버튼 및 연결된 문 ")]
        [Tooltip("이동 오브젝트가 도달해야 하는 목표 버튼 Collider")]
        [SerializeField] private Collider2D targetButton;

        [Tooltip("목표 도달 시 열릴 문")]
        [SerializeField] private Door targetDoor;

        private Rigidbody2D movingRb;
        private bool isSolved = false;

        //발판 중복 입력 방지용 플래그
        private bool isStandingOnPad1 = false;
        private bool isStandingOnPad2 = false;
        private float lastBounceTime = 0f; //연속 충돌 방지용 타임스탬프

        void Start()
        {
            if(movingObject != null)
            {
                movingRb = movingObject.GetComponent<Rigidbody2D>();
            }

            ValidateSettings();
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

            //벽 충돌 검사 (연속 반전 방지를 위해 최소 0.1 초 쿨타임 적용)
            if (Time.time - lastBounceTime > 0.1f)
            {
                ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
                RaycastHit2D[] hits = new RaycastHit2D[4];
                Vector2 checkDir = (currentAxis == MoveAxis.Horizontal) ? Vector2.right * moveDirection : Vector2.up * moveDirection;

                //약간 감지 거리를 두어 벽 도착 직전에 반전시킴 (0.05f -> 0.1f)
                int count = movingCollider.Cast(checkDir, filter, hits, 0.1f);
                for (int i = 0; i < count; i++)
                {
                    //벽/장애물에 충돌한 경우 방향 반전
                    if (hits[i].collider != null && hits[i].collider.gameObject != movingObject.gameObject)
                    {
                        moveDirection *= -1; // 방향 반전
                        lastBounceTime = Time.time; // 반전 시각 기록 (부르르 떨림 방지)
                        break;
                    }
                }
            }

            //목표 버튼 도착 검사 (오브젝트 충돌체와 목표 버튼 겹침 확인)
            if (targetButton != null && movingCollider.IsTouching(targetButton))
            {
                SolvePuzzle();
            }
        }

        private void CheckPadTriggers()
        {
            if(switchPad1 !=null)
            {
                bool isPlayerOnPad1 = CheckPlayerOnTrigger(switchPad1);
                if (isPlayerOnPad1 && !isStandingOnPad1)
                {
                    ToggleMoveAxis(); // 새로 밟았을 때 1회 전환
                }
                isStandingOnPad1 = isPlayerOnPad1;
            }
        }
        private bool CheckPlayerOnTrigger(Collider2D padCollider)
        {
            if (padCollider == null) return false;

            //발판 콜라이더의 현재 영역(Bounds) 크기와 위치를 기반으로 겹치는 플레이어 탐색
            Bounds bounds = padCollider.bounds;
            Collider2D[] hitColliders = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f);

            foreach (var hit in hitColliders)
            {
                //발판 자기 자신은 제외
                if (hit == padCollider) continue;

                //플레이어 감지 (Player 태그 또는 PlayerMoveMent 컴포넌트로 감지)
                if (hit.CompareTag("Player") || hit.GetComponentInParent<Player.Move.PlayerMoveMent>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        //이동 축 전환 (상하 <-> 좌우)
        private void ToggleMoveAxis()
        {
            currentAxis = (currentAxis == MoveAxis.Horizontal) ? MoveAxis.Vertical : MoveAxis.Horizontal;
            Debug.Log($"[{gameObject.name}] 발판 동작! 이동 축 변경됨 -> {currentAxis}");
        }

        //퍼즐 성공 처리
        private void SolvePuzzle()
        {
            if (isSolved) return;

            isSolved = true;
            movingRb.linearVelocity = Vector2.zero; //이동 정지
            Debug.Log($"[{gameObject.name}] 퍼즐 성공! 목표 버튼에 도달했습니다. 문을 엽니다.");

            if (targetDoor != null)
            {
                targetDoor.Open(); 
            }
        }
    }
}
