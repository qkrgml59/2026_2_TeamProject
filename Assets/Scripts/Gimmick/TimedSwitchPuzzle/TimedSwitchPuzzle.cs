using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Gimmick.TimedAttackSwitch
{
    public class TimedAttackSwitchPuzzle : MonoBehaviour
    {
        [Header("공격받을 버튼 오브젝트")]
        [Tooltip("공격받을 버튼 오브젝트의 Transform을 Inspector에서 등록하세요.")]
        [SerializeField] private Transform switchObject;

        [Header("문이 열려 있는 시간 (초)")]
        [Tooltip("버튼을 때렸을 때 문이 열려 있을 제한 시간 (기본값: 5초)")]
        [SerializeField] private float duration = 5f;

        [Header("연결된 문")]
        [Tooltip("시간 제한 동안 열릴 Door 오브젝트")]
        [SerializeField] private Door targetDoor;

        private Coroutine timerCoroutine = null; 
        private bool isOpen = false;             

        private void Start()
        {
            ValidateSettings();
        }

        private void ValidateSettings()
        {
            if (switchObject == null)
                Debug.LogWarning($"[{gameObject.name}] Switch Object가 설정되지 않았습니다!");

            if (targetDoor == null)
                Debug.LogWarning($"[{gameObject.name}] 연결된 Target Door가 없습니다!");
        }

        /// <summary>
        /// 플레이어가 버튼을 공격했을 때 PlayerMoveMent 등에서 호출하는 함수
        /// </summary>
        public void OnAttacked(Transform hitObject)
        {
            //공격받은 대상이 등록된 버튼 오브젝트인지 확인
            if (switchObject == null || hitObject == null) return;
            if (hitObject != switchObject && hitObject.gameObject != switchObject.gameObject) return;

            Debug.Log($"[{gameObject.name}] 버튼 피격 감지! 문을 열고 {duration}초 타이머 시작");

            //문 열기
            if (targetDoor != null)
            {
                targetDoor.Open();
                isOpen = true;
            }

            //이미 타이머가 실행 중이라면 이전 타이머 중지
            if (timerCoroutine != null)
            {
                StopCoroutine(timerCoroutine);
                Debug.Log($"[{gameObject.name}] 타이머가 리셋되어 다시 {duration}초부터 시작합니다.");
            }

            //타이머 코루틴 시작
            timerCoroutine = StartCoroutine(Co_TimerRoutine());
        }

        private IEnumerator Co_TimerRoutine()
        {
            yield return new WaitForSeconds(duration);

            Debug.Log($"[{gameObject.name}] 제한 시간 종료! 문을 닫습니다.");

            if (targetDoor != null)
            {
                targetDoor.Close();
                isOpen = false;
            }

            timerCoroutine = null;
        }
    }
}