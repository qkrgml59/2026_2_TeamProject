using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary> 두 카메라 구역이 맞닿는 입구에서 중심선 기준으로 양쪽 구역을 전환 </summary>
    public sealed class MapCameraTransition2D : MonoBehaviour
    {
        [Tooltip("이 영역 안에서 두 구역 사이를 양방향으로 전환합니다.")]
        public Rect area = new Rect(-1f, -1f, 2f, 2f);
        public bool vertical;
        public MapCameraZone2D negativeSide;
        public MapCameraZone2D positiveSide;
        [Min(0f)] public float hysteresis = 0.15f;

        public Rect WorldArea => new Rect(area.position + (Vector2)transform.position, area.size);

        private void OnValidate()
        {
            area.size = Vector2.Max(area.size, Vector2.one * 0.1f);
        }

        public MapCameraZone2D Select(Vector2 point, MapCameraZone2D current)
        {
            if (!WorldArea.Contains(point)) return null;

            float delta = vertical ? point.y - WorldArea.center.y : point.x - WorldArea.center.x;

            //중심선 근처에서는 현재 구역을 유지해 빠르게 왕복할 때 화면이 번갈아 바뀌지 않게 함
            if (Mathf.Abs(delta) < hysteresis && (current == negativeSide || current == positiveSide)) return current;
            return delta < 0f ? negativeSide : positiveSide;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.4f, 1f);
            Gizmos.DrawWireCube(WorldArea.center, WorldArea.size);
        }
    }
}
