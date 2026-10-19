using FourGuardians.CourseContent.Combat;
using FourGuardians.CourseContent.Movement;
using UnityEngine;
using UnityEngine.Events;

namespace Hwanjo.MapPlay
{
    /// <summary> 기존 공격 Hitbox를 기존 퍼즐의 공개 함수에 연결하는 씬 수신점 </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class MapAttackTarget : MonoBehaviour
    {
        public UnityEvent onAttacked = new UnityEvent();
        public int ReceivedCount { get; private set; }

        private float lastHit = -10f;
        private Collider2D lastWeapon;
        private bool receivedInWindow;

        private void Update()
        {
            //무기 Hitbox가 꺼지면(공격 판정 구간 종료) 다음 공격을 받을 수 있게 함
            if (lastWeapon == null || !lastWeapon.enabled) receivedInWindow = false;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryReceive(other);
        private void OnTriggerStay2D(Collider2D other) => TryReceive(other);

        //플레이어의 무기 Hitbox만 받고, 같은 공격 판정 구간에서는 한 번만 처리
        private void TryReceive(Collider2D other)
        {
            if (!other.enabled || other.GetComponent<DamageHitbox2D>() == null ||
                other.GetComponentInParent<BasicPlayerMovement2D>() == null) return;
            if (lastWeapon == other && receivedInWindow) return;

            lastWeapon = other;
            receivedInWindow = true;
            ReceiveAttack(other.gameObject);
        }

        public void ReceiveAttack(GameObject source)
        {
            if (!isActiveAndEnabled || Time.time - lastHit < .08f) return;

            lastHit = Time.time;
            ReceivedCount++;
            onAttacked.Invoke();
        }
    }
}
