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

        private void Awake()
        {
            if (doorCollider == null)
            {
                doorCollider = GetComponent<Collider2D>();
            }
        }

        ///<summary>
        ///SequencePuzzle에서 호출하는 문 열기 함수
        ///</summary>
        public void Open()
        {
            if (isOpen) return;

            isOpen = true;
            StartCoroutine(Co_OpenDoor());
        }

        private IEnumerator Co_OpenDoor()
        {
            Vector3 startPosition = transform.position;
            Vector3 targetPosition = startPosition + (Vector3)moveDistance;
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
        }
    }
}