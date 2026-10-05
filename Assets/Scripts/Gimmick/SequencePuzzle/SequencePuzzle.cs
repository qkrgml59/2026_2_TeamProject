using Gimmick;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle.Sequence
{
    public class SequencePuzzle : MonoBehaviour
    {
        [Header("퍼즐 오브젝트 목록")]
        [Tooltip("이 퍼즐에 사용되는 모든 오브젝트를 등록하세요.")]
        [SerializeField] private List<SequencePuzzleObject> puzzleObjects = new List<SequencePuzzleObject>();

        [Header("정답 순서 설정")]
        [Tooltip("공격해야 하는 정답 순서대로 오브젝트를 드래그해서 배치하세요.")]
        [SerializeField] private List<SequencePuzzleObject> answerSequence = new List<SequencePuzzleObject>();

        [Header("연결된 문")]
        [SerializeField] private Door targetDoor;

        [Header("리셋 설정")]
        [Tooltip("실패 시 초기화 대기 시간(초)")]
        [SerializeField] private float resetDelay = 0.8f;

        private int currentIndex = 0;
        private bool isSolved = false;
        private bool isResetting = false;

        public void Start()
        {
            //오브젝트 퍼즐 연결
           foreach(var obj in puzzleObjects)
           {
                if(obj !=null)
                {
                    obj.Init(this);
                }
           }
           

           //퍼즐 순서가 맞는지 검사
            ValidateSettings();
        }

        private void ValidateSettings()
        {
            foreach(var answerObj in answerSequence)
            {
                if(answerObj !=null && !puzzleObjects.Contains(answerObj))
                {
                    Debug.LogError($"[{gameObject.name}] Answer Sequence에 포함된 '{answerObj.name}' 오브젝트가 Puzzle Objects 목록에 없습니다! Inspector 설정을 확인하세요.");
                }
            }
        }

        ///<summary>
        ///SequencePuzzleObject에서 공격 신호를 받을 때 실행되는 정답 판정
        ///</summary>
        public void CheckPuzzle(SequencePuzzleObject hitObject)
        {
            if (isSolved || isResetting) return;

            //현재 순서의 정답 오브젝트와 일치하는지 확인
            if (answerSequence[currentIndex] == hitObject)
            {
                // 정답 처리
                hitObject.SetSuccessState();
                currentIndex++;

                //모든 정답 순서를 다 맞춘 경우
                if (currentIndex >= answerSequence.Count)
                {
                    SolvePuzzle();
                }
            }
            else
            {
                //오답 처리
                StartCoroutine(Co_HandleFail(hitObject));
            }
        }

        private IEnumerator Co_HandleFail(SequencePuzzleObject failedObject)
        {
            isResetting = true;

            //오답 효과 실행
            StartCoroutine(failedObject.FailEffect());

            yield return new WaitForSeconds(resetDelay);

            //해당 퍼즐의 모든 오브젝트 초기화
            foreach (var obj in puzzleObjects)
            {
                if (obj != null)
                {
                    obj.ResetObject();
                }
            }

            currentIndex = 0;
            isResetting = false;
        }

        private void SolvePuzzle()
        {
            isSolved = true;

            if (targetDoor != null)
            {
                targetDoor.Open();
            }
        }

    }
}


