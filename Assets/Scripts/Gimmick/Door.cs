using System.Collections;
using UnityEngine;

namespace Gimmick
{
    public class Door : MonoBehaviour
    {
        [Header("문 이동 설정")]
        [Tooltip("문이 열릴 때 이동할 방향과 거리")]
        [SerializeField] private Vector2 moveDistance = new Vector2(0f, 3f);

        [Tooltip("문이 완전히 열리는 데 걸리는 시간(초)")]
        [SerializeField] private float moveDuration = 1.5f;

        [Header("콜라이더 설정")]
        [Tooltip("문이 열렸을 때 통과 가능하도록 꺼줄 Collider2D")]
        [SerializeField] private Collider2D doorCollider;

        private bool isOpen = false;
        private Vector3 closedPosition;   
        private Vector3 openedPosition;   
        private Coroutine doorCoroutine;  

        private void Awake()
        {
            if (doorCollider == null)
            {
                doorCollider = GetComponent<Collider2D>();
            }

            closedPosition = transform.position;
            openedPosition = closedPosition + (Vector3)moveDistance;

        }

        ///<summary>
        ///SequencePuzzle에서 호출하는 문 열기 함수
        ///</summary>
        public void Open()
        {
            if (isOpen) return;

            isOpen = true;

            if (doorCoroutine != null) StopCoroutine(doorCoroutine);
            doorCoroutine = StartCoroutine(Co_MoveDoor(openedPosition, false));
        }

        public void Close()
        {
            if (!isOpen) return;

            isOpen = false;

            if (doorCoroutine != null) StopCoroutine(doorCoroutine);
            doorCoroutine = StartCoroutine(Co_MoveDoor(closedPosition, true));
        }

        /// <summary> 클리어 한 퍼즐용 오픈 </summary>
        public void OpenImmediate()
        {
            if (doorCoroutine != null)
            {
                StopCoroutine(doorCoroutine);
                doorCoroutine = null;
            }

            isOpen = true;
            transform.position = openedPosition;

            if (doorCollider != null)
                doorCollider.enabled = false;
        }

        private IEnumerator Co_MoveDoor(Vector3 targetPosition, bool enableColliderOnEnd)
        {

            if (enableColliderOnEnd && doorCollider != null)
            {
                doorCollider.enabled = true;
            }

            Vector3 startPosition = transform.position;
            float elapsedTime = 0f;

            while (elapsedTime < moveDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / moveDuration;
                t = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            transform.position = targetPosition;

            if (doorCollider != null)
            {
                doorCollider.enabled = false;
            }

            doorCoroutine = null;
        }
    }
}