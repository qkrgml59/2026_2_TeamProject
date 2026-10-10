using System.Collections.Generic;
using UnityEngine;

namespace Gimmick.SafePlatform
{
    public class SafePlatformPuzzle : MonoBehaviour
    {
        [Header("발판 오브젝트 목록")]
        [Tooltip("퍼즐에 사용할 모든 발판의 Collider2D를 Inspector에서 드래그하여 등록하세요.")]
        [SerializeField] private List<Collider2D> platformColliders = new List<Collider2D>();

        [Header("정답 순서 설정 (인덱스)")]
        [Tooltip("platformColliders 목록의 Element 번호(0부터 시작)를 정답 순서대로 적어주세요. 예: 0 -> 2 -> 1")]
        [SerializeField] private List<int> correctSequence = new List<int>();

        [Header("연결된 문")]
        [Tooltip("퍼즐 클리어 시 열릴 문 오브젝트")]
        [SerializeField] private Door targetDoor;

        private int currentStep = 0;      //현재 진행 중인 정답 단계 (0부터 시작)
        private bool isSolved = false;    //퍼즐 성공 여부

        //발판 중복 감지 방지용 (이전에 감지한 발판 저장)
        private Collider2D lastSteppedPlatform = null;

        private void Start()
        {
            ValidateSettings();
        }

        private void ValidateSettings()
        {
            if (platformColliders == null || platformColliders.Count == 0)
            {
                Debug.LogWarning($"[{gameObject.name}] 등록된 발판(Platform Colliders)이 없습니다!");
            }

            if (correctSequence == null || correctSequence.Count == 0)
            {
                Debug.LogWarning($"[{gameObject.name}] 정답 순서(Correct Sequence)가 설정되지 않았습니다!");
            }
            else
            {
                //정답 인덱스 범위 예외 검사
                for (int i = 0; i < correctSequence.Count; i++)
                {
                    int index = correctSequence[i];
                    if (index < 0 || index >= platformColliders.Count)
                    {
                        Debug.LogWarning($"[{gameObject.name}] correctSequence[{i}]의 값({index})이 발판 목록 범위를 벗어났습니다!");
                    }
                }
            }

            if (targetDoor == null)
            {
                Debug.LogWarning($"[{gameObject.name}] 연결된 Target Door가 없습니다!");
            }
        }

        private void Update()
        {
            if (isSolved || platformColliders.Count == 0 || correctSequence.Count == 0) return;


            CheckPlatformStepped();
        }

        //플레이어가 발판 영역 안으로 들어왔는지 Overlap으로 감지
        private void CheckPlatformStepped()
        {
            Collider2D currentStepped = GetSteppedPlatform();

            if (currentStepped == null)
            {
                lastSteppedPlatform = null;
                return;
            }

            //동일한 발판에 서 있는 동안 중복 반응 방지
            if (currentStepped == lastSteppedPlatform) return;

            //새로운 발판을 밟았을 때
            lastSteppedPlatform = currentStepped;
            int steppedIndex = platformColliders.IndexOf(currentStepped);

            OnPlatformStepped(steppedIndex, currentStepped);
        }

        //현재 밟은 발판의 Collider2D 반환
        private Collider2D GetSteppedPlatform()
        {
            Collider2D[] results = new Collider2D[5];
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };

            foreach (var pad in platformColliders)
            {
                if (pad == null) continue;

                int count = pad.Overlap(filter, results);
                for (int i = 0; i < count; i++)
                {
                    //밟은 대상이 플레이어인지 확인
                    if (results[i] != null && (results[i].CompareTag("Player") || results[i].GetComponentInParent<Player.Move.PlayerMoveMent>() != null))
                    {
                        return pad;
                    }
                }
            }

            return null;
        }

        //발판을 밟았을 때 정답/오답 처리
        private void OnPlatformStepped(int platformIndex, Collider2D platformCollider)
        {
            int expectedIndex = correctSequence[currentStep];

            //이미 맞춘 단계를 동일한 발판으로 재방문했을 경우
            //(예: 1번 발판(정답) -> 2번 발판(정답) 진행 후 다시 1번 발판을 밟는 경우 안전하게 유지 또는 무시)
            if (currentStep > 0 && correctSequence[currentStep - 1] == platformIndex)
            {
                Debug.Log($"[{gameObject.name}] 직전에 맞춘 안전 발판({platformIndex}번)을 다시 밟았습니다. 상태 유지.");
                return;
            }

            //정답 발판을 올바르게 밟은 경우
            if (platformIndex == expectedIndex)
            {
                currentStep++;
                Debug.Log($"[{gameObject.name}] 정답 발판! ({platformIndex}번 발판) -> 진행 단계: {currentStep}/{correctSequence.Count}");

                //모든 정답 순서를 순서대로 맞췄다면 성공
                if (currentStep >= correctSequence.Count)
                {
                    SolvePuzzle();
                }
            }
            //틀린 발판을 밟은 경우 (초기화)
            else
            {
                Debug.LogWarning($"[{gameObject.name}] 오답 발판! ({platformIndex}번 발판) -> 진행 상황이 초기화됩니다.");
                ResetPuzzleProgress();
            }
        }

        //진행 상황 초기화
        private void ResetPuzzleProgress()
        {
            currentStep = 0;
            lastSteppedPlatform = null;
        }

        //퍼즐 성공 처리
        private void SolvePuzzle()
        {
            isSolved = true;
            Debug.Log($"[{gameObject.name}] 안전 발판 퍼즐 성공! 문을 엽니다.");

            if (targetDoor != null)
            {
                targetDoor.Open(); 
            }
        }
    }
}