using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary> 카메라 구역 하나의 화면 경계와 진입 영역. 두 사각형 모두 Transform 위치 기준 </summary>
    public sealed class MapCameraZone2D : MonoBehaviour
    {
        [Tooltip("카메라 화면 전체가 들어가야 하는 경계. Transform 위치 기준입니다.")]
        public Rect viewBounds = new Rect(-5f, -3f, 10f, 6f);

        [Tooltip("플레이어가 이 영역에 들어오면 이 카메라 구역을 선택합니다. 경계와 독립적으로 편집합니다.")]
        public Rect activationArea = new Rect(-5f, -3f, 10f, 6f);

        [Min(0.1f)] public float orthographicSize = 3.5f;
        public int priority;

        public Rect WorldView => ToWorld(viewBounds);
        public Rect WorldActivation => ToWorld(activationArea);

        private Rect ToWorld(Rect value) => new Rect(value.position + (Vector2)transform.position, value.size);

        private void OnValidate()
        {
            viewBounds.size = Vector2.Max(viewBounds.size, Vector2.one * 0.1f);
            activationArea.size = Vector2.Max(activationArea.size, Vector2.one * 0.1f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.15f, 0.8f, 1f);
            Gizmos.DrawWireCube(WorldView.center, WorldView.size);
            Gizmos.color = new Color(1f, 0.7f, 0.15f);
            Gizmos.DrawWireCube(WorldActivation.center, WorldActivation.size);
        }
    }
}
