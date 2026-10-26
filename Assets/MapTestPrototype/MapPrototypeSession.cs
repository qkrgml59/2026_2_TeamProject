using System.Collections;
using FourGuardians.CourseContent.Combat;
using FourGuardians.CourseContent.Movement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hwanjo.MapPlay
{
    /// <summary> 직접 실행하는 MapTest의 대시 진행도와 기존 맵 입구 사망 복귀를 연결 </summary>
    [DefaultExecutionOrder(-90)]
    [RequireComponent(typeof(BasicPlayerMovement2D), typeof(Health2D))]
    public sealed class MapPrototypeSession : MonoBehaviour
    {
        public const string DashFlag = "Ability/Dash";

        [Tooltip("팀의 slot 파일과 분리된 하위 폴더이며 저장 형식·조건은 기존 SaveManager를 사용합니다.")]
        public string directProfile = "MapTestPrototype";

        [Min(.1f)] public float deathDelay = .8f;

        private BasicPlayerMovement2D movement;
        private Health2D health;
        private bool respawning;

        private void Awake()
        {
            movement = GetComponent<BasicPlayerMovement2D>();
            // MapTest 프로토타입은 아트 버전과 관계없이 슬라이딩을 사용하지 않음.
            movement.SetSlideEnabled(false);
            health = GetComponent<Health2D>();
            if (SaveManager.Instance == null)
            {
                Debug.LogError("MapTest requires the existing Systems bootstrap prefab.");
                enabled = false;
                return;
            }

            //독립 실행의 첫 씬 이벤트가 기존 Single 필터를 지나지 않는 경우를 MapTest에서만 보완
            //이미 제목/로딩/플레이 상태인 흐름은 건드리지 않고, 아직 초기화되지 않은 상태만 시작
            if (GameManager.Instance.CurrentState == GameState.None)
                GameManager.Instance.ChangeState(GameState.Playing);

            SaveManager.Instance.BeginDirectSceneSession(directProfile);
            movement.SetDashUnlocked(SaveManager.Instance.Progress.HasFlag(DashFlag));
            SaveManager.Instance.Progress.OnChanged += OnProgressChanged;
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
            if (SaveManager.HasInstance) SaveManager.Instance.Progress.OnChanged -= OnProgressChanged;
        }

        private void OnProgressChanged(string key)
        {
            if (key == DashFlag) movement.SetDashUnlocked(SaveManager.Instance.Progress.HasFlag(DashFlag));
        }

        private void Update()
        {
            //기존 디버그 사망 입력(L키)도 정상적인 사망 복귀 경로를 사용
            if (!respawning && movement.Action == WarriorAction.Death) OnDied();
        }

        private void OnDied()
        {
            if (respawning) return;

            respawning = true;
            movement.EnterDeathState();
            GameManager.Instance.ChangeState(GameState.GameOver);
            StartCoroutine(Respawn());
        }

        private IEnumerator Respawn()
        {
            //GameOver 상태에서는 timeScale이 낮아지므로 실제 시간으로 대기
            yield return new WaitForSecondsRealtime(deathDelay);

            //진행도는 유지하고, 저장 시스템이 기록한 현재 맵 진입 상태로 돌아감
            if (!SaveManager.Instance.RespawnAtMapEntrance())
                SceneLoadManager.Instance.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
