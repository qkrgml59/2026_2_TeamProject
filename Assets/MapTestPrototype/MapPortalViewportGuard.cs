using Unity.Cinemachine;
using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary>
    /// 연결 입구의 전환 대기 중 플레이어가 화면 밖으로 사라지는 현상만 보정
    /// Cinemachine의 최종 가상 카메라 상태를 조정하며 실제 Camera Transform은 직접 쓰지 않음
    /// 입구 밖에서는 원래 Confiner 결과를 그대로 사용
    /// </summary>
    public sealed class MapPortalViewportGuard : CinemachineExtension
    {
        public MapCinemachineDirector director;
        [Min(0f)] public float playerPadding = .06f;

        private MapCinemachineShot shot;
        private Collider2D playerCollider;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize || director == null || !director.isActiveAndEnabled
                || director.ActiveConnection == null) return;

            shot ??= GetComponent<MapCinemachineShot>();
            if (shot == null || !director.ActiveConnection.Includes(shot)) return;

            //현재 고정 퍼즐 구도는 그대로 두고, 그 구도에서 빠져나가는 블렌드만 떠나는 플레이어를 따라가게 허용
            if (shot.fixedOverview && director.ActiveShot == shot) return;

            playerCollider ??= director.player.GetComponent<Collider2D>();
            if (playerCollider == null) return;

            Bounds player = playerCollider.bounds;
            float halfY = state.Lens.OrthographicSize, halfX = halfY * director.GetComponent<Camera>().aspect;
            Vector3 position = state.GetFinalPosition(), wanted = position;

            //화면에 이미 들어온 경우 보정은 0. 필요한 거리만큼만 연결된 이웃 쪽으로 여유를 줌
            wanted.x = Mathf.Clamp(wanted.x, player.max.x + playerPadding - halfX, player.min.x - playerPadding + halfX);
            wanted.y = Mathf.Clamp(wanted.y, player.max.y + playerPadding - halfY, player.min.y - playerPadding + halfY);
            if (!shot.fixedOverview)
            {
                Rect allowed = director.ActiveConnection.AllowedBounds;
                wanted.x = Mathf.Clamp(wanted.x, allowed.xMin + halfX, allowed.xMax - halfX);
                wanted.y = Mathf.Clamp(wanted.y, allowed.yMin + halfY, allowed.yMax - halfY);
            }
            state.PositionCorrection += wanted - position;
        }
    }
}
