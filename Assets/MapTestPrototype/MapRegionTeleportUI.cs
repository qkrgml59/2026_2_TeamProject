using System;
using System.Collections.Generic;
using FourGuardians.CourseContent.Combat;
using FourGuardians.CourseContent.Movement;
using Hwanjo.MapTools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Hwanjo.MapPlay
{
    /// <summary>MapTest 빌드용 구역 이동 창. 씬/진행도를 변경하지 않고 실제 지면을 검사합니다.</summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class MapRegionTeleportUI : MonoBehaviour
    {
        private const string FieldName = "Hwanjo.RegionTeleport";
        private const string Help = "1~17 / 3-1 / 11-1 / 14-1";
        private readonly List<Behaviour> locked = new List<Behaviour>();
        private readonly List<bool> previousEnabled = new List<bool>();
        private readonly Collider2D[] overlaps = new Collider2D[64];
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
        private BasicPlayerMovement2D movement;
        private Rigidbody2D body;
        private CapsuleCollider2D capsule;
        private Health2D health;
        private ExcelTilemapSource source;
        private MapCinemachineDirector director;
        private MapGimmickPresentation presentation;
        private MapCinemachineShot[] shots;
        private Font font;
        private Vector2 originalSpawn, savedVelocity;
        private float savedAngular, savedTimeScale, savedFixedStep;
        private bool savedAudioPause, focusRequested, hasLocks, didTeleport;
        private RigidbodyConstraints2D savedConstraints;
        private GameState savedState;
        private GameObject savedSelection;
        private int releaseFrame = -1;
        private float messageUntil;
        private GUIStyle labelStyle, textStyle;

        public bool IsOpen { get; private set; }
        public bool IsMapInputActive { get; private set; }
        public string InputText { get; set; } = "";
        public string Message { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }

        private static void Install(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MapTest") return;
            foreach (var root in scene.GetRootGameObjects())
            {
                var player = root.GetComponentInChildren<BasicPlayerMovement2D>();
                if (player != null && player.enabled && player.GetComponent<MapRegionTeleportUI>() == null)
                    player.gameObject.AddComponent<MapRegionTeleportUI>();
            }
        }

        private void Awake()
        {
            movement = GetComponent<BasicPlayerMovement2D>();
            body = GetComponent<Rigidbody2D>();
            capsule = GetComponent<CapsuleCollider2D>();
            health = GetComponent<Health2D>();
            originalSpawn = transform.position;
            foreach (var item in FindObjectsByType<ExcelTilemapSource>(FindObjectsSortMode.None))
                if (item.gameObject.scene == gameObject.scene) source = item;
            foreach (var item in FindObjectsByType<MapCinemachineDirector>(FindObjectsSortMode.None))
                if (item.player == transform) director = item;
            foreach (var item in FindObjectsByType<MapGimmickPresentation>(FindObjectsSortMode.None))
                if (item.player == movement) presentation = item;
            shots = FindObjectsByType<MapCinemachineShot>(FindObjectsSortMode.None);
            if (presentation != null) font = presentation.uiFont;
        }

        private void Update()
        {
            // 닫는 키가 같은 프레임에 게임/일시정지 메뉴로 전달되지 않게 한 프레임 뒤 복구.
            if (releaseFrame >= 0)
            {
                if (Time.frameCount >= releaseFrame) ReleaseLocks();
                return;
            }
            if (IsMapInputActive)
            {
                if (GameManager.HasInstance && GameManager.Instance.CurrentState != GameState.Paused) ReleaseMapInput();
                return;
            }
            if (IsOpen && GameManager.HasInstance && GameManager.Instance.CurrentState != GameState.Paused)
            {
                Cancel();
                return;
            }
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.tabKey.wasPressedThisFrame)
            {
                if (IsOpen) Cancel(); else Open();
            }
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame) Cancel();
            else if (IsOpen && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                Submit(InputText);
        }

        public bool Open()
        {
            if (IsOpen || hasLocks) return false;
            if (movement == null || !movement.isActiveAndEnabled || body == null || capsule == null || source == null
                || health != null && health.IsDead || movement.Action == WarriorAction.Death
                || presentation != null && presentation.IsPresenting
                || GameManager.HasInstance && GameManager.Instance.CurrentState != GameState.Playing
                || SceneLoadManager.HasInstance && SceneLoadManager.Instance.IsLoading || Time.timeScale <= 0f)
            {
                ShowMessage("플레이 중에만 사용할 수 있습니다.");
                return false;
            }
            savedVelocity = body.linearVelocity;
            savedAngular = body.angularVelocity;
            savedConstraints = body.constraints;
            savedTimeScale = Time.timeScale;
            savedFixedStep = Time.fixedDeltaTime;
            savedAudioPause = AudioListener.pause;
            savedState = GameManager.HasInstance ? GameManager.Instance.CurrentState : GameState.Playing;
            savedSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            hasLocks = true;
            didTeleport = false;
            Lock(movement);
            Lock(GetComponent<Player.Move.PlayerMoveMent>());
            foreach (var item in GetComponentsInChildren<PlayerElementHandler>(true)) Lock(item);
            foreach (var item in GetComponentsInChildren<PlayerCombat2D>(true)) Lock(item);
            foreach (var item in GetComponentsInChildren<PlayerInput>(true)) Lock(item);
            foreach (var item in FindObjectsByType<PauseMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Lock(item);
            foreach (var item in FindObjectsByType<BaseInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Lock(item);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            if (GameManager.HasInstance) GameManager.Instance.ChangeState(GameState.Paused);
            else Time.timeScale = 0f;
            IsOpen = focusRequested = true;
            InputText = "";
            Message = "";
            return true;
        }

        /// <summary> 지도는 같은 입력 잠금을 재사용하며 순간이동 입력창은 표시하지 않음 </summary>
        public bool TryAcquireMapInput()
        {
            if (!Open()) return false;
            IsOpen = false;
            IsMapInputActive = true;
            messageUntil = 0f;
            return true;
        }

        public void ReleaseMapInput()
        {
            if (!IsMapInputActive) return;
            IsMapInputActive = false;
            Close();
        }

        private void Lock(Behaviour item)
        {
            if (item == null || locked.Contains(item)) return;
            locked.Add(item);
            previousEnabled.Add(item.enabled);
            item.enabled = false;
        }

        public void Cancel()
        {
            if (!IsOpen) return;
            Close();
        }

        public bool Submit(string text)
        {
            if (!IsOpen) return false;
            InputText = text ?? "";
            string id = InputText.Trim();
            if (string.Equals(id, "char1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "char2", StringComparison.OrdinalIgnoreCase))
            {
                var variant = GetComponent<MapCharacterVariant>();
                if (variant == null || !variant.TrySetCharacter(string.Equals(id, "char2", StringComparison.OrdinalIgnoreCase)))
                {
                    ShowMessage("캐릭터 표시 자산을 찾지 못했습니다.");
                    return false;
                }
                Close();
                return true;
            }
            if (id.Length == 0) { ShowMessage("구역 번호 또는 char1·char2를 입력하세요."); return false; }
            if (!TryFindDestination(id, out Vector2 destination, out string error))
            {
                ShowMessage(error);
                focusRequested = true;
                return false;
            }
            movement.ResetAfterTeleport(destination);
            Physics2D.SyncTransforms();
            if (director != null) director.RequestSnap();
            didTeleport = true;
            Close();
            return true;
        }

        private void Close()
        {
            IsOpen = false;
            releaseFrame = Time.frameCount + 1;
            GUIUtility.keyboardControl = 0;
        }

        private void ReleaseLocks()
        {
            releaseFrame = -1;
            if (!hasLocks) return;
            hasLocks = false;
            IsMapInputActive = false;
            bool alive = health == null || !health.IsDead;
            if (body != null)
            {
                body.constraints = savedConstraints;
                body.linearVelocity = didTeleport || !alive ? Vector2.zero : savedVelocity;
                body.angularVelocity = didTeleport || !alive ? 0f : savedAngular;
            }
            for (int i = 0; i < locked.Count; i++)
                if (locked[i] != null) locked[i].enabled = previousEnabled[i];
            locked.Clear();
            previousEnabled.Clear();
            // 다른 시스템이 로딩/사망으로 전환한 상태를 되돌리지 않습니다.
            if (!GameManager.HasInstance || GameManager.Instance.CurrentState == GameState.Paused)
            {
                if (GameManager.HasInstance) GameManager.Instance.ChangeState(savedState);
                Time.timeScale = savedTimeScale;
                Time.fixedDeltaTime = savedFixedStep;
                AudioListener.pause = savedAudioPause;
            }
            if (EventSystem.current != null && savedSelection != null && savedSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(savedSelection);
        }

        private void OnDisable()
        {
            IsOpen = false;
            ReleaseLocks();
        }

        private void ShowMessage(string text)
        {
            Message = text;
            messageUntil = Time.unscaledTime + 3f;
        }

        private void OnGUI()
        {
            if (!IsOpen && Time.unscaledTime >= messageUntil) return;
            labelStyle ??= new GUIStyle(GUI.skin.label) { font = font, fontSize = 15, wordWrap = true };
            textStyle ??= new GUIStyle(GUI.skin.textField) { font = font, fontSize = 20 };
            int oldDepth = GUI.depth;
            GUI.depth = -10000;
            float width = Mathf.Min(310f, Screen.width - 24f);
            GUI.Box(new Rect(12, 12, width, IsOpen ? 168 : 56), GUIContent.none);
            if (IsOpen)
            {
                GUI.Label(new Rect(24, 20, width - 24, 24), "구역 이동  ·  " + Help, labelStyle);
                GUI.SetNextControlName(FieldName);
                InputText = GUI.TextField(new Rect(24, 49, width - 24, 30), InputText, 12, textStyle);
                if (focusRequested && Event.current.type == EventType.Repaint)
                {
                    GUI.FocusControl(FieldName);
                    focusRequested = false;
                }
                GUI.Label(new Rect(24, 84, width - 24, 24), "Enter 적용  /  Tab·Esc 취소", labelStyle);
                GUI.Label(new Rect(24, 108, width - 24, 24), "char2 새 캐릭터 / char1 기존", labelStyle);
                if (Time.unscaledTime < messageUntil)
                    GUI.Label(new Rect(24, 132, width - 24, 34), Message, labelStyle);
            }
            else GUI.Label(new Rect(24, 22, width - 24, 38), Message, labelStyle);
            GUI.depth = oldDepth;
        }

        /// <summary>현재 타일맵의 영구 지면에서만 후보를 고르고 실제 충돌/위험/트리거를 검사.</summary>
        public bool TryFindDestination(string id, out Vector2 destination, out string error)
        {
            destination = default;
            error = "없는 구역입니다. " + Help;
            if (source == null || capsule == null || body == null) return false;
            ExcelTilemapSource.RegionLayers region = null;
            foreach (var item in source.regions)
                if (item.id == (id == "11-1" ? "11" : id)) { region = item; break; }
            if (region == null || region.solid == null || !region.solid.gameObject.activeInHierarchy) return false;

            Tilemap map = region.solid;
            BoundsInt bounds = map.cellBounds;
            Vector2 preferred = map.CellToWorld(Vector3Int.FloorToInt(bounds.center));
            Rect? allowed = null;
            if (id == "1") preferred = originalSpawn;
            if (id == "3-1" && region.root != null)
                foreach (var item in region.root.GetComponentsInChildren<Transform>())
                    if (item.name == "Safe retry position") preferred = item.position;
            if (id == "14-1" && region.root != null)
                foreach (var item in region.root.GetComponentsInChildren<Transform>())
                    if (item.name.StartsWith("14-1 OptionalRewardAnchor", StringComparison.Ordinal)) preferred = item.position;
            if (id == "11-1" || id == "15")
            {
                string shotName = id == "15" ? "CM_Region15_Puzzle" : "CM_Region11_1_Puzzle";
                foreach (var shot in shots)
                    if (shot != null && shot.gameObject.scene == gameObject.scene && shot.name == shotName)
                    {
                        // 15번은 미로 내부가 아닌 기존 조작대 진입 영역으로만 이동.
                        allowed = shot.activationArea;
                        preferred = shot.activationArea.center;
                        break;
                    }
                if (!allowed.HasValue) { error = "구역의 안전한 진입 영역을 찾지 못했습니다."; return false; }
            }
            Physics2D.SyncTransforms();
            float best = float.PositiveInfinity;
            Vector2 offset = (Vector2)capsule.bounds.center - body.position;
            float bottomOffset = offset.y - capsule.bounds.extents.y;
            foreach (var cell in bounds.allPositionsWithin)
            {
                if (!map.HasTile(cell) || map.HasTile(cell + Vector3Int.up)) continue;
                Vector3 middle = map.GetCellCenterWorld(cell);
                Vector2 point = new Vector2(middle.x, middle.y + source.cellSize * .5f + .025f - bottomOffset);
                if (allowed.HasValue && !allowed.Value.Contains(point)) continue;
                float score = (point - preferred).sqrMagnitude;
                if (score >= best || !IsSafePosition(point, map)) continue;
                best = score;
                destination = point;
            }
            if (float.IsPositiveInfinity(best))
            {
                error = "현재 구역에 안전한 도착 지점이 없습니다.";
                return false;
            }
            error = "";
            return true;
        }

        private bool IsSafePosition(Vector2 position, Tilemap ground)
        {
            Vector2 center = position + (Vector2)capsule.bounds.center - body.position;
            Vector2 size = capsule.bounds.size;
            Vector2 margin = size * .5f + Vector2.one * .25f;
            foreach (var region in source.regions)
            {
                if (region.damage == null) continue;
                Vector3Int first = region.damage.WorldToCell(center - margin);
                Vector3Int last = region.damage.WorldToCell(center + margin);
                for (int y = first.y; y <= last.y; y++)
                    for (int x = first.x; x <= last.x; x++)
                        if (region.damage.HasTile(new Vector3Int(x, y, 0))) return false;
            }
            // 트리거도 제외: 도착만으로 대시 획득/발판 정답/출구가 작동하지 않게 합니다.
            int count = Physics2D.OverlapCapsule(center, size, CapsuleDirection2D.Vertical, 0f,
                new ContactFilter2D { useTriggers = true }, overlaps);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (overlaps[i] != null && overlaps[i].attachedRigidbody != body
                    && !overlaps[i].transform.IsChildOf(transform)) return false;
            float feet = center.y - size.y * .5f;
            float half = size.x * .35f;
            return HasGround(new Vector2(center.x - half, feet + .025f), ground)
                && HasGround(new Vector2(center.x + half, feet + .025f), ground);
        }

        private bool HasGround(Vector2 origin, Tilemap ground)
        {
            int count = Physics2D.Raycast(origin, Vector2.down, new ContactFilter2D { useTriggers = false }, hits, .12f);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null && hits[i].collider.gameObject == ground.gameObject && hits[i].normal.y > .8f)
                    return true;
            return false;
        }
    }
}

