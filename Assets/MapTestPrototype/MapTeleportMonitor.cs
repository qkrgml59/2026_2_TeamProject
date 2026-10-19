using FourGuardians.CourseContent.Movement;
using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary> 기존 기믹이 Transform을 순간 이동시키면 MapTest 이동 상태와 카메라를 함께 정리 </summary>
    [DefaultExecutionOrder(900)]
    [RequireComponent(typeof(BasicPlayerMovement2D))]
    public sealed class MapTeleportMonitor : MonoBehaviour
    {
        public MapCinemachineDirector director;

        private BasicPlayerMovement2D movement;
        private Rigidbody2D body;
        private Vector3 previousPosition;
        private float previousSpeed;

        private void Awake()
        {
            movement = GetComponent<BasicPlayerMovement2D>();
            body = GetComponent<Rigidbody2D>();
            previousPosition = transform.position;
        }

        private void LateUpdate()
        {
            //빠른 낙하나 낮은 프레임 속도에서의 정상 이동을 순간 이동으로 오인하지 않도록 속도에 비례한 여유를 둠
            float speed = body.linearVelocity.magnitude;
            float expectedTravel = Mathf.Max(1.5f, Mathf.Max(speed, previousSpeed) * Time.deltaTime * 2f + .5f);
            if ((transform.position - previousPosition).sqrMagnitude > expectedTravel * expectedTravel)
            {
                movement.ResetAfterTeleport(transform.position);
                Physics2D.SyncTransforms();
                if (director != null) director.RequestSnap();
            }
            previousPosition = transform.position;
            previousSpeed = speed;
        }
    }
}
