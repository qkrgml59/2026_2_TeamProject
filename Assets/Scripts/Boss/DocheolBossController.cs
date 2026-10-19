using FourGuardians.CourseContent.Combat;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Boss.Docheol
{
    ///<summary>
    ///도철 보스 컨트롤러
    ///</summary>
    public class DocheolBossController : MonoBehaviour
    {
        [Header("기본 상태 정보")]
        public float maxHealth = 1000f;
        public float currentHealth;
        public bool isFirstChallenge = true;    //첫 번째 도전 여부
        public Transform playerTransform;       //플레이어 트랜스폼 참조
        private Health2D playerHealth;          //플레이어 Health2D 참조

        [Header("페이즈 관리")]
        public int currentPhase = 1;
        private bool hasUsedLateRoar = false;   //체력 20% 이하 후반 포효/낙석 사용 여부

        [Header("전투 거리 및 이동 설정")]
        public float attackRange = 3.0f;        //근접 공격 가능 거리
        public float moveSpeed = 3.0f;          //접근/후퇴 이동 속도
        public float dashCooldown = 5.0f;       //땅굴/특수기 쿨타임
        private float dashCooldownTimer = 0f;

        [Header("땅굴 및 패턴 연쇄 기록")]
        private string lastBurrowAttack = "";   //직전 땅굴 공격 이름
        private int consecutiveCount = 0;       //동일 땅굴 공격 연속 횟수

        [Header("외부 컴포넌트 및 프리팹 참조")]
        public Animator animator;
        public Rigidbody2D rb;
        public GameObject rockPrefab;              //낙석 프리팹
        public GameObject warningMarkerPrefab;     //낙석 경고 표시 프리팹
        public Transform rockSpawnAreaMin;         //낙석 생성 범위 최소점
        public Transform rockSpawnAreaMax;         //낙석 생성 범위 최대점

        //보스 동작 상태 플래그
        public bool isBusy { get; private set; } = false;
        public bool isDead { get; private set; } = false;
        public bool isFacingRight = false;

        //Animator Trigger 파라미터 Hash
        private readonly int hashCutsceneIntro = Animator.StringToHash("CutsceneIntro");
        private readonly int hashQuickIntro = Animator.StringToHash("QuickIntro");
        private readonly int hashRoar = Animator.StringToHash("Roar");
        private readonly int hashPhase2Roar = Animator.StringToHash("Phase2Roar");
        private readonly int hashRockfallRoar = Animator.StringToHash("RockfallRoar");
        private readonly int hashDie = Animator.StringToHash("Die");
        private readonly int hashPrepareTackle = Animator.StringToHash("PrepareTackle");
        private readonly int hashDoTackle = Animator.StringToHash("DoTackle");
        private readonly int hashRecovery = Animator.StringToHash("Recovery");
        private readonly int hashPrepareSwipe = Animator.StringToHash("PrepareSwipe");
        private readonly int hashDoSwipe = Animator.StringToHash("DoSwipe");
        private readonly int hashPrepareSlam = Animator.StringToHash("PrepareSlam");
        private readonly int hashDoSlam = Animator.StringToHash("DoSlam");
        private readonly int hashPrepareFiveCombo = Animator.StringToHash("PrepareFiveCombo");
        private readonly int hashComboStrike = Animator.StringToHash("ComboStrike");
        private readonly int hashFinalStrike = Animator.StringToHash("FinalStrike");
        private readonly int hashBurrowDown = Animator.StringToHash("BurrowDown");
        private readonly int hashUnburrow = Animator.StringToHash("Unburrow");
        private readonly int hashLongLeapStart = Animator.StringToHash("LongLeapStart");
        private readonly int hashLongLeapLand = Animator.StringToHash("LongLeapLand");

        private void Start()
        {
            currentHealth = maxHealth;

            if (animator == null) animator = GetComponent<Animator>();

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                playerHealth = playerObj.GetComponent<Health2D>();

                if (playerHealth != null)
                {
                    playerHealth.Died += OnPlayerDied;
                }
                Debug.Log("[도철 보스] 플레이어를 성공적으로 감지했습니다.");
            }
            else
            {
                Debug.LogError("[도철 보스] 'Player' 태그를 가진 오브젝트를 찾지 못했습니다!");
            }

            StartCoroutine(SequenceBossIntro());
        }

        private void OnDestroy()
        {

            if (playerHealth != null)
            {
                playerHealth.Died -= OnPlayerDied;
            }
        }

        private void OnPlayerDied()
        {
            //플레이어가 사망하면 보스는 모든 패턴을 멈춤
            Debug.Log("[도철 보스] 플레이어 사망 감지 - 모든 행동을 중단합니다.");
            StopAllCoroutines();
            isBusy = true;
            SafeSetTrigger(hashRecovery);
        }

        private void Update()
        {
            if (isDead || (playerHealth != null && playerHealth.IsDead)) return;

            if (dashCooldownTimer > 0) dashCooldownTimer -= Time.deltaTime;

            if (isBusy) return;

            //보스 사망 검사
            if (CheckDeathCondition()) return;

            //페이즈 전환 검사
            if (CheckPhaseTransition()) return;

            //패턴 평가 및 진행
            EvaluateNextAction();
        }

        private void SafeSetTrigger(int triggerHash)
        {
            if (animator != null && animator.enabled)
            {
                animator.SetTrigger(triggerHash);
            }
        }

        //보스 방 입장 및 초기화 순서
        private IEnumerator SequenceBossIntro()
        {
            isBusy = true;
            SetPlayerControl(false);

            if (isFirstChallenge)
            {
                //등장 컷씬 진행
                Debug.Log("[도철 보스] 첫 도전 등장 컷씬 실행 중...");
                SafeSetTrigger(hashCutsceneIntro);
                yield return new WaitForSeconds(3.0f); //컷씬 재생 시간 대기

                //전투 초기화 및 주변 오브젝트 흡입/도발 포효
                SafeSetTrigger(hashRoar);
                yield return new WaitForSeconds(2.0f);
                isFirstChallenge = false;
            }
            else
            {
                //등장 생략 및 즉시 전투 초기화
                Debug.Log("[도철 보스] 재도전 전투 빠른 시작");
                SafeSetTrigger(hashQuickIntro);
                yield return new WaitForSeconds(1.0f);
            }

            SetPlayerControl(true);
            isBusy = false;
            Debug.Log("[도철 보스] 보스 전투 상태 진입 완료!");
        }

        private bool CheckDeathCondition()
        {
            if (currentHealth <= 0)
            {
                Debug.Log("[도철 보스] 체력 0 도달 - 사망 연출 시작");
                isDead = true;
                isBusy = true;
                StopAllCoroutines();
                StartCoroutine(SequenceBossDeath());
                return true;
            }
            return false;
        }

        private bool CheckPhaseTransition()
        {
            //체력이 50% 이하일 경우 2페이지 전환
            if (currentPhase == 1 && (currentHealth / maxHealth) <= 0.5f)
            {
                Debug.Log("[도철 보스] 체력 50% 이하 - 2페이지 전환 시작");
                StartCoroutine(SequencePhase2Transition());
                return true;
            }

            //2페이지이고 체력 20% 이하이며 후반 포효를 아직 사용하지 않은 경우 -> 낙석 기믹 발동
            if (currentPhase == 2 && !hasUsedLateRoar && (currentHealth / maxHealth) <= 0.2f)
            {
                Debug.Log("[도철 보스] 체력 20% 이하 - 후반 낙석 퍼즐 시작");
                hasUsedLateRoar = true;
                StartCoroutine(SequenceRockfallPattern());
                return true;
            }
            return false;
        }

        private void EvaluateNextAction()
        {
            if (playerTransform == null) return;

            LookAtPlayer();

            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

            //공격 거리가 확보되었거나 다가온 경우 패턴 선택
            if (distanceToPlayer <= attackRange)
            {
                Debug.Log($"[도철 보스] 사거리 진입 (현재 거리: {distanceToPlayer:F2} / 공격 사거리: {attackRange}) -> 패턴 실행");
                SelectAndExecuteAttack(distanceToPlayer);
            }
            else
            {
                //플레이어에게 이동
                MoveTowardsPlayer();
            }
        }

        private void LookAtPlayer()
        {
            if (playerTransform.position.x > transform.position.x && !isFacingRight)
            {
                Flip();
            }
            else if (playerTransform.position.x < transform.position.x && isFacingRight)
            {
                Flip();
            }
        }

        private void Flip()
        {
            isFacingRight = !isFacingRight;
            Vector3 scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
        }

        private void MoveTowardsPlayer()
        {
            Vector2 targetPosition = new Vector2(playerTransform.position.x, transform.position.y);
            transform.position = Vector2.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        }

        private void SelectAndExecuteAttack(float distance)
        {
            //후보군 확률 계산을 위한 가중치 배열
            List<string> candidateList = new List<string>();

            //들이받기
            if ((currentHealth / maxHealth) <= 0.8f) candidateList.Add("Tackle");

            //앞발 휘두르기
            candidateList.Add("FrontPawSwipe");

            //한쪽 찍기
            candidateList.Add("OneSideSlam");

            //땅굴 파기
            if (dashCooldownTimer <= 0) candidateList.Add("Burrow");

            //2페이지 전용 패턴 (좌우 연타)
            if (currentPhase == 2) candidateList.Add("FiveCombo");

            if (candidateList.Count == 0) return;

            //무작위 패턴
            string selectedAttack = candidateList[Random.Range(0, candidateList.Count)];
            Debug.Log($"[도철 보스] 실행할 패턴 선택됨: {selectedAttack}");
            ExecuteSelectedPattern(selectedAttack);
        }

        private void ExecuteSelectedPattern(string patternName)
        {
            isBusy = true;
            switch (patternName)
            {
                case "Tackle":
                    StartCoroutine(PatternTackle());
                    break;
                case "FrontPawSwipe":
                    StartCoroutine(PatternFrontPawSwipe());
                    break;
                case "OneSideSlam":
                    StartCoroutine(PatternOneSideSlam());
                    break;
                case "Burrow":
                    dashCooldownTimer = dashCooldown; //쿨타임 리셋
                    StartCoroutine(SequenceBurrowFlow());
                    break;
                case "FiveCombo":
                    StartCoroutine(PatternFiveCombo());
                    break;
                default:
                    isBusy = false;
                    break;
            }
        }

        //들이받기 (예고 -> 전진 몸통 공격 -> 회복)
        private IEnumerator PatternTackle()
        {
            SafeSetTrigger(hashPrepareTackle);
            yield return new WaitForSeconds(0.6f); //예고 대기

            SafeSetTrigger(hashDoTackle);
            //짧은 전진 이동
            float elapsed = 0f;
            Vector3 dir = isFacingRight ? Vector3.right : Vector3.left;
            while (elapsed < 0.5f)
            {
                transform.Translate(dir * moveSpeed * 2.5f * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            SafeSetTrigger(hashRecovery);
            yield return new WaitForSeconds(0.8f);
            isBusy = false;
        }

        //앞발 휘두르기 
        private IEnumerator PatternFrontPawSwipe()
        {
            SafeSetTrigger(hashPrepareSwipe);
            yield return new WaitForSeconds(0.5f);

            SafeSetTrigger(hashDoSwipe);
            yield return new WaitForSeconds(0.6f);

            SafeSetTrigger(hashRecovery);
            yield return new WaitForSeconds(0.5f);
            isBusy = false;
        }

        //한쪽 찍기
        private IEnumerator PatternOneSideSlam()
        {
            SafeSetTrigger(hashPrepareSlam);
            yield return new WaitForSeconds(0.7f);

            SafeSetTrigger(hashDoSlam);
            yield return new WaitForSeconds(0.8f);

            SafeSetTrigger(hashRecovery);
            yield return new WaitForSeconds(0.5f);
            isBusy = false;
        }

        //2페이지 전용 연타 패턴
        private IEnumerator PatternFiveCombo()
        {
            SafeSetTrigger(hashPrepareFiveCombo);
            yield return new WaitForSeconds(0.8f);

            for (int i = 0; i < 4; i++)
            {
                SafeSetTrigger(hashComboStrike);
                yield return new WaitForSeconds(0.3f);
            }

            SafeSetTrigger(hashFinalStrike);
            yield return new WaitForSeconds(1.0f);

            SafeSetTrigger(hashRecovery);
            yield return new WaitForSeconds(1.2f);
            isBusy = false;
        }

        // 땅굴 시스템
        private IEnumerator SequenceBurrowFlow()
        {
            SafeSetTrigger(hashBurrowDown); //땅속으로 들어감
            yield return new WaitForSeconds(1.0f);

            if (currentPhase == 1)
            {
                //1페이지는 한쪽 찍기 실행 후 솟구침
                yield return StartCoroutine(ExecuteBurrowAttack("OneSideSlam"));
            }
            else
            {
                //2페이지: 땅굴 확률 계산 알고리즘 실행
                string chosenBurrowAttack = CalculateBurrowProbability();
                yield return StartCoroutine(ExecuteBurrowAttack(chosenBurrowAttack));

                //연속 땅굴 파기 여부 판별
                if (Random.value <= 0.6f)
                {
                    yield return StartCoroutine(SequenceBurrowFlow());
                    yield break;
                }
            }

            SafeSetTrigger(hashUnburrow); //땅 위로 올라옴
            yield return new WaitForSeconds(0.8f);
            isBusy = false;
        }

        private string CalculateBurrowProbability()
        {
            //기본 확률 정의
            float probSlam = 0.20f;
            float probFiveCombo = 0.35f;
            float probLongLeap = 0.45f;

            //직전 선택이 '한쪽 찍기'였던 경우 확률 보정
            if (lastBurrowAttack == "OneSideSlam")
            {
                probSlam = 0.60f;
                probFiveCombo = 0.20f;
                probLongLeap = 0.20f;
            }

            //2회 연속 사용한 경우 해당 패턴 제외 및 비율 100% 재합산 처리
            if (consecutiveCount >= 2)
            {
                if (lastBurrowAttack == "OneSideSlam") probSlam = 0;
                else if (lastBurrowAttack == "FiveCombo") probFiveCombo = 0;
                else if (lastBurrowAttack == "LongLeap") probLongLeap = 0;

                float sum = probSlam + probFiveCombo + probLongLeap;
                if (sum > 0)
                {
                    probSlam /= sum;
                    probFiveCombo /= sum;
                    probLongLeap /= sum;
                }
            }

            //확률 추첨
            float rand = Random.value;
            string selected = "LongLeap";
            if (rand < probSlam) selected = "OneSideSlam";
            else if (rand < probSlam + probFiveCombo) selected = "FiveCombo";

            //연속 선택 횟수 기록 갱신
            if (selected == lastBurrowAttack) consecutiveCount++;
            else
            {
                lastBurrowAttack = selected;
                consecutiveCount = 1;
            }

            return selected;
        }

        private IEnumerator ExecuteBurrowAttack(string attackName)
        {
            //땅 밑 위치 이동 후 기습 공격
            if (playerTransform != null)
            {
                transform.position = new Vector3(playerTransform.position.x, transform.position.y, transform.position.z);
            }

            if (attackName == "OneSideSlam") yield return StartCoroutine(PatternOneSideSlam());
            else if (attackName == "FiveCombo") yield return StartCoroutine(PatternFiveCombo());
            else yield return StartCoroutine(PatternLongLeap());
        }

        private IEnumerator PatternLongLeap()
        {
            SafeSetTrigger(hashLongLeapStart);
            yield return new WaitForSeconds(0.5f);

            //공중 도약 및 착지 충격파 연출
            SafeSetTrigger(hashLongLeapLand);
            yield return new WaitForSeconds(1.0f);
        }

        private IEnumerator SequencePhase2Transition()
        {
            isBusy = true;
            currentPhase = 2;
            SafeSetTrigger(hashPhase2Roar); //페이지 전환 포효

            //플레이어 피해 차단 및 연출 대기
            yield return new WaitForSeconds(2.0f);

            //2페이지 전환 낙석 실행
            yield return StartCoroutine(SequenceRockfallPattern());

            isBusy = false;
        }

        private IEnumerator SequenceRockfallPattern()
        {
            SafeSetTrigger(hashRockfallRoar);
            yield return new WaitForSeconds(0.4f); //첫 예고 시각 0.4초

            int totalRocks = 7; //총 돌 개수 7개
            int consecutiveLargeRocks = 0; //대형 연속 제한 확인용

            for (int i = 0; i < totalRocks; i++)
            {
                if (rockSpawnAreaMin != null && rockSpawnAreaMax != null)
                {
                    //랜덤 위치 지정
                    float randomX = Random.Range(rockSpawnAreaMin.position.x, rockSpawnAreaMax.position.x);
                    Vector3 spawnPos = new Vector3(randomX, rockSpawnAreaMin.position.y, 0);

                    //돌 크기 결정 (0: 소, 1: 중, 2: 대 / 대형 3연속 금지)
                    int sizeType = Random.Range(0, 3);
                    if (sizeType == 2)
                    {
                        consecutiveLargeRocks++;
                        if (consecutiveLargeRocks >= 3)
                        {
                            sizeType = Random.Range(0, 2); //소/중으로 강제 전환
                            consecutiveLargeRocks = 0;
                        }
                    }
                    else consecutiveLargeRocks = 0;

                    //경고 표시 생성 후 0.5초 뒤 낙석 프리팹 생성
                    if (warningMarkerPrefab) Instantiate(warningMarkerPrefab, new Vector3(randomX, playerTransform.position.y, 0), Quaternion.identity);
                    yield return new WaitForSeconds(0.5f);

                    if (rockPrefab) Instantiate(rockPrefab, spawnPos, Quaternion.identity);
                }

                yield return new WaitForSeconds(0.25f); //낙하 간격
            }

            yield return new WaitForSeconds(1.0f); //낙석 패턴 완료 후 정돈 대기
        }

        private IEnumerator SequenceBossDeath()
        {
            SafeSetTrigger(hashDie);
            yield return new WaitForSeconds(3.0f);
            SetPlayerControl(true);
            gameObject.SetActive(false); //보스 비활성화 및 전투 종료
        }

        private void SetPlayerControl(bool enable)
        {
            //프로젝트에 구현된 플레이어 입력 스크립트 잠금/해제 호출
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                MonoBehaviour inputScript = player.GetComponent("PlayerInput") as MonoBehaviour;
                if (inputScript != null) inputScript.enabled = enable;
            }
        }
    }
}