using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gimmick.SafePlatform
{
    public enum PuzzleMode
    {
        SingleTarget,
        Sequence
    }

    public class SafePlatformPuzzle : MonoBehaviour
    {
        [Header("퍼즐 작동 방식 선택")]
        [SerializeField] private PuzzleMode puzzleMode = PuzzleMode.Sequence;

        [Header(" 발판 오브젝트 목록")]
        [SerializeField] private List<Collider2D> platformColliders = new List<Collider2D>();

        [Header("[단일 모드용] 정답 발판")]
        [SerializeField] private Collider2D singleCorrectPlatform;

        [Header("[순서 모드용] 정답 순서 (인덱스)")]
        [SerializeField] private List<int> correctSequence = new List<int>();

        [Header("연결된 문")]
        [SerializeField] private Door targetDoor;

        [Header("실패 시 플레이어 리셋 위치 (선택)")]
        [SerializeField] private Transform resetPoint;

        [Header("발판 연출 설정")]
        [Tooltip("밟았을 때 내려갈 Y축 깊이")]
        [SerializeField] private float pressDepth = 0.15f;

        [Tooltip("발판 복귀/하강 속도")]
        [SerializeField] private float moveSpeed = 10f;

        private int currentStep = 0;
        private bool isSolved = false;

        private Collider2D currentPressedPlatform = null; 
        private Collider2D lastHandledPlatform = null;    

        private List<Vector3> originPositions = new List<Vector3>();

        private void Start()
        {
            ValidateSettings();

            //시작할 때 각 발판의 원래 초기 위치 보관
            originPositions.Clear();
            foreach (var pad in platformColliders)
            {
                if (pad != null)
                {
                    originPositions.Add(pad.transform.position);
                }
            }
        }

        private void ValidateSettings()
        {
            if (platformColliders == null || platformColliders.Count == 0)
                Debug.LogWarning($"[{gameObject.name}] 등록된 발판이 없습니다!");

            if (puzzleMode == PuzzleMode.SingleTarget && singleCorrectPlatform == null)
                Debug.LogWarning($"[{gameObject.name}] SingleTarget 모드이나 'Single Correct Platform'이 설정되지 않았습니다!");

            if (puzzleMode == PuzzleMode.Sequence && (correctSequence == null || correctSequence.Count == 0))
                Debug.LogWarning($"[{gameObject.name}] Sequence 모드이나 'Correct Sequence'가 설정되지 않았습니다!");

            if (targetDoor == null)
                Debug.LogWarning($"[{gameObject.name}] 연결된 Target Door가 없습니다!");
        }

        private void Update()
        {
            AnimatePlatforms();

            if (isSolved || platformColliders.Count == 0) return;

            CheckPlatformStepped();
        }

        //발판 위치 보간 연출
        private void AnimatePlatforms()
        {
            for (int i = 0; i < platformColliders.Count; i++)
            {
                var pad = platformColliders[i];
                if (pad == null || i >= originPositions.Count) continue;

                Vector3 targetPos = originPositions[i];

                // 현재 플레이어가 밟고 있는 발판이면 아래로 낮춤
                if (pad == currentPressedPlatform)
                {
                    targetPos.y -= pressDepth;
                }

                pad.transform.position = Vector3.Lerp(pad.transform.position, targetPos, Time.deltaTime * moveSpeed);
            }
        }

        private void CheckPlatformStepped()
        {
            //플레이어와 현재 밟힌 발판 가져오기
            var (steppedPad, playerTransform) = GetSteppedPlatformAndPlayer();

            currentPressedPlatform = steppedPad;

            //아무 발판도 안 밟고 있으면 판정 기록 초기화
            if (steppedPad == null)
            {
                lastHandledPlatform = null;
                return;
            }

            //동일 발판에 서 있는 동안 중복 판정 방지
            if (steppedPad == lastHandledPlatform) return;

            lastHandledPlatform = steppedPad;
            int steppedIndex = platformColliders.IndexOf(steppedPad);

            if (puzzleMode == PuzzleMode.SingleTarget)
            {
                ProcessSingleMode(steppedPad, playerTransform);
            }
            else if (puzzleMode == PuzzleMode.Sequence)
            {
                ProcessSequenceMode(steppedIndex, steppedPad, playerTransform);
            }
        }

        //밟고 있는 발판과 플레이어 Transform을 함께 찾아 반환
        private (Collider2D pad, Transform player) GetSteppedPlatformAndPlayer()
        {
            Collider2D[] results = new Collider2D[5];
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };

            foreach (var pad in platformColliders)
            {
                if (pad == null) continue;

                int count = pad.Overlap(filter, results);
                for (int i = 0; i < count; i++)
                {
                    if (results[i] != null && (results[i].CompareTag("Player") || results[i].GetComponentInParent<Player.Move.PlayerMoveMent>() != null))
                    {
                        return (pad, results[i].transform);
                    }
                }
            }

            return (null, null);
        }

        private void ProcessSingleMode(Collider2D steppedPlatform, Transform playerTransform)
        {
            if (steppedPlatform == singleCorrectPlatform)
            {
                Debug.Log($"[{gameObject.name}] 정답 발판!");
                SolvePuzzle();
            }
            else
            {
                Debug.Log($"[{gameObject.name}] 오답 발판!");
                ResetPuzzleProgress(playerTransform);
            }
        }

        private void ProcessSequenceMode(int platformIndex, Collider2D platformCollider, Transform playerTransform)
        {
            int expectedIndex = correctSequence[currentStep];

            if (currentStep > 0 && correctSequence[currentStep - 1] == platformIndex)
            {
                Debug.Log($"[{gameObject.name}] 직전에 맞춘 발판 재방문. 상태 유지.");
                return;
            }

            if (platformIndex == expectedIndex)
            {
                currentStep++;
                Debug.Log($"[{gameObject.name}] 정답 발판! ({platformIndex}번) -> 단계: {currentStep}/{correctSequence.Count}");

                if (currentStep >= correctSequence.Count)
                {
                    SolvePuzzle();
                }
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] 오답 발판! ({platformIndex}번) -> 진행 상황 초기화 및 순간이동!");
                ResetPuzzleProgress(playerTransform);
            }
        }

        private void ResetPuzzleProgress(Transform playerTransform)
        {
            currentStep = 0;
            lastHandledPlatform = null;
            currentPressedPlatform = null; 

            //텔레포트 처리
            if (resetPoint != null && playerTransform != null)
            {
                //플레이어 부모/본체 Rigidbody2D 속도 초기화
                Rigidbody2D playerRb = playerTransform.GetComponentInParent<Rigidbody2D>();
                if (playerRb != null)
                {
                    playerRb.linearVelocity = Vector2.zero;
                }

                //리셋 지점으로 이동
                Transform targetMoveTransform = playerTransform.GetComponentInParent<Player.Move.PlayerMoveMent>()?.transform ?? playerTransform;
                targetMoveTransform.position = resetPoint.position;

                Debug.Log($"[{gameObject.name}] 플레이어를 리셋 위치({resetPoint.position})로 순간이동시켰습니다.");
            }
        }

        private void SolvePuzzle()
        {
            isSolved = true;
            Debug.Log($"[{gameObject.name}] 퍼즐 클리어! 문을 엽니다.");

            if (targetDoor != null)
            {
                targetDoor.Open();
            }
        }
    }
}