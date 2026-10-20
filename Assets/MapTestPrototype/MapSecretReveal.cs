using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary> 기존 비밀 통로의 입구 덮개만 해제. 보상 종류는 별도 앵커에 남겨 둠 </summary>
    public sealed class MapSecretReveal : MonoBehaviour
    {
        public GameObject cover;
        public Collider2D blocker;
        public PermanentClear progress;

        private void Start()
        {
            if (progress != null && progress.IsCleared) Apply();
        }

        public void Reveal()
        {
            Apply();
            if (progress != null) progress.MarkCleared();
        }

        //원본 타일은 지우지 않고 덮개와 막는 충돌체만 끄며, 근처 조작 안내도 숨김
        private void Apply()
        {
            if (cover != null) cover.SetActive(false);
            if (blocker != null) blocker.enabled = false;
            foreach (var hint in GetComponentsInChildren<MapInteractionHint>(true)) hint.enabled = false;
        }
    }
}
