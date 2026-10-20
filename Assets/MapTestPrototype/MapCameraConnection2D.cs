using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary>
    /// 실제 지도에서 확인한 열린 입구
    /// 연결되지 않은 벽 너머 방에는 카메라 여유를 허용하지 않음
    /// </summary>
    public sealed class MapCameraConnection2D : MonoBehaviour
    {
        public Rect area;
        public MapCinemachineShot first;
        public MapCinemachineShot second;
        public MapCinemachineShot[] additionalShots = new MapCinemachineShot[0];

        public bool Contains(Vector2 point) => area.Contains(point);

        public bool Includes(MapCinemachineShot shot)
        {
            if (shot == first || shot == second) return true;
            foreach (var related in additionalShots)
                if (shot == related) return true;
            return false;
        }

        //입구 양쪽 방의 경계를 합친 범위. 고정 구도 방은 퍼즐 전체 내용 영역을 사용
        public Rect AllowedBounds
        {
            get
            {
                Rect a = first.fixedOverview ? first.requiredContent : first.BoundaryBounds;
                Rect b = second.fixedOverview ? second.requiredContent : second.BoundaryBounds;
                return Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.85f, .45f, 1f);
            Gizmos.DrawWireCube(area.center, area.size);
        }
    }
}
