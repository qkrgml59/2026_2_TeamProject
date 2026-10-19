using Unity.Cinemachine;
using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary>
    /// 방 진입 영역으로 가상 카메라를 선택. 실제 화면 위치와 줌은 Cinemachine Brain만 갱신
    /// 기존 13개 경계와 입구 두 곳을 직접 연결하며, 영역 밖에서는 가장 가까운 일반 방 경계를 유지
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CinemachineBrain), typeof(Camera))]
    public sealed class MapCinemachineDirector : MonoBehaviour
    {
        public Transform player;
        public Transform shotRoot;
        public Transform sourceZoneRoot;

        [Tooltip("진입 기준점을 플레이어 발 위치 대신 콜라이더 중심으로 잡습니다.")]
        public bool useColliderCenterForSelection;
        public MapSafety2D safety;

        [Tooltip("빠른 낙하 중에만 시선을 아래로 옮깁니다.")]
        [Min(0f)] public float fallLookAhead = 0.75f;
        public float fallSpeedThreshold = -4f;
        [Min(0.01f)] public float fallOffsetSmoothTime = 0.25f;

        public MapCinemachineShot ActiveShot { get; private set; }
        public int ShotChangeCount { get; private set; }
        public MapCameraConnection2D ActiveConnection { get; private set; }
        public bool UsedNearestFallback { get; private set; }

        private MapCinemachineShot[] shots;
        private MapCameraConnection2D[] connections;
        private MapCameraTransition2D[] transitions;
        private CinemachineBrain brain;
        private Camera output;
        private Rigidbody2D playerBody;
        private Collider2D playerCollider;
        private Vector3 previousPlayerPosition;
        private bool snapRequested = true;
        private float fallOffset, fallVelocity;

        private void Awake()
        {
            brain = GetComponent<CinemachineBrain>();
            output = GetComponent<Camera>();
            shots = shotRoot == null ? new MapCinemachineShot[0] : shotRoot.GetComponentsInChildren<MapCinemachineShot>();
            connections = shotRoot == null ? new MapCameraConnection2D[0] : shotRoot.GetComponentsInChildren<MapCameraConnection2D>();
            transitions = sourceZoneRoot == null ? new MapCameraTransition2D[0] : sourceZoneRoot.GetComponentsInChildren<MapCameraTransition2D>(true);
            playerBody = player == null ? null : player.GetComponent<Rigidbody2D>();
            playerCollider = player == null ? null : player.GetComponent<Collider2D>();
            if (player != null) previousPlayerPosition = player.position;
        }

        private void OnEnable()
        {
            if (safety != null) safety.Returned += RequestSnap;
        }

        private void OnDisable()
        {
            if (safety != null) safety.Returned -= RequestSnap;
        }

        public void RequestSnap()
        {
            snapRequested = true;
        }

        private MapCinemachineShot Select(Vector2 position)
        {
            UsedNearestFallback = false;
            MapCinemachineShot selected = null;
            foreach (var shot in shots) shot.RefreshSourceZone();

            //신전의 기존 입구 두 곳은 방향과 히스테리시스까지 그대로 이용
            foreach (var transition in transitions)
            {
                var zone = transition.Select(position, !snapRequested && ActiveShot != null ? ActiveShot.sourceZone : null);
                if (zone == null) continue;
                foreach (var shot in shots)
                    if (shot.isActiveAndEnabled && shot.sourceZone == zone) return shot;
            }

            //진입 영역 안의 샷 중 우선순위가 높은 샷. 같으면 현재 샷 유지 (현재 샷은 exitPadding만큼 넓게 판정)
            foreach (var shot in shots)
            {
                if (!shot.isActiveAndEnabled || shot.fallback) continue;
                if (shot.Contains(position, !snapRequested && shot == ActiveShot)
                    && (selected == null || shot.areaPriority > selected.areaPriority
                        || shot == ActiveShot && shot.areaPriority == selected.areaPriority)) selected = shot;
            }
            if (selected != null) return selected;

            //어느 진입 영역에도 없으면 가장 가까운 일반 방. 고정 퍼즐 구도는 제외
            UsedNearestFallback = true;
            float closestDistance = float.PositiveInfinity;
            foreach (var shot in shots)
            {
                if (!shot.isActiveAndEnabled || !shot.nearestFallback || shot.fixedOverview) continue;
                Rect area = shot.activationArea;
                Vector2 closest = new Vector2(Mathf.Clamp(position.x, area.xMin, area.xMax), Mathf.Clamp(position.y, area.yMin, area.yMax));
                float distance = (closest - position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    selected = shot;
                }
            }

            //이전 씬 설정과의 호환용. 새 MapTest에는 통짜 맵 fallback을 배치하지 않음
            if (selected == null)
                foreach (var shot in shots)
                    if (shot.isActiveAndEnabled && shot.fallback) selected = shot;
            return selected;
        }

        private void Update()
        {
            if (player == null || shots == null || shots.Length == 0) return;

            Vector2 selectionPoint = useColliderCenterForSelection && playerCollider != null ? playerCollider.bounds.center : player.position;
            var selected = Select(selectionPoint);
            if (selected == null) return;

            var previousConnection = ActiveConnection;
            ActiveConnection = null;
            foreach (var connection in connections)
                if (connection.Contains(selectionPoint) && connection.Includes(selected))
                {
                    ActiveConnection = connection;
                    break;
                }

            //입구 여유 허용은 입구 전환에 속하므로, 입구를 벗어나도 그 블렌드가 끝날 때까지 유지
            if (ActiveConnection == null && brain.IsBlending && previousConnection != null
                && previousConnection.Includes(selected)) ActiveConnection = previousConnection;
            if (selected != ActiveShot)
            {
                ActiveShot = selected;
                ShotChangeCount++;
            }

            float wantedOffset = playerBody != null && playerBody.linearVelocity.y < fallSpeedThreshold ? -fallLookAhead : 0f;
            fallOffset = snapRequested ? wantedOffset : Mathf.SmoothDamp(fallOffset, wantedOffset, ref fallVelocity, fallOffsetSmoothTime);

            //선택된 샷만 우선순위를 높이고, 순간 이동 요청이 있으면 모든 샷의 이전 추적 상태를 버림
            foreach (var shot in shots)
            {
                shot.Camera.Priority = shot == selected ? 100 : 0;
                shot.Prepare(output);
                var composer = shot.GetComponent<CinemachinePositionComposer>();
                if (composer != null) composer.TargetOffset = new Vector3(0f, 0.25f + fallOffset, 0f);
                if (!snapRequested) continue;
                if (!shot.fixedOverview && shot.Camera.PreviousStateIsValid) shot.Camera.OnTargetObjectWarped(player, player.position - previousPlayerPosition);
                shot.Camera.PreviousStateIsValid = false;
            }
            if (snapRequested)
            {
                brain.ResetState();
                fallVelocity = 0f;
                snapRequested = false;
            }
            previousPlayerPosition = player.position;
        }
    }
}
