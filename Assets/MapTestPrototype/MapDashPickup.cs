using FourGuardians.CourseContent.Movement;
using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary> 대시 획득을 진행도에 기록하고, 진행도 변경으로 생기는 자동저장 요청과 획득 안내를 그대로 사용 </summary>
    public sealed class MapDashPickup : MonoBehaviour
    {
        public MapGimmickPresentation presentation;

        private bool collected;

        private void Start()
        {
            //이미 획득한 저장 데이터면 획득물을 숨김
            if (SaveManager.Instance.Progress.HasFlag(MapPrototypeSession.DashFlag))
            {
                collected = true;
                gameObject.SetActive(false);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<BasicPlayerMovement2D>();
            if (player != null) Collect(player);
        }

        public void Collect(BasicPlayerMovement2D player)
        {
            if (collected || player == null) return;

            collected = true;
            SaveManager.Instance.Progress.SetFlag(MapPrototypeSession.DashFlag);
            player.SetDashUnlocked(true);
            if (presentation != null) presentation.ShowDashAcquired();
            gameObject.SetActive(false);
        }
    }
}
