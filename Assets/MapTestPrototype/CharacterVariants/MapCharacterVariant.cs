using FourGuardians.CourseContent.Movement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hwanjo.MapPlay
{
    /// <summary> 같은 MapTest 플레이어의 외형만 전환. 선택은 실행 중에만 유지하며 저장하지 않음 </summary>
    [DisallowMultipleComponent]
    public sealed class MapCharacterVariant : MonoBehaviour
    {
        private static bool useCharacter2;
        private Animator animator;
        private Transform visual;
        private RuntimeAnimatorController originalController, character2Controller;
        private Vector3 originalScale;
        private ParticleSystemRenderer dustRenderer;
        private bool originalDustEnabled;

        public bool IsCharacter2 { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            useCharacter2 = false;
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }

        private static void Install(Scene scene, LoadSceneMode mode)
        {
            // 사망으로 MapTest를 다시 로드할 때는 선택 유지. 메뉴로 돌아가면 기본 외형으로 시작.
            if (scene.name == "Mainmenu") { useCharacter2 = false; return; }
            if (scene.name != "MapTest") return;
            foreach (var root in scene.GetRootGameObjects())
            {
                var player = root.GetComponentInChildren<BasicPlayerMovement2D>();
                if (player != null && player.enabled && player.GetComponent<MapCharacterVariant>() == null)
                    player.gameObject.AddComponent<MapCharacterVariant>();
            }
        }

        private void Awake()
        {
            var bridge = GetComponentInChildren<WarriorAnimatorBridge>();
            if (bridge == null) return;
            animator = bridge.GetComponent<Animator>();
            visual = bridge.transform;
            originalScale = visual.localScale;
            originalController = animator.runtimeAnimatorController;
            var dust = transform.Find("Dash_Dust");
            if (dust != null) dustRenderer = dust.GetComponent<ParticleSystemRenderer>();
            if (dustRenderer != null)
            {
                originalDustEnabled = dustRenderer.enabled;
            }
            character2Controller = Resources.Load<RuntimeAnimatorController>("MapTestChar2/Char2");
            if (useCharacter2) TrySetCharacter(true);
        }

        public bool TrySetCharacter(bool character2)
        {
            if (animator == null || visual == null || character2 && character2Controller == null) return false;
            if (IsCharacter2 == character2) return true;
            bool initialized = animator.isInitialized;
            var state = initialized ? animator.GetCurrentAnimatorStateInfo(0) : default;
            float speed = animator.speed;
            animator.runtimeAnimatorController = character2 ? character2Controller : originalController;
            // 기존 33px 몸통과 새 64px 몸통의 월드 높이를 맞춤. 부모 물리/판정 크기는 변경하지 않음.
            visual.localScale = character2 ? originalScale * (33f / 64f) : originalScale;
            // 새 시트에 먼지가 포함됨. 누락 머티리얼의 기존 먼지만 char2에서 숨기고 char1 표시 상태는 복원.
            if (dustRenderer != null)
                dustRenderer.enabled = originalDustEnabled && (!character2 || dustRenderer.sharedMaterial != null);
            animator.speed = speed;
            if (initialized)
            {
                animator.Play(state.fullPathHash, 0, state.normalizedTime);
                animator.Update(0f);
            }
            IsCharacter2 = useCharacter2 = character2;
            return true;
        }
    }
}

