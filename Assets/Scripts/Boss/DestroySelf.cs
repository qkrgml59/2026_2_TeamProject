using UnityEngine;

namespace Boss.Warning
{
    public class DestroySelf : MonoBehaviour
    {
        [Header("자동 파괴 시간 설정")]
        [Tooltip("몇 초 뒤에 이 오브젝트를 파괴할지 설정합니다")]
        public float destroyDelay = 0.8f;

        private void Strart()
        {
            Destroy(gameObject, destroyDelay);
        }
     
    }
}

