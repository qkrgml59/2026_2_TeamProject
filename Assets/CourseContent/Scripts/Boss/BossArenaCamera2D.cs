using UnityEngine;

namespace FourGuardians.CourseContent.Boss
{
    /// <summary>
    /// 플레이어와 보스의 중간 지점을 따라가며 둘 사이 거리에 맞춰 화면 크기를 조절합니다.
    /// 보스전에서는 어느 한쪽이 화면 밖으로 사라지지 않게 하는 것이 핵심입니다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BossArenaCamera2D : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform boss;
        [SerializeField] private Vector2 arenaCenter = new Vector2(0f, 0.4f);
        [SerializeField] private Vector2 arenaMinimum = new Vector2(-4f, 0.4f);
        [SerializeField] private Vector2 arenaMaximum = new Vector2(4f, 1.6f);
        [SerializeField, Min(3f)] private float minimumSize = 5.6f;
        [SerializeField, Min(3f)] private float maximumSize = 6.8f;
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.18f;
        [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.22f;

        private Camera arenaCamera;
        private Vector3 positionVelocity;
        private float zoomVelocity;

        public void Configure(Transform newPlayer, Transform newBoss)
        {
            player = newPlayer;
            boss = newBoss;
        }

        private void Awake()
        {
            arenaCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (player == null || boss == null || arenaCamera == null)
            {
                return;
            }

            Vector2 midpoint = ((Vector2)player.position + (Vector2)boss.position) * 0.5f;
            // 세로 중심을 위로 올려 바닥보다 보스의 상체와 공격 궤적이 더 많이 보이게 한다.
            midpoint.y += 1.35f;
            midpoint.x = Mathf.Clamp(midpoint.x, arenaMinimum.x, arenaMaximum.x);
            midpoint.y = Mathf.Clamp(midpoint.y, arenaMinimum.y, arenaMaximum.y);

            Vector3 desiredPosition = new Vector3(midpoint.x, midpoint.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime);

            float horizontalDistance = Mathf.Abs(player.position.x - boss.position.x);
            float desiredSize = Mathf.Clamp(minimumSize + horizontalDistance * 0.075f, minimumSize, maximumSize);
            arenaCamera.orthographicSize = Mathf.SmoothDamp(
                arenaCamera.orthographicSize,
                desiredSize,
                ref zoomVelocity,
                zoomSmoothTime);
        }
    }
}
