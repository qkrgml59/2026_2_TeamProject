using UnityEngine;

namespace CameraFollw
{
    ///<summary>
    ///플레이어를 부드럽게 따라가는 2D 카메라
    ///LateUpdate를 사용하면 플레이어 이동이 끝난 뒤 카메라가 움직여 화면 떨림이 줄어듭니다
    ///</summary>

    public sealed class CameraFollow : MonoBehaviour
    {
        [Header("추적 대상")]
        [Tooltip("카메라가 따라갈 플레이어 Transform입니다.")]
        [SerializeField] private Transform target;
        [Tooltip("플레이어를 화면 중앙에서 얼마나 옮겨 보이게 할지 정합니다.")]
        [SerializeField] private Vector2 offset = new Vector2(0f, 1f);

        [Header("가로 추적")]
        [Tooltip("작을수록 좌우 이동을 빠르게 따라가고, 클수록 느긋하게 따라갑니다.")]
        [Min(0.01f)]
        [SerializeField] private float horizontalSmoothTime = 0.12f;

        [Header("세로 추적")]
        [Tooltip("점프와 착지에서 사용하는 세로 추적 완화 시간입니다.")]
        [Min(0.01f)]
        [SerializeField] private float verticalSmoothTime = 0.25f;
        [Tooltip("플레이어가 이 거리보다 위로 벗어나기 전에는 카메라를 올리지 않습니다.")]
        [Min(0f)]
        [SerializeField] private float upperDeadZone = 1.2f;
        [Tooltip("플레이어가 이 거리보다 아래로 벗어나기 전에는 카메라를 내리지 않습니다.")]
        [Min(0f)]
        [SerializeField] private float lowerDeadZone = 0.8f;

        [Header("낙하 선행")]
        [Tooltip("낙하 속도를 확인하기 위한 플레이어 Rigidbody2D입니다. 비어 있으면 자동으로 찾습니다.")]
        [SerializeField] private Rigidbody2D targetBody;
        [Tooltip("세로 속도가 이 값보다 작아지면 빠른 낙하로 판단합니다.")]
        [SerializeField] private float fallSpeedThreshold = -4f;
        [Tooltip("빠르게 낙하할 때 착지 지점을 보여주기 위해 카메라를 아래로 이동하는 거리입니다.")]
        [Min(0f)]
        [SerializeField] private float fallLookAhead = 1.5f;

        [Header("카메라 중심 제한")]
        [Tooltip("카메라 중심이 이동할 수 있는 월드 좌표의 최솟값입니다.")]
        [SerializeField] private Vector2 minimumPosition = new Vector2(-21f, 0f);
        [Tooltip("카메라 중심이 이동할 수 있는 월드 좌표의 최댓값입니다.")]
        [SerializeField] private Vector2 maximumPosition = new Vector2(21f, 4f);

        [Header("도트 화면")]
        [Tooltip("카메라 위치를 픽셀 격자에 맞춰 도트 배경의 미세한 흔들림을 줄입니다.")]
        [SerializeField] private bool usePixelSnap = true;
        [Tooltip("Sprite Import Settings에서 사용하는 Pixels Per Unit과 같은 값을 입력합니다.")]
        [Min(1f)]
        [SerializeField] private float pixelsPerUnit = 64f;

        private float horizontalVelocity;
        private float verticalVelocity;
        private float desiredVerticalPosition;

        private void Awake()
        {
            //이전 버전의 씬을 열어도 새 낙하 선행 기능이 바로 동작하도록 참조를 복구한다.
            if (target != null && targetBody == null)
            {
                targetBody = target.GetComponent<Rigidbody2D>();
            }

            desiredVerticalPosition = transform.position.y;
        }

        public void Configure(Transform newTarget, Vector2 newOffset, float newSmoothTime)
        {
            target = newTarget;
            offset = newOffset;
            horizontalSmoothTime = Mathf.Max(0.01f, newSmoothTime);
            targetBody = target == null ? null : target.GetComponent<Rigidbody2D>();
            desiredVerticalPosition = transform.position.y;
        }

        public void ConfigurePlatformerSettings(
            float newVerticalSmoothTime,
            float newUpperDeadZone,
            float newLowerDeadZone,
            float newFallLookAhead,
            Vector2 newMinimumPosition,
            Vector2 newMaximumPosition)
        {
            verticalSmoothTime = Mathf.Max(0.01f, newVerticalSmoothTime);
            upperDeadZone = Mathf.Max(0f, newUpperDeadZone);
            lowerDeadZone = Mathf.Max(0f, newLowerDeadZone);
            fallLookAhead = Mathf.Max(0f, newFallLookAhead);
            minimumPosition = Vector2.Min(newMinimumPosition, newMaximumPosition);
            maximumPosition = Vector2.Max(newMinimumPosition, newMaximumPosition);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            float targetY = target.position.y + offset.y;

            //데드존 안의 작은 점프에는 반응하지 않아 화면이 덜 출렁인다.
            if (targetY > desiredVerticalPosition + upperDeadZone)
            {
                desiredVerticalPosition = targetY - upperDeadZone;
            }
            else if (targetY < desiredVerticalPosition - lowerDeadZone)
            {
                desiredVerticalPosition = targetY + lowerDeadZone;
            }

            //빠르게 낙하할 때는 플레이어 아래를 미리 보여줘 착지 지점을 확인하게 한다.
            float fallOffset = 0f;

            if (targetBody != null && targetBody.linearVelocity.y < fallSpeedThreshold)
            {
                fallOffset = -fallLookAhead;
            }

            float desiredX = Mathf.SmoothDamp(
                transform.position.x,
                target.position.x + offset.x,
                ref horizontalVelocity,
                horizontalSmoothTime);
            float desiredY = Mathf.SmoothDamp(
                transform.position.y,
                desiredVerticalPosition + fallOffset,
                ref verticalVelocity,
                verticalSmoothTime);

            desiredX = Mathf.Clamp(desiredX, minimumPosition.x, maximumPosition.x);
            desiredY = Mathf.Clamp(desiredY, minimumPosition.y, maximumPosition.y);

            if (usePixelSnap)
            {
                //월드 좌표를 1 / PPU 간격으로 반올림하면 픽셀 사이의 위치에 카메라가 놓이지 않는다.
                float safePixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
                desiredX = Mathf.Round(desiredX * safePixelsPerUnit) / safePixelsPerUnit;
                desiredY = Mathf.Round(desiredY * safePixelsPerUnit) / safePixelsPerUnit;
            }

            transform.position = new Vector3(desiredX, desiredY, transform.position.z);
        }
    }

}


