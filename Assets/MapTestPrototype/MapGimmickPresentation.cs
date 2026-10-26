using System.Collections;
using System.Collections.Generic;
using FourGuardians.CourseContent.Combat;
using FourGuardians.CourseContent.Movement;
using Unity.Cinemachine;
using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary> 공용 문 확인 연출과 대시 획득 안내. 기존 구역 카메라와 가림 설정은 보존 </summary>
    public sealed class MapGimmickPresentation : MonoBehaviour
    {
        public BasicPlayerMovement2D player;
        public MapCinemachineDirector director;
        public CinemachineCamera eventCamera;
        public Font uiFont;
        public MapTestDoor[] doors;
        public float shakeDuration = .14f;
        public float travelDuration = .65f;
        public float doorHoldDuration = .9f;

        public bool IsPresenting { get; private set; }
        public int PresentedCount { get; private set; }
        public float AcquisitionAlpha { get; private set; }

        private readonly Queue<MapTestDoor> queued = new Queue<MapTestDoor>();
        private Coroutine worker, acquisition;
        private CinemachineBrain brain;
        private CinemachineBlenderSettings originalBlends, eventBlends;
        private Camera output;
        private Rigidbody2D playerBody;
        private Health2D health;
        private MapSafety2D safety;

        //연출 동안 잠갔다가 되돌릴 원래 상태
        private RigidbodyConstraints2D savedConstraints;
        private bool savedMovement, savedSafety;
        private Collider2D[] hurtboxColliders;
        private bool[] hurtboxStates;
        private MapTimedSwitchPuzzle[] timedPuzzles;
        private bool[] timedStates;
        private GameState savedState;
        private MapPuzzleOcclusion[] masks;
        private bool[] maskStates;

        private MapInteractionHint[] hints;
        private GUIStyle titleStyle, hintStyle;

        private void Awake()
        {
            output = director.GetComponent<Camera>();
            brain = director.GetComponent<CinemachineBrain>();

            //기존 블렌드 설정은 그대로 두고, 연출 카메라로 들어가는 전환만 앞에 추가한 사본을 사용
            originalBlends = brain.CustomBlends;
            eventBlends = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
            var blends = new List<CinemachineBlenderSettings.CustomBlend>
            {
                //같은 화면 위치에서 연출 카메라로 즉시 이어 받아 짧은 흔들림이 블렌드에 묻히지 않게 함
                new CinemachineBlenderSettings.CustomBlend
                {
                    From = "**ANY CAMERA**", To = eventCamera.name,
                    Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f)
                }
            };
            if (originalBlends != null && originalBlends.CustomBlends != null) blends.AddRange(originalBlends.CustomBlends);
            eventBlends.CustomBlends = blends.ToArray();
            brain.CustomBlends = eventBlends;

            playerBody = player.GetComponent<Rigidbody2D>();
            health = player.GetComponent<Health2D>();
            var hurtboxes = player.GetComponentsInChildren<Hurtbox2D>();
            hurtboxColliders = new Collider2D[hurtboxes.Length];
            hurtboxStates = new bool[hurtboxes.Length];
            for (int i = 0; i < hurtboxes.Length; i++) hurtboxColliders[i] = hurtboxes[i].GetComponent<Collider2D>();
            timedPuzzles = FindObjectsByType<MapTimedSwitchPuzzle>(FindObjectsSortMode.None);
            timedStates = new bool[timedPuzzles.Length];
            safety = player.GetComponent<MapSafety2D>();
            masks = FindObjectsByType<MapPuzzleOcclusion>(FindObjectsSortMode.None);
            maskStates = new bool[masks.Length];
            hints = FindObjectsByType<MapInteractionHint>(FindObjectsSortMode.None);
            eventCamera.Priority = -100;
        }

        private void Start()
        {
            foreach (var door in doors)
                if (door != null) door.Opened += OnDoorOpened;
        }

        private void OnDisable()
        {
            if (doors != null)
                foreach (var door in doors)
                    if (door != null) door.Opened -= OnDoorOpened;
            if (IsPresenting) ReleasePlayer();
            if (eventCamera != null) eventCamera.Priority = -100;
        }

        private void OnDestroy()
        {
            if (brain != null && brain.CustomBlends == eventBlends) brain.CustomBlends = originalBlends;
            if (eventBlends != null) Destroy(eventBlends);
        }

        //여러 문이 동시에 열려도 한 번에 하나씩 순서대로 보여 줌
        private void OnDoorOpened(MapTestDoor door)
        {
            if (queued.Contains(door)) return;

            queued.Enqueue(door);
            if (worker == null) worker = StartCoroutine(ShowDoors());
        }

        private IEnumerator ShowDoors()
        {
            while (queued.Count > 0)
            {
                MapTestDoor door = queued.Dequeue();
                if (door == null || health.IsDead) continue;

                LockPlayer();
                Vector3 origin = output.transform.position;
                float originSize = output.orthographicSize;
                eventCamera.transform.position = origin;
                if (director.ActiveShot != null) eventCamera.Lens = director.ActiveShot.Camera.Lens;
                eventCamera.Lens.OrthographicSize = originSize;
                eventCamera.Priority = 200;

                //1. 현재 화면에서 짧게 흔들림
                float elapsed = 0f;
                while (elapsed < shakeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float weight = 1f - elapsed / Mathf.Max(.01f, shakeDuration);
                    eventCamera.transform.position = origin + new Vector3(Mathf.Sin(elapsed * 83f), Mathf.Sin(elapsed * 107f), 0f) * (.055f * weight);
                    yield return null;
                }

                //2. 열린 문 위치로 이동하며 줌 조정
                Vector3 focus = new Vector3(door.transform.position.x, door.transform.position.y, origin.z);
                elapsed = 0f;
                while (elapsed < travelDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(.01f, travelDuration));
                    eventCamera.transform.position = Vector3.Lerp(origin, focus, t);
                    eventCamera.Lens.OrthographicSize = Mathf.Lerp(originSize, 3.5f, t);
                    yield return null;
                }

                //3. 문을 잠시 보여 준 뒤 플레이어 구도로 복귀
                yield return new WaitForSecondsRealtime(doorHoldDuration);
                eventCamera.Priority = -100;

                //Brain이 다시 플레이어 구도로 돌아오는 동안에도 조작 잠금은 유지
                yield return null;
                float timeout = 2f;
                while (brain.IsBlending && timeout > 0f)
                {
                    timeout -= Time.unscaledDeltaTime;
                    yield return null;
                }
                ReleasePlayer();
                PresentedCount++;
            }
            worker = null;
        }

        //연출 중 이동·위험 지형 판정·피격·시간제 퍼즐 시간을 멈추고, 원래 상태를 기억해 ReleasePlayer에서 되돌림
        private void LockPlayer()
        {
            IsPresenting = true;
            savedMovement = player.enabled;
            savedSafety = safety != null && safety.enabled;
            savedConstraints = playerBody.constraints;
            savedState = GameManager.Instance.CurrentState;
            player.enabled = false;
            if (safety != null) safety.enabled = false;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
            for (int i = 0; i < hurtboxColliders.Length; i++)
            {
                hurtboxStates[i] = hurtboxColliders[i].enabled;
                hurtboxColliders[i].enabled = false;
            }
            for (int i = 0; i < timedPuzzles.Length; i++)
            {
                timedStates[i] = timedPuzzles[i].enabled;
                timedPuzzles[i].enabled = false;
            }
            GameManager.Instance.ChangeState(GameState.Cutscene);

            //원격 문을 보여 줄 때만 퍼즐의 표시층을 숨기고, 복귀 후 원래 상태로 되돌림
            //일반 출입구의 가림 모양·셰이더·활성화 조건은 수정하지 않음
            for (int i = 0; i < masks.Length; i++)
            {
                if (masks[i] == null) continue;
                maskStates[i] = masks[i].enabled;
                masks[i].enabled = false;
            }
        }

        private void ReleasePlayer()
        {
            if (!IsPresenting) return;

            playerBody.constraints = savedConstraints;
            player.enabled = savedMovement;
            player.ResetAfterTeleport(playerBody.position, .15f);
            if (safety != null) safety.enabled = savedSafety;
            for (int i = 0; i < hurtboxColliders.Length; i++)
                if (hurtboxColliders[i] != null) hurtboxColliders[i].enabled = hurtboxStates[i];
            for (int i = 0; i < timedPuzzles.Length; i++)
                if (timedPuzzles[i] != null) timedPuzzles[i].enabled = timedStates[i];
            for (int i = 0; i < masks.Length; i++)
                if (masks[i] != null) masks[i].enabled = maskStates[i];
            if (GameManager.HasInstance && GameManager.Instance.CurrentState == GameState.Cutscene)
                GameManager.Instance.ChangeState(savedState == GameState.Playing ? savedState : GameState.Playing);
            IsPresenting = false;
        }

        public void ShowDashAcquired()
        {
            if (acquisition != null) StopCoroutine(acquisition);
            acquisition = StartCoroutine(Acquisition());
        }

        //안내 패널 페이드 인 → 유지 → 페이드 아웃
        private IEnumerator Acquisition()
        {
            for (float t = 0; t < .3f; t += Time.unscaledDeltaTime)
            {
                AcquisitionAlpha = t / .3f;
                yield return null;
            }
            AcquisitionAlpha = 1f;
            yield return new WaitForSecondsRealtime(1.2f);
            for (float t = 0; t < .4f; t += Time.unscaledDeltaTime)
            {
                AcquisitionAlpha = 1f - t / .4f;
                yield return null;
            }
            AcquisitionAlpha = 0f;
            acquisition = null;
        }

        //대시 획득 패널을 우선 표시하고, 그 외에는 가장 가까운 조작 안내 하나만 표시
        private void OnGUI()
        {
            // 지도 위로 월드 조작 안내·획득 패널이 겹치지 않도록 표시만 숨김.
            if (player != null && player.TryGetComponent<MapOverviewUI>(out var map) && map.IsOpen) return;
            titleStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, font = uiFont, fontSize = 42, fontStyle = FontStyle.Bold };
            hintStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, font = uiFont, fontSize = 20, wordWrap = true };
            Color previous = GUI.color;
            float scale = Mathf.Clamp(Screen.height / 900f, .65f, 1.5f);
            if (AcquisitionAlpha > 0f)
            {
                Rect panel = new Rect(0, Screen.height * .32f, Screen.width, 210f * scale);
                GUI.color = new Color(0, 0, 0, .72f * AcquisitionAlpha);
                GUI.DrawTexture(panel, Texture2D.whiteTexture);
                GUI.color = new Color(1, 1, 1, AcquisitionAlpha);
                titleStyle.fontSize = Mathf.RoundToInt(46f * scale);
                GUI.Label(new Rect(panel.x, panel.y + 25f * scale, panel.width, 80f * scale), "대시 획득함", titleStyle);
                GUI.Label(new Rect(panel.x, panel.y + 110f * scale, panel.width, 55f * scale), "Shift 또는 K", hintStyle);
            }
            else if (!IsPresenting && player != null && hints != null)
            {
                MapInteractionHint nearest = null;
                float distance = float.PositiveInfinity;
                foreach (var hint in hints)
                {
                    if (hint == null || !hint.isActiveAndEnabled) continue;
                    float current = Vector2.Distance(player.transform.position, hint.transform.position);
                    if (current < hint.range && current < distance)
                    {
                        nearest = hint;
                        distance = current;
                    }
                }
                if (nearest != null)
                {
                    Rect panel = new Rect(Screen.width * .15f, Screen.height - 92f * scale, Screen.width * .7f, 64f * scale);
                    GUI.color = new Color(0, 0, 0, .65f);
                    GUI.DrawTexture(panel, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(panel, nearest.message, hintStyle);
                }
            }
            GUI.color = previous;
        }
    }
}
