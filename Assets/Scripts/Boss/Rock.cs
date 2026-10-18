using FourGuardians.CourseContent.Combat;
using UnityEngine;

namespace Boss.Docheol
{
    public class Rock : MonoBehaviour
    {
        [Header("낙석 설정")]
        [SerializeField, Min(1)] private int damageAmount = 1; //플레이어에게 줄 피해량
        [SerializeField] private float knockbackForce = 3f;     //피격 시 넉백 힘
        [SerializeField] private float maxLifeTime = 5.0f;     //자동 파괴 시간

        [Header("이팩트")]
        [SerializeField] private GameObject hitEffectPrefab;   //파괴 이펙트

        private void Start()
        {
            //바닥에 떨어지지 않고 낙하 탈출하는 경우를 대비해 일정 시간 후 자동 파괴
            Destroy(gameObject, maxLifeTime);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            //플레이어 충돌 검사
            if (collision.CompareTag("Player"))
            {
                if (collision.TryGetComponent<Health2D>(out var playerHealth))
                {
                    //플레이어 반대 방향으로 넉백 벡터 계산
                    Vector2 knockbackDir = (collision.transform.position - transform.position).normalized;
                    Vector2 knockback = knockbackDir * knockbackForce;

                    //생성자를 통해 DamageInfo 전달
                    DamageInfo damageInfo = new DamageInfo(
                        damageAmount,
                        transform.position,
                        knockback,
                        gameObject
                    );

                    //피해 전달
                    playerHealth.TakeDamage(damageInfo);
                }

                DestroyRock();
            }
            //바닥(Ground) 충돌 검사
            else if (collision.CompareTag("Ground") || collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                DestroyRock();
            }
        }

        private void DestroyRock()
        {
            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }

    }

}
