using System;
using System.Collections.Generic;
using FourGuardians.CourseContent.Movement;
using Gimmick.RotationPuzzle;
using Hwanjo.MapTools;
using Puzzle.Sequence;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Hwanjo.MapPlay
{
    /// <summary> MapTest 실제 Tilemap과 기존 진행 상태를 읽어 표시하는 전체 공개 지도 </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class MapOverviewUI : MonoBehaviour
    {
        [SerializeField] private MapRegionLabels regionLabels;

        private sealed class RegionView
        {
            public string id;
            public Rect bounds;
            public RectTransform badge;
            public Text text;
        }

        private enum MarkerKind { Puzzle, Repeat, Door, Dash }

        private sealed class Marker
        {
            public string id, label, glyph;
            public Vector2 position;
            public MarkerKind kind;
            public PermanentClear progress;
            public MapTestDoor door;
            public MapCollapsingPlatforms collapse;
            public RectTransform rect;
            public Image image;
            public RectTransform leader;
        }

        private static readonly Color Pending = new Color(.96f, .36f, .38f);
        private static readonly Color Complete = new Color(.3f, .86f, .58f);
        private static readonly Color Repeat = new Color(.45f, .7f, .94f);
        private static readonly Color Closed = new Color(.96f, .36f, .38f);
        private static readonly Color PlayerColor = new Color(.3f, .93f, 1f);
        private static readonly Color Ink = new Color(.035f, .06f, .085f);

        private readonly List<RegionView> regions = new List<RegionView>();
        private readonly List<Marker> markers = new List<Marker>();
        private ExcelTilemapSource source;
        private MapRegionTeleportUI input;
        private Canvas canvas;
        private Camera uiCamera;
        private RectTransform plot, availablePlot, playerMarker;
        private Text currentText, detailText;
        private Texture2D terrain;
        private Sprite hexagonSprite, circleSprite;
        private Rect worldBounds;
        private Font font;
        private bool cursorVisible;
        private CursorLockMode cursorLock;
        private int screenWidth, screenHeight;

        public bool IsOpen { get; private set; }
        public string CurrentRegionId { get; private set; } = "";

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
                if (player == null || !player.enabled) continue;
                if (player.GetComponent<MapRegionTeleportUI>() == null) player.gameObject.AddComponent<MapRegionTeleportUI>();
                if (player.GetComponent<MapOverviewUI>() == null) player.gameObject.AddComponent<MapOverviewUI>();
            }
        }

        private void Awake()
        {
            input = GetComponent<MapRegionTeleportUI>();
            if (regionLabels == null) regionLabels = Resources.Load<MapRegionLabels>("MapRegionLabels");
            foreach (var item in FindObjectsByType<ExcelTilemapSource>(FindObjectsSortMode.None))
                if (item.gameObject.scene == gameObject.scene) source = item;
            foreach (var item in FindObjectsByType<MapGimmickPresentation>(FindObjectsSortMode.None))
                if (item.player != null && item.player.transform == transform) font = item.uiFont;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (source == null || input == null) { enabled = false; return; }

            CollectRegions();
            CollectMarkers();
            BuildTerrain();
            CreateMarkerSprites();
            CreateCanvas();
            canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (IsOpen && (!input.IsMapInputActive || !input.isActiveAndEnabled))
            {
                Close();
                return;
            }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.mKey.wasPressedThisFrame) { if (IsOpen) Close(); else Open(); }
                else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame) Close();
            }
            if (!IsOpen) return;
            if (screenWidth != Screen.width || screenHeight != Screen.height) Layout();
            RefreshView();
        }

        public bool Open()
        {
            if (IsOpen || canvas == null || !input.isActiveAndEnabled
                || input.IsOpen || input.IsMapInputActive
                || GameManager.HasInstance && GameManager.Instance.CurrentState != GameState.Playing) return false;
            if (!input.TryAcquireMapInput()) return false;
            cursorVisible = Cursor.visible;
            cursorLock = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            IsOpen = true;
            canvas.gameObject.SetActive(true);
            uiCamera.enabled = true;
            Layout();
            RefreshView();
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (uiCamera != null) uiCamera.enabled = false;
            if (input != null) input.ReleaseMapInput();
            Cursor.visible = cursorVisible;
            Cursor.lockState = cursorLock;
        }

        private void OnDisable() => Close();

        private void OnDestroy()
        {
            Close();
            if (terrain != null) Destroy(terrain);
            if (hexagonSprite != null) Destroy(hexagonSprite);
            if (circleSprite != null) Destroy(circleSprite);
        }

        private string Label(string id) => regionLabels != null ? regionLabels.GetLabel(id) : id;

        private void CollectRegions()
        {
            foreach (var region in source.regions)
            {
                if (region.solid == null || region.solid.GetUsedTilesCount() == 0) continue;
                Bounds bounds = region.solid.localBounds;
                Vector3 first = region.solid.transform.TransformPoint(bounds.min);
                Vector3 last = region.solid.transform.TransformPoint(bounds.max);
                regions.Add(new RegionView { id = region.id, bounds = Rect.MinMaxRect(first.x, first.y, last.x, last.y) });
            }
            foreach (var shot in FindObjectsByType<MapCinemachineShot>(FindObjectsSortMode.None))
                if (shot.gameObject.scene == gameObject.scene && shot.name == "CM_Region11_1_Puzzle")
                    regions.Add(new RegionView { id = "11-1", bounds = shot.requiredContent });
        }

        private void BuildTerrain()
        {
            float left = float.PositiveInfinity, bottom = float.PositiveInfinity;
            float right = float.NegativeInfinity, top = float.NegativeInfinity;
            foreach (var region in regions)
            {
                left = Mathf.Min(left, region.bounds.xMin); bottom = Mathf.Min(bottom, region.bounds.yMin);
                right = Mathf.Max(right, region.bounds.xMax); top = Mathf.Max(top, region.bounds.yMax);
            }
            float cell = source.cellSize;
            worldBounds = Rect.MinMaxRect(left - 1f, bottom - 1f, right + 1f, top + 1f);
            int width = Mathf.CeilToInt(worldBounds.width / cell);
            int height = Mathf.CeilToInt(worldBounds.height / cell);
            var pixels = new Color32[width * height];
            Color32 outside = new Color(.04f, .065f, .09f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = outside;

            // 지역 배경과 충돌 타일은 분리. 통로의 실제 모양은 Solid/OneWay/Damage 셀 그대로 표시.
            foreach (var region in regions)
            {
                if (region.id == "11-1") continue;
                int x1 = Mathf.RoundToInt((region.bounds.xMin - worldBounds.xMin) / cell);
                int y1 = Mathf.RoundToInt((region.bounds.yMin - worldBounds.yMin) / cell);
                int x2 = Mathf.RoundToInt((region.bounds.xMax - worldBounds.xMin) / cell);
                int y2 = Mathf.RoundToInt((region.bounds.yMax - worldBounds.yMin) / cell);
                for (int y = Mathf.Max(0, y1); y < Mathf.Min(height, y2); y++)
                    for (int x = Mathf.Max(0, x1); x < Mathf.Min(width, x2); x++)
                        pixels[y * width + x] = new Color(.085f, .14f, .18f);
            }
            foreach (var region in source.regions)
            {
                Paint(region.solid, new Color(.43f, .53f, .6f), pixels, width, height);
                Paint(region.oneWay, new Color(1f, .68f, .25f), pixels, width, height);
                Paint(region.damage, new Color(.97f, .28f, .36f), pixels, width, height);
            }
            terrain = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "MapTest terrain", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            terrain.SetPixels32(pixels);
            terrain.Apply();
        }

        private void Paint(Tilemap map, Color32 color, Color32[] pixels, int width, int height)
        {
            if (map == null || map.GetUsedTilesCount() == 0) return;
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                if (!map.HasTile(cell)) continue;
                Vector3 world = map.GetCellCenterWorld(cell);
                int x = Mathf.FloorToInt((world.x - worldBounds.xMin) / source.cellSize);
                int y = Mathf.FloorToInt((world.y - worldBounds.yMin) / source.cellSize);
                if (x >= 0 && x < width && y >= 0 && y < height) pixels[y * width + x] = color;
            }
        }

        private void CollectMarkers()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var item in root.GetComponentsInChildren<RotationPuzzle>(true))
                    AddPuzzle("rotation", "회전 퍼즐", "회", item, true);
                foreach (var item in root.GetComponentsInChildren<MapSafePlatformPuzzle>(true))
                    AddPuzzle("safe-platform", "정답 발판", "발", item, true);
                foreach (var item in root.GetComponentsInChildren<SequencePuzzle>(true))
                    AddPuzzle("sequence", "공격 순서 퍼즐", "순", item, true);
                foreach (var item in root.GetComponentsInChildren<MapOrderedMazePuzzle>(true))
                    AddPuzzle("maze", "원격 조작 퍼즐", "미", item, true);
                foreach (var item in root.GetComponentsInChildren<MapTimedSwitchPuzzle>(true))
                    AddPuzzle("timed", "시간제 스위치", "시", item, false);
                foreach (var item in root.GetComponentsInChildren<MapSecretReveal>(true))
                    markers.Add(new Marker { id = "secret", label = "숨은 입구", glyph = "숨",
                        position = item.cover != null ? VisualCenter(item.cover.transform) : item.transform.position,
                        kind = MarkerKind.Puzzle, progress = item.progress });
                foreach (var item in root.GetComponentsInChildren<MapCollapsingPlatforms>(true))
                    markers.Add(new Marker { id = "collapse", label = "붕괴 발판", glyph = "붕",
                        position = item.switchVisual != null ? item.switchVisual.bounds.center : item.transform.position,
                        kind = MarkerKind.Repeat, collapse = item });
                foreach (var item in root.GetComponentsInChildren<MapTestDoor>(true))
                    // sceneLoaded에서 수집: 영구 완료 복원으로 문이 움직이기 전의 닫힌 위치를 고정.
                    markers.Add(new Marker { id = item.name, label = item.name, glyph = item.name.Length > 2 ? item.name.Substring(1, 2) : "문",
                        position = item.transform.position, kind = MarkerKind.Door, door = item });
                foreach (var item in root.GetComponentsInChildren<MapDashPickup>(true))
                    markers.Add(new Marker { id = "dash", label = "대시 획득", glyph = "대",
                        position = item.transform.position, kind = MarkerKind.Dash });
            }
        }

        private void AddPuzzle(string id, string title, string glyph, Component item, bool permanent)
        {
            Transform visualRoot = item.transform.parent != null ? item.transform.parent : item.transform;
            markers.Add(new Marker { id = id, label = title, glyph = glyph, position = VisualCenter(visualRoot),
                kind = permanent ? MarkerKind.Puzzle : MarkerKind.Repeat,
                progress = permanent ? item.GetComponent<PermanentClear>() : null });
        }

        private static Vector2 VisualCenter(Transform root)
        {
            var visuals = root.GetComponentsInChildren<SpriteRenderer>(true);
            if (visuals.Length == 0) return root.position;
            Bounds bounds = visuals[0].bounds;
            for (int i = 1; i < visuals.Length; i++) bounds.Encapsulate(visuals[i].bounds);
            return bounds.center;
        }

        private bool Completed(Marker marker)
        {
            if (marker.kind == MarkerKind.Dash)
                return SaveManager.HasInstance && SaveManager.Instance.Progress.HasFlag(MapPrototypeSession.DashFlag);
            return marker.progress != null && SaveManager.HasInstance && marker.progress.IsCleared;
        }

        private string State(Marker marker)
        {
            if (marker.door != null) return marker.door.IsMoving ? "이동 중" : marker.door.IsOpen ? "열림" : "닫힘";
            if (marker.kind == MarkerKind.Dash) return Completed(marker) ? "획득" : "미획득";
            if (marker.collapse != null) return "붕괴 " + marker.collapse.CollapsedCount + " / " + marker.collapse.platforms.Length;
            if (marker.kind == MarkerKind.Repeat) return "반복 기믹";
            return marker.progress != null ? Completed(marker) ? "완료" : "미완료" : "상태 정보 없음";
        }

        private Color MarkerColor(Marker marker)
        {
            if (marker.door != null) return marker.door.IsOpen ? Complete : Closed;
            if (marker.kind == MarkerKind.Dash) return Color.white;
            if (marker.kind == MarkerKind.Repeat) return Repeat;
            return Completed(marker) ? Complete : Pending;
        }

        private static bool IsGimmick(Marker marker) => marker.kind is MarkerKind.Puzzle or MarkerKind.Repeat;

        private static bool MarkerVisible(Marker marker)
        {
            return marker.kind != MarkerKind.Door || marker.door != null && marker.door.IsOpen;
        }

        private void CreateMarkerSprites()
        {
            hexagonSprite = MarkerSprite("Map gimmick hexagon", new[]
            {
                new Vector2(-.5f, 0), new Vector2(-.25f, .5f), new Vector2(.25f, .5f),
                new Vector2(.5f, 0), new Vector2(.25f, -.5f), new Vector2(-.25f, -.5f)
            });
            var circle = new Vector2[32];
            for (int i = 0; i < circle.Length; i++)
            {
                float angle = -Mathf.PI * 2f * i / circle.Length;
                circle[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .5f;
            }
            circleSprite = MarkerSprite("Map dash circle", circle);
        }

        private static Sprite MarkerSprite(string name, Vector2[] vertices)
        {
            var texture = Texture2D.whiteTexture;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), texture.width);
            var triangles = new ushort[(vertices.Length - 2) * 3];
            for (int i = 0; i < vertices.Length - 2; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (ushort)(i + 1);
                triangles[i * 3 + 2] = (ushort)(i + 2);
            }
            // OverrideGeometry는 Rect 내부 픽셀 좌표를 받으므로 정규화한 도형을 변환.
            var pixels = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                pixels[i] = (vertices[i] + Vector2.one * .5f) * texture.width;
            sprite.OverrideGeometry(pixels, triangles);
            sprite.name = name;
            return sprite;
        }

        private void CreateCanvas()
        {
            var holder = new GameObject("Map Overview Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            holder.transform.SetParent(transform, false);
            canvas = holder.GetComponent<Canvas>();
            holder.layer = 5;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            // 게임의 퍼즐 가림 셰이더가 지도 위로 올라오지 않도록 UI 전용 화면으로 표시.
            var cameraObject = new GameObject("Map Overview Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -100f);
            uiCamera = cameraObject.GetComponent<Camera>();
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = Ink;
            uiCamera.cullingMask = 1 << 5;
            uiCamera.depth = 100f;
            uiCamera.orthographic = true;
            uiCamera.nearClipPlane = .1f;
            uiCamera.farClipPlane = 20f;
            uiCamera.allowHDR = false;
            uiCamera.allowMSAA = false;
            uiCamera.enabled = false;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10000;
            var scaler = holder.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;

            var panel = MakeImage("Map Panel", holder.transform, new Color(.035f, .055f, .08f, .985f));
            Stretch(panel.rectTransform, new Vector2(16, 16), new Vector2(-16, -16));
            var title = MakeText("Title", panel.transform, "전체 지도", 26, Color.white);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -16), new Vector2(300, 40));
            currentText = MakeText("Current Region", panel.transform, "", 17, PlayerColor);
            Place(currentText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -18), new Vector2(500, 30));
            currentText.alignment = TextAnchor.MiddleRight;
            var keys = MakeText("Controls", panel.transform, "M / Esc 닫기  ·  전체 공개", 14, new Color(.7f, .78f, .84f));
            Place(keys.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -55), new Vector2(500, 24));
            detailText = MakeText("Marker Detail", panel.transform, "", 14, new Color(.85f, .9f, .94f));
            Place(detailText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -54), new Vector2(650, 25));
            detailText.alignment = TextAnchor.MiddleRight;

            availablePlot = MakeRect("Map Area", panel.transform);
            Stretch(availablePlot, new Vector2(22, 104), new Vector2(-22, -88));
            plot = MakeRect("World Map", availablePlot);
            var raw = new GameObject("Terrain", typeof(RectTransform), typeof(RawImage));
            raw.transform.SetParent(plot, false);
            raw.layer = 5;
            raw.GetComponent<RawImage>().texture = terrain;
            raw.GetComponent<RawImage>().raycastTarget = false;
            Stretch(raw.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            foreach (var region in regions)
            {
                var badge = MakeImage("Region:" + region.id, plot, new Color(.025f, .045f, .065f, .94f));
                region.badge = badge.rectTransform;
                region.text = MakeText("Label", badge.transform, Label(region.id), 15, new Color(.94f, .96f, 1f));
                region.text.alignment = TextAnchor.MiddleCenter;
                region.text.horizontalOverflow = HorizontalWrapMode.Wrap;
                region.text.verticalOverflow = VerticalWrapMode.Truncate;
                Stretch(region.text.rectTransform, new Vector2(4, 1), new Vector2(-4, -1));
            }
            foreach (var marker in markers)
            {
                var leader = MakeImage("Link:" + marker.id, plot, new Color(.8f, .86f, .9f, .55f));
                marker.leader = leader.rectTransform;
                marker.leader.SetSiblingIndex(1);
                marker.leader.anchorMin = marker.leader.anchorMax = Vector2.zero;
                marker.leader.pivot = new Vector2(0, .5f);
                marker.image = MakeImage("Marker:" + marker.id, plot, MarkerColor(marker));
                marker.rect = marker.image.rectTransform;
                if (IsGimmick(marker) || marker.kind == MarkerKind.Dash)
                {
                    marker.image.sprite = IsGimmick(marker) ? hexagonSprite : circleSprite;
                    marker.image.useSpriteMesh = true;
                }
                var outline = marker.image.gameObject.AddComponent<Outline>();
                outline.effectColor = Ink;
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                var glyph = MakeText("Symbol", marker.rect, marker.kind == MarkerKind.Dash ? "" : marker.glyph, marker.kind == MarkerKind.Door ? 11 : 13, Ink);
                glyph.fontStyle = FontStyle.Bold;
                glyph.alignment = TextAnchor.MiddleCenter;
                Stretch(glyph.rectTransform, Vector2.zero, Vector2.zero);
            }
            var player = MakeImage("Player Marker", plot, PlayerColor);
            playerMarker = player.rectTransform;
            playerMarker.localRotation = Quaternion.Euler(0, 0, 45);
            var playerOutline = player.gameObject.AddComponent<Outline>();
            playerOutline.effectColor = Color.white;
            playerOutline.effectDistance = new Vector2(1.3f, -1.3f);

            string legend = "회 회전  ·  발 정답 발판  ·  순 공격 순서  ·  미 원격 조작  ·  시 시간제  ·  붕 붕괴 발판  ·  숨 숨은 입구\n"
                + "기믹 육각형: 빨강 미완료 / 초록 완료 / 파랑 반복형    초록 사각형: 열린 문    흰 원: 대시(획득 여부는 상세 안내)    청록 마름모: 현재 위치\n"
                + "지형: 회청색 벽·바닥  /  주황 일방통행 발판  /  빨강 위험 지형    아이콘에 마우스를 올리면 상태를 볼 수 있습니다.";
            var legendText = MakeText("Legend", panel.transform, legend, 13, new Color(.76f, .83f, .88f));
            legendText.rectTransform.anchorMin = Vector2.zero;
            legendText.rectTransform.anchorMax = Vector2.right;
            legendText.rectTransform.offsetMin = new Vector2(26, 16);
            legendText.rectTransform.offsetMax = new Vector2(-26, 91);
            legendText.alignment = TextAnchor.MiddleLeft;
            legendText.resizeTextForBestFit = false;
        }

        private void Layout()
        {
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            Canvas.ForceUpdateCanvases();
            Vector2 space = availablePlot.rect.size;
            float scale = Mathf.Min(space.x / worldBounds.width, space.y / worldBounds.height);
            plot.anchorMin = plot.anchorMax = new Vector2(.5f, .5f);
            plot.pivot = Vector2.zero;
            plot.sizeDelta = worldBounds.size * scale;
            plot.anchoredPosition = -plot.sizeDelta * .5f;
            foreach (var region in regions)
            {
                region.text.text = Label(region.id);
                float width = Mathf.Min(150f, Mathf.Max(26f, region.bounds.width * scale - 8f));
                float wanted = Mathf.Max(26f, region.text.preferredWidth + 12f);
                region.badge.anchorMin = region.badge.anchorMax = Vector2.zero;
                region.badge.pivot = new Vector2(0, 1);
                region.badge.sizeDelta = new Vector2(Mathf.Min(width, wanted), 24);
                region.badge.anchoredPosition = MapPoint(new Vector2(region.bounds.xMin, region.bounds.yMax)) + new Vector2(4, -4);
                Rect playerArea = new Rect(MapPoint(transform.position) - Vector2.one * 9f, Vector2.one * 18f);
                if (BadgeRect(region.badge).Overlaps(playerArea))
                    region.badge.anchoredPosition = MapPoint(new Vector2(region.bounds.xMax, region.bounds.yMax))
                        + new Vector2(-region.badge.sizeDelta.x - 4f, -4f);
            }
            var occupied = new List<Rect>();
            occupied.Add(new Rect(MapPoint(transform.position) - Vector2.one * 9f, Vector2.one * 18f));
            foreach (var region in regions) occupied.Add(BadgeRect(region.badge));
            foreach (var marker in markers)
            {
                bool visible = MarkerVisible(marker);
                marker.rect.gameObject.SetActive(visible);
                if (!visible)
                {
                    marker.leader.gameObject.SetActive(false);
                    continue;
                }
                marker.rect.anchorMin = marker.rect.anchorMax = Vector2.zero;
                marker.rect.pivot = new Vector2(.5f, .5f);
                marker.rect.sizeDelta = marker.kind == MarkerKind.Door ? new Vector2(22, 19) : IsGimmick(marker) ? new Vector2(24, 21) : new Vector2(22, 22);
                Vector2 anchor = MapPoint(marker.position);
                Vector2 shown = MarkerPosition(anchor, marker.rect.sizeDelta, occupied);
                marker.rect.anchoredPosition = shown;
                occupied.Add(new Rect(shown - marker.rect.sizeDelta * .5f - Vector2.one * 2f,
                    marker.rect.sizeDelta + Vector2.one * 4f));
                Vector2 delta = shown - anchor;
                marker.leader.gameObject.SetActive(delta.sqrMagnitude > 1f);
                marker.leader.anchoredPosition = anchor;
                marker.leader.sizeDelta = new Vector2(delta.magnitude, 1f);
                marker.leader.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
            playerMarker.anchorMin = playerMarker.anchorMax = Vector2.zero;
            playerMarker.pivot = new Vector2(.5f, .5f);
            playerMarker.sizeDelta = new Vector2(11, 11);
        }

        private static Rect BadgeRect(RectTransform badge)
        {
            return new Rect(badge.anchoredPosition - Vector2.up * badge.sizeDelta.y, badge.sizeDelta);
        }

        private Vector2 MarkerPosition(Vector2 anchor, Vector2 size, List<Rect> occupied)
        {
            // 실제 위치는 선의 시작점에 유지하고, 겹치는 아이콘만 짧게 옮겨 읽기 쉽게 표시.
            Vector2[] offsets = { Vector2.zero, new Vector2(0, 28), new Vector2(0, -28),
                new Vector2(28, 0), new Vector2(-28, 0), new Vector2(28, 28), new Vector2(-28, 28),
                new Vector2(28, -28), new Vector2(-28, -28), new Vector2(0, 56), new Vector2(0, -56),
                new Vector2(56, 0), new Vector2(-56, 0) };
            foreach (var offset in offsets)
            {
                Vector2 point = anchor + offset;
                Rect area = new Rect(point - size * .5f - Vector2.one * 2f, size + Vector2.one * 4f);
                if (area.xMin < 0 || area.yMin < 0 || area.xMax > plot.rect.width || area.yMax > plot.rect.height) continue;
                bool overlaps = false;
                foreach (var other in occupied) if (area.Overlaps(other)) { overlaps = true; break; }
                if (!overlaps) return point;
            }
            return anchor;
        }

        private Vector2 MapPoint(Vector2 world)
        {
            return new Vector2((world.x - worldBounds.xMin) / worldBounds.width * plot.rect.width,
                (world.y - worldBounds.yMin) / worldBounds.height * plot.rect.height);
        }

        private void RefreshView()
        {
            // 문 상태가 바뀌면 표시·배치도 갱신. 숨긴 문은 연결선과 hover 대상에서도 제외.
            foreach (var marker in markers)
                if (marker.rect.gameObject.activeSelf != MarkerVisible(marker)) { Layout(); break; }
            Vector2 position = transform.position;
            CurrentRegionId = "";
            foreach (var region in regions)
            {
                if (!region.bounds.Contains(position)) continue;
                CurrentRegionId = region.id;
                if (region.id == "11-1") break;
            }
            string label = CurrentRegionId == "" ? "연결 통로" : Label(CurrentRegionId);
            currentText.text = "현재 위치  ·  " + label;
            playerMarker.anchoredPosition = MapPoint(position);
            detailText.text = "육각형 기믹 · 흰 원 대시 · 열린 문만 표시";
            var mouse = Mouse.current;
            foreach (var marker in markers)
            {
                if (!MarkerVisible(marker)) continue;
                marker.image.color = MarkerColor(marker);
                if (mouse != null && MarkerHovered(marker, mouse.position.ReadValue()))
                {
                    string shape = IsGimmick(marker) ? "육각형 기믹" : marker.kind == MarkerKind.Dash ? "흰 원" : "열린 문";
                    detailText.text = shape + "  ·  " + marker.label + "  ·  " + State(marker);
                }
            }
        }

        private bool MarkerHovered(Marker marker, Vector2 screenPosition)
        {
            if (!marker.rect.gameObject.activeInHierarchy
                || !RectTransformUtility.RectangleContainsScreenPoint(marker.rect, screenPosition, canvas.worldCamera)) return false;
            if (!IsGimmick(marker) && marker.kind != MarkerKind.Dash) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(marker.rect, screenPosition, canvas.worldCamera, out Vector2 point)) return false;
            float x = Mathf.Abs(point.x) / marker.rect.rect.width, y = Mathf.Abs(point.y) / marker.rect.rect.height;
            return IsGimmick(marker) ? x + y * .5f <= .5f : x * x + y * y <= .25f;
        }

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = 5;
            return go.GetComponent<RectTransform>();
        }

        private static Image MakeImage(string name, Transform parent, Color color)
        {
            var rect = MakeRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text MakeText(string name, Transform parent, string value, int size, Color color)
        {
            var rect = MakeRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.color = color;
            text.raycastTarget = false;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}

