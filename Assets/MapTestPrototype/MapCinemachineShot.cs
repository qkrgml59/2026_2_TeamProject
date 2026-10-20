using Unity.Cinemachine;
using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary>
    /// MapTest의 구도 한 개. 진입 범위는 월드 좌표이며 Excel 편집 영역과 독립적
    /// 실제 추적과 전환은 Cinemachine이 담당하고, 이 컴포넌트는 화면비에 맞는 렌즈만 준비
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CinemachineCamera), typeof(CinemachineConfiner2D))]
    public sealed class MapCinemachineShot : MonoBehaviour
    {
        [Tooltip("기존 방 경계를 직접 참조합니다. 연결된 경우 진입 영역과 Confiner 외곽은 원본 경계 값을 사용합니다.")]
        public MapCameraZone2D sourceZone;

        [Tooltip("어느 진입 영역에도 속하지 않는 연결부에서 가장 가까운 일반 방으로 선택할 수 있습니다.")]
        public bool nearestFallback;

        private Rect lastSourceBounds;
        private bool sourcePrepared;

        [Header("구도 진입 · 월드 좌표")]
        public Rect activationArea = new Rect(0f, 0f, 10f, 10f);
        public int areaPriority = 10;
        [Min(0f)] public float exitPadding = 0.4f;
        public bool fallback;

        [Header("시야 크기")]
        [Tooltip("기본 화면 반높이입니다. Cinemachine Lens는 이 값과 화면비로 갱신됩니다.")]
        [Min(0.1f)] public float orthographicSize = 3f;

        [Tooltip("플레이어 대신 아래 임시 범위 전체를 안정된 고정 구도로 보여 줍니다.")]
        public bool fixedOverview;

        [Tooltip("실제 퍼즐 배치가 아닌 구도 검토용 월드 범위입니다.")]
        public Rect requiredContent = new Rect(94f, 10.5f, 16.5f, 17.25f);
        [Min(0f)] public float contentPadding = 0.75f;

        public CinemachineCamera Camera => cameraComponent != null ? cameraComponent : cameraComponent = GetComponent<CinemachineCamera>();
        public CinemachineConfiner2D Confiner => confiner != null ? confiner : confiner = GetComponent<CinemachineConfiner2D>();
        public int LensInvalidationCount { get; private set; }
        public Rect BoundaryBounds { get; private set; }

        private CinemachineCamera cameraComponent;
        private CinemachineConfiner2D confiner;
        private float lastSize = -1f;
        private float lastAspect = -1f;
        private int lastShapeHash;

        public bool Contains(Vector2 point, bool retainCurrent)
        {
            Rect rect = activationArea;

            //현재 선택된 샷은 exitPadding만큼 넓게 판정해 경계에서 화면이 번갈아 바뀌지 않게 함
            if (retainCurrent)
            {
                rect.min -= Vector2.one * exitPadding;
                rect.max += Vector2.one * exitPadding;
            }
            return rect.Contains(point);
        }

        /// <summary> 보관되어 있던 13개 경계의 실제 값을 현재 Confiner에 연결 </summary>
        public void RefreshSourceZone()
        {
            if (sourceZone == null) return;

            activationArea = sourceZone.WorldActivation;
            areaPriority = sourceZone.priority;
            Rect bounds = sourceZone.WorldView;
            if (sourcePrepared && bounds == lastSourceBounds) return;

            if (Confiner.BoundingShape2D is PolygonCollider2D polygon)
            {
                //이 Collider는 카메라 계산 전용이며 물리 충돌에 참여하지 않음
                Vector2 Local(Vector2 p) => (Vector2)polygon.transform.InverseTransformPoint(p) - polygon.offset;
                polygon.points = new[] { Local(bounds.min), Local(new Vector2(bounds.xMax, bounds.yMin)),
                    Local(bounds.max), Local(new Vector2(bounds.xMin, bounds.yMax)) };
                Confiner.InvalidateBoundingShapeCache();
            }
            lastSourceBounds = bounds;
            sourcePrepared = true;
        }

        /// <summary> 화면비에 맞춰 렌즈 크기를 정하고, 렌즈·경계가 바뀐 경우에만 Confiner2D 캐시를 무효화 </summary>
        public void Prepare(Camera output)
        {
            if (output == null || Camera == null || Confiner == null) return;

            RefreshSourceZone();
            float aspect = Mathf.Max(0.1f, output.aspect);
            float requested = orthographicSize;
            if (fixedOverview)
            {
                Vector2 extent = requiredContent.size + Vector2.one * (2f * contentPadding);
                requested = Mathf.Max(requested, extent.y * 0.5f, extent.x / (2f * aspect));
                transform.position = new Vector3(requiredContent.center.x, requiredContent.center.y, -10f);
            }

            //경계 오브젝트는 물리 충돌이 없는 비활성 Collider. points에서 직접 범위를 읽음
            int shapeHash = 17;
            if (Confiner.BoundingShape2D is PolygonCollider2D polygon && polygon.pathCount > 0)
            {
                Vector2 min = Vector2.one * float.PositiveInfinity;
                Vector2 max = Vector2.one * float.NegativeInfinity;
                foreach (Vector2 local in polygon.points)
                {
                    Vector2 point = polygon.transform.TransformPoint(local + polygon.offset);
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                    unchecked
                    {
                        shapeHash = shapeHash * 31 + local.GetHashCode();
                    }
                }
                unchecked
                {
                    shapeHash = shapeHash * 31 + polygon.transform.lossyScale.GetHashCode();
                }
                BoundaryBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

                if (!fixedOverview)
                {
                    float fittingSize = Mathf.Min(BoundaryBounds.height * 0.5f, BoundaryBounds.width / (2f * aspect));
                    requested = Mathf.Min(requested, fittingSize);

                    //딱 맞는 축에서 내부 다각형의 면적이 0이 되는 경우는 기본 골격 기능으로 처리
                    //줌을 임의로 줄이지 않으며, 설정이 바뀔 때만 형상 캐시를 갱신
                    bool needSkeleton = requested >= fittingSize - 0.00001f;
                    var settings = Confiner.OversizeWindow;
                    if (settings.Enabled != needSkeleton || !Mathf.Approximately(settings.MaxWindowSize, orthographicSize))
                    {
                        settings.Enabled = needSkeleton;
                        settings.MaxWindowSize = orthographicSize;
                        settings.Padding = 0f;
                        Confiner.OversizeWindow = settings;
                        Confiner.InvalidateBoundingShapeCache();
                    }
                }
            }
            if (shapeHash != lastShapeHash)
            {
                Confiner.InvalidateBoundingShapeCache();
                lastShapeHash = shapeHash;
            }

            Camera.Lens.OrthographicSize = Mathf.Max(0.1f, requested);
            if (!Mathf.Approximately(lastSize, requested) || !Mathf.Approximately(lastAspect, aspect))
            {
                Confiner.InvalidateLensCache();
                LensInvalidationCount++;
                lastSize = requested;
                lastAspect = aspect;
            }
        }

        private void Update()
        {
            //편집 모드에서도 Scene/Game 뷰 구도를 바로 확인할 수 있게 렌즈를 준비
            if (!Application.isPlaying) Prepare(UnityEngine.Camera.main);
        }

        private void OnValidate()
        {
            activationArea.size = Vector2.Max(activationArea.size, Vector2.one * 0.1f);
            requiredContent.size = Vector2.Max(requiredContent.size, Vector2.one * 0.1f);
            lastSize = lastAspect = -1f;
            lastShapeHash = 0;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.15f);
            Gizmos.DrawWireCube(activationArea.center, activationArea.size);
            if (!fixedOverview) return;
            Gizmos.color = new Color(0.3f, 0.85f, 1f);
            Gizmos.DrawWireCube(requiredContent.center, requiredContent.size);
        }
    }
}
