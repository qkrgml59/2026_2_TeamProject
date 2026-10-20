using System;
using System.Collections;
using FourGuardians.CourseContent.Movement;
using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary>
    /// MapTest 전용 문. 이동, 잠금 충돌, 닫힐 때의 끼임 방지를 직접 관리
    /// (원본 Door는 이동이 끝나면 항상 충돌을 꺼서 닫힌 뒤에도 통과할 수 있으므로 별도로 만듦)
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MapTestDoor : MonoBehaviour
    {
        [Header("문 이동")]
        [SerializeField] private Vector2 moveDistance = Vector2.up * 2f;
        [SerializeField, Min(.01f)] private float moveDuration = 1.1f;
        [SerializeField] private BoxCollider2D doorCollider;

        public event Action<MapTestDoor> Opened;
        public bool IsOpen { get; private set; }
        public bool IsMoving { get; private set; }

        private Vector3 closedPosition;
        private Vector3 openedPosition;
        private Coroutine motion;
        private readonly Collider2D[] occupants = new Collider2D[16];

        private void Awake()
        {
            if (doorCollider == null) doorCollider = GetComponent<BoxCollider2D>();
            closedPosition = transform.position;
            openedPosition = closedPosition + (Vector3)moveDistance;
            doorCollider.enabled = true;
        }

        /// <summary> 통로를 열고 연출에 알림. 이미 열린 문을 다시 공격해도 연출은 반복하지 않음 </summary>
        public void Open()
        {
            if (IsOpen) return;

            StopMotion();
            IsOpen = true;
            doorCollider.enabled = false;
            motion = StartCoroutine(MoveTo(openedPosition, false));
            Opened?.Invoke(this);
        }

        public void Close()
        {
            //이미 닫힌 문에 Close가 다시 불려도 잠금 충돌이 풀리지 않도록 열린 상태에서만 처리
            if (!IsOpen) return;

            StopMotion();
            IsOpen = false;
            doorCollider.enabled = false;
            motion = StartCoroutine(MoveTo(closedPosition, true));
        }

        /// <summary> 저장된 영구 완료 상태를 연출 없이 복구 </summary>
        public void OpenImmediate()
        {
            StopMotion();
            IsOpen = true;
            transform.position = openedPosition;
            doorCollider.enabled = false;
        }

        private void StopMotion()
        {
            if (motion != null) StopCoroutine(motion);
            motion = null;
            IsMoving = false;
        }

        private IEnumerator MoveTo(Vector3 destination, bool closing)
        {
            IsMoving = true;
            Vector3 origin = transform.position;
            float elapsed = 0f;
            float duration = Mathf.Max(.01f, moveDuration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(origin, destination, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }
            transform.position = destination;
            IsMoving = false;

            if (closing)
            {
                //닫힘 이동이 끝난 뒤에만 점유를 확인. 플레이어가 벗어난 다음 충돌을 복구 (끼임 방지)
                while (PlayerOccupiesDoor()) yield return null;
                doorCollider.enabled = true;
            }
            motion = null;
        }

        private bool PlayerOccupiesDoor()
        {
            Vector2 center = transform.TransformPoint(doorCollider.offset);
            Vector2 scale = transform.lossyScale;
            Vector2 size = Vector2.Scale(doorCollider.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.OverlapBox(center, size, transform.eulerAngles.z, filter, occupants);
            for (int i = 0; i < count; i++)
            {
                if (occupants[i] != doorCollider && occupants[i].GetComponentInParent<BasicPlayerMovement2D>() != null)
                    return true;
            }
            return false;
        }
    }
}
