using FourGuardians.CourseContent.CameraSystem;
using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary>
    /// CameraFollow2D 기반 구역 카메라 (Cinemachine 도입 전 방식)
    /// 현재 MapTest 씬에서는 컴포넌트가 비활성이고 MapCinemachineDirector가 대신 사용됨
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(Camera), typeof(CameraFollow2D))]
    public sealed class MapCameraController2D : MonoBehaviour
    {
        public Transform player;
        public Transform zoneRoot;
        public MapSafety2D safety;
        [Min(0f)] public float zoomSmoothTime = 0.2f;

        public MapCameraZone2D ActiveZone { get; private set; }
        public Rect EffectiveBounds { get; private set; }

        private MapCameraZone2D[] zones;
        private MapCameraTransition2D[] transitions;
        private Camera view;
        private CameraFollow2D follow;
        private float zoomVelocity;
        private bool snap = true;

        private void Awake()
        {
            view = GetComponent<Camera>();
            follow = GetComponent<CameraFollow2D>();
            zones = zoneRoot == null ? new MapCameraZone2D[0] : zoneRoot.GetComponentsInChildren<MapCameraZone2D>();
            transitions = zoneRoot == null ? new MapCameraTransition2D[0] : zoneRoot.GetComponentsInChildren<MapCameraTransition2D>();
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
            snap = true;
            zoomVelocity = 0f;
        }

        private void LateUpdate()
        {
            if (player == null || zones.Length == 0) return;

            Vector2 position = player.position;
            MapCameraZone2D selected = null;
            MapCameraTransition2D activeGate = null;

            //구역 선택 순서: 입구 전환 영역 → 진입 영역(우선순위, 같으면 현재 구역 유지) → 가장 가까운 구역
            foreach (var gate in transitions)
            {
                selected = gate.Select(position, ActiveZone);
                if (selected != null)
                {
                    activeGate = gate;
                    break;
                }
            }
            if (selected == null)
            {
                foreach (var zone in zones)
                    if (zone.WorldActivation.Contains(position)
                        && (selected == null || zone.priority > selected.priority || zone == ActiveZone && zone.priority == selected.priority))
                        selected = zone;
            }
            if (selected == null)
            {
                float distance = float.PositiveInfinity;
                foreach (var zone in zones)
                {
                    Rect area = zone.WorldActivation;
                    Vector2 closest = new Vector2(Mathf.Clamp(position.x, area.xMin, area.xMax), Mathf.Clamp(position.y, area.yMin, area.yMax));
                    float candidate = (closest - position).sqrMagnitude;
                    if (candidate < distance)
                    {
                        distance = candidate;
                        selected = zone;
                    }
                }
            }
            ActiveZone = selected;

            //입구 전환 영역 안에서는 두 구역의 경계와 줌을 위치에 따라 섞음
            Rect bounds = selected.WorldView;
            float requestedSize = selected.orthographicSize;
            if (activeGate != null && activeGate.negativeSide != null && activeGate.positiveSide != null)
            {
                Rect area = activeGate.WorldArea;
                float t = activeGate.vertical ? Mathf.InverseLerp(area.yMin, area.yMax, position.y) : Mathf.InverseLerp(area.xMin, area.xMax, position.x);
                t = Mathf.SmoothStep(0f, 1f, t);
                Rect a = activeGate.negativeSide.WorldView, b = activeGate.positiveSide.WorldView;
                bounds = new Rect(Vector2.Lerp(a.position, b.position, t), Vector2.Lerp(a.size, b.size, t));
                requestedSize = Mathf.Lerp(activeGate.negativeSide.orthographicSize, activeGate.positiveSide.orthographicSize, t);
            }
            EffectiveBounds = bounds;

            //경계보다 화면이 커지지 않도록 줌을 제한
            float aspect = Mathf.Max(0.01f, view.aspect);
            float fittingSize = Mathf.Min(bounds.height * 0.5f, bounds.width / (2f * aspect));
            float targetSize = Mathf.Min(requestedSize, fittingSize);
            float size = snap || zoomSmoothTime <= 0f ? targetSize : Mathf.SmoothDamp(view.orthographicSize, targetSize, ref zoomVelocity, zoomSmoothTime);
            view.orthographicSize = Mathf.Min(size, fittingSize);
            Vector2 half = new Vector2(view.orthographicSize * aspect, view.orthographicSize);
            follow.ConfigurePlatformerSettings(0.25f, 1.2f, 0.8f, 1.5f, bounds.min + half, bounds.max - half);
            if (snap)
            {
                follow.SnapToTarget();
                snap = false;
            }

            //기존 Follow의 픽셀 반올림까지 끝난 뒤 이 MapTest 카메라만 화면 전체를 경계 안에 둠
            Vector3 positionAfterFollow = transform.position;
            positionAfterFollow.x = Mathf.Clamp(positionAfterFollow.x, bounds.xMin + half.x, bounds.xMax - half.x);
            positionAfterFollow.y = Mathf.Clamp(positionAfterFollow.y, bounds.yMin + half.y, bounds.yMax - half.y);
            transform.position = positionAfterFollow;
        }
    }
}
