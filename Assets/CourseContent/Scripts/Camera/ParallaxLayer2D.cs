using UnityEngine;

namespace FourGuardians.CourseContent.CameraSystem
{
    /// <summary>
    /// 카메라가 움직인 거리의 일부만큼 배경을 움직여 깊이감을 만듭니다.
    /// 0에 가까우면 멀리, 1에 가까우면 플레이 공간 가까이에 있는 것처럼 보입니다.
    /// </summary>
    public sealed class ParallaxLayer2D : MonoBehaviour
    {
        [Tooltip("이 레이어의 이동량을 계산할 기준 카메라입니다.")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("가로 카메라 이동량 중 이 레이어가 따라갈 비율입니다. 작을수록 멀리 보입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float horizontalMultiplier = 0.3f;
        [Tooltip("세로 카메라 이동량 중 이 레이어가 따라갈 비율입니다. 가로보다 작게 두면 안정적입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float verticalMultiplier = 0.1f;

        private Vector3 layerStartPosition;
        private Vector3 cameraStartPosition;

        public void Configure(Transform newCamera, float horizontal, float vertical)
        {
            cameraTransform = newCamera;
            horizontalMultiplier = Mathf.Clamp01(horizontal);
            verticalMultiplier = Mathf.Clamp01(vertical);
        }

        private void Start()
        {
            // 시작 위치를 기준으로 계산해야 카메라가 움직여도 배경의 원래 배치를 잃지 않는다.
            layerStartPosition = transform.position;

            if (cameraTransform != null)
            {
                cameraStartPosition = cameraTransform.position;
            }
        }

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                return;
            }

            Vector3 cameraMovement = cameraTransform.position - cameraStartPosition;
            // 핵심 공식: 배경 위치 = 시작 위치 + 카메라 이동량 × 거리별 배율
            transform.position = layerStartPosition + new Vector3(
                cameraMovement.x * horizontalMultiplier,
                cameraMovement.y * verticalMultiplier,
                0f);
        }
    }
}
