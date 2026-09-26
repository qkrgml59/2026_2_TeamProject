using System;
using FourGuardians.CourseContent.CameraSystem;
using FourGuardians.CourseContent.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FourGuardians.CourseContent.Editor
{
    public static class BasicMovementSceneBuilder
    {
        private const string SpriteSheetPath = "Assets/Warrior free set/Sprite Sheet/Warrior_SheetnoEffect.png";
        private const string EffectSpriteSheetPath = "Assets/Warrior free set/Sprite Sheet/Warrior_Sheet-Effect.png";
        private const string ScenePath = "Assets/CourseContent/Scenes/BasicMovement.unity";
        private const string GeneratedAnimationFolder = "Assets/CourseContent/Animations";
        private const string ControllerPath = "Assets/Warrior free set/Aniamtion/Warrior.controller";
        private const string FarBackgroundPath = "Assets/CourseContent/Art/Backgrounds/PixelNight_Far.png";
        private const string MiddleBackgroundPath = "Assets/CourseContent/Art/Backgrounds/PixelNight_Middle.png";
        private const string NearBackgroundPath = "Assets/CourseContent/Art/Backgrounds/PixelNight_Near.png";
        private static readonly Vector2 WarriorPivot = new Vector2(0.3795939f, 0.0166377f);

        [MenuItem("Tools/Four Guardians/Build Basic Movement Scene")]
        public static void BuildAndOpenFromMenu()
        {
            RebuildGeneratedContent();
        }

        public static void BuildFromCommandLine()
        {
            RebuildGeneratedContent();
            EditorApplication.Exit(0);
        }

        public static void RebuildGeneratedContent()
        {
            if (SceneManager.GetActiveScene().path == ScenePath)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.DeleteAsset(GeneratedAnimationFolder);
            BuildScene();

            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        public static void BuildScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                ConfigureRunScene();
                return;
            }

            EnsureFolder("Assets/CourseContent");
            EnsureFolder("Assets/CourseContent/Scenes");
            ConfigureSpriteSheet();
            ConfigureBackgroundTextures();
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

            if (controller == null)
            {
                throw new InvalidOperationException($"Warrior 원본 Animator Controller를 찾지 못했습니다: {ControllerPath}");
            }

            Scene previousScene = SceneManager.GetActiveScene();
            bool hasSavedPreviousScene = !string.IsNullOrEmpty(previousScene.path);
            NewSceneMode creationMode = hasSavedPreviousScene ? NewSceneMode.Additive : NewSceneMode.Single;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, creationMode);
            SceneManager.SetActiveScene(scene);

            GameObject environment = CreateRoot("01_ENVIRONMENT");
            GameObject background = CreateRoot("00_BACKGROUND");
            GameObject gameplay = CreateRoot("02_GAMEPLAY_OBJECTS");
            GameObject cameraRoot = CreateRoot("03_CAMERA");

            Transform player = CreatePlayer(gameplay.transform, controller);
            Transform cameraTransform = CreateCamera(cameraRoot.transform, player);
            CreateParallaxBackground(background.transform, cameraTransform);
            CreateGround(environment.transform);
            CreatePlatform(environment.transform, "Wall", new Vector2(6f, 0f), new Vector2(1f, 5f));
            CreatePlatform(environment.transform, "LadderTop", new Vector2(2f, 1.25f), new Vector2(3f, 0.5f));
            CreateLadder(environment.transform);
            CreateExtendedMap(environment.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            ConfigureRunScene();

            if (hasSavedPreviousScene)
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Warrior 원본 에셋 기반 BasicMovement 씬을 생성했습니다.");
        }

        private static void ConfigureSpriteSheet()
        {
            ConfigureSpriteSheet(SpriteSheetPath);
            ConfigureSpriteSheet(EffectSpriteSheetPath);
        }

        private static void ConfigureBackgroundTextures()
        {
            ConfigureBackgroundTexture(FarBackgroundPath);
            ConfigureBackgroundTexture(MiddleBackgroundPath);
            ConfigureBackgroundTexture(NearBackgroundPath);
        }

        private static void ConfigureBackgroundTexture(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (importer == null)
            {
                throw new InvalidOperationException($"도트 배경을 찾지 못했습니다: {assetPath}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        private static void ConfigureSpriteSheet(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (importer == null)
            {
                throw new InvalidOperationException($"Warrior 스프라이트를 찾지 못했습니다: {assetPath}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            SpriteRect[] sprites = dataProvider.GetSpriteRects();

            for (int index = 0; index < sprites.Length; index++)
            {
                SpriteRect sprite = sprites[index];
                sprite.alignment = SpriteAlignment.Custom;
                sprite.pivot = WarriorPivot;
                sprites[index] = sprite;
            }

            dataProvider.SetSpriteRects(sprites);
            dataProvider.Apply();
            importer.SaveAndReimport();
        }

        private static Transform CreatePlayer(Transform parent, RuntimeAnimatorController controller)
        {
            Sprite[] sprites = LoadSprites();
            GameObject player = new GameObject("Player", typeof(Rigidbody2D), typeof(CapsuleCollider2D));
            player.transform.SetParent(parent);
            player.transform.position = new Vector3(-4f, -1.4f, 0f);

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.gravityScale = 3f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D collider = player.GetComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.7f, 1.2f);
            collider.offset = new Vector2(0f, 0.6f);

            BasicPlayerMovement2D movement = player.AddComponent<BasicPlayerMovement2D>();

            GameObject visual = new GameObject("Visual", typeof(SpriteRenderer), typeof(Animator));
            visual.transform.SetParent(player.transform, false);
            // 모든 애니메이션 프레임의 Pivot이 발바닥이므로 Player 원점과 바로 맞춘다.
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * 3f;

            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sprite = sprites[0];
            visual.GetComponent<Animator>().runtimeAnimatorController = controller;
            visual.AddComponent<WarriorAnimatorBridge>().Configure(movement);
            return player.transform;
        }

        private static Sprite[] LoadSprites()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SpriteSheetPath);
            Sprite[] sprites = new Sprite[99];

            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is not Sprite sprite || !sprite.name.StartsWith("Warrior_SheetnoEffect_"))
                {
                    continue;
                }

                string indexText = sprite.name.Substring("Warrior_SheetnoEffect_".Length);

                if (int.TryParse(indexText, out int index) && index >= 0 && index < sprites.Length)
                {
                    sprites[index] = sprite;
                }
            }

            for (int index = 0; index < sprites.Length; index++)
            {
                if (sprites[index] == null)
                {
                    throw new InvalidOperationException($"Warrior 프레임 {index}을 찾지 못했습니다.");
                }
            }

            return sprites;
        }

        private static Sprite[] Slice(Sprite[] source, int start, int count)
        {
            Sprite[] result = new Sprite[count];
            Array.Copy(source, start, result, 0, count);
            return result;
        }

        private static void CreateGround(Transform parent)
        {
            CreatePlatform(parent, "Ground", new Vector2(0f, -2.5f), new Vector2(60f, 1f));
            CreatePlatform(parent, "Step", new Vector2(4f, -1.25f), new Vector2(3f, 0.5f));
        }

        private static void CreateExtendedMap(Transform parent)
        {
            // 플레이어가 좌우로 오래 달리며 카메라와 패럴랙스를 비교할 수 있는 연습 구간이다.
            CreatePlatform(parent, "LeftBoundary", new Vector2(-30f, 1f), new Vector2(1f, 8f));
            CreatePlatform(parent, "RightBoundary", new Vector2(30f, 1f), new Vector2(1f, 8f));

            CreatePlatform(parent, "WestStep_A", new Vector2(-24f, -1.45f), new Vector2(4f, 0.6f));
            CreatePlatform(parent, "WestStep_B", new Vector2(-18f, -0.3f), new Vector2(4f, 0.6f));
            CreatePlatform(parent, "WestBridge", new Vector2(-11f, 0.6f), new Vector2(6f, 0.6f));
            CreatePlatform(parent, "CenterUpper", new Vector2(-4f, 1.8f), new Vector2(4f, 0.6f));

            CreatePlatform(parent, "EastStep_A", new Vector2(10f, -1.2f), new Vector2(4f, 0.6f));
            CreatePlatform(parent, "EastStep_B", new Vector2(16f, 0f), new Vector2(4f, 0.6f));
            CreatePlatform(parent, "EastBridge", new Vector2(23f, 1.1f), new Vector2(7f, 0.6f));

            CreateMapMarker(parent, "WEST", new Vector2(-27f, -1.65f), new Color(0.35f, 0.65f, 0.72f));
            CreateMapMarker(parent, "EAST", new Vector2(27f, -1.65f), new Color(0.82f, 0.55f, 0.25f));
        }

        private static void CreateMapMarker(Transform parent, string name, Vector2 position, Color color)
        {
            GameObject marker = new GameObject($"MapMarker_{name}", typeof(SpriteRenderer));
            marker.transform.SetParent(parent);
            marker.transform.position = position;

            SpriteRenderer renderer = marker.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            renderer.color = color;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(0.35f, 2f);
            renderer.sortingOrder = 2;
            marker.transform.localScale = Vector3.one;
        }

        private static void CreateLadder(Transform parent)
        {
            GameObject ladder = new GameObject("Ladder", typeof(SpriteRenderer), typeof(BoxCollider2D));
            ladder.transform.SetParent(parent);
            ladder.transform.position = new Vector2(2f, -0.5f);
            SpriteRenderer renderer = ladder.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            renderer.color = new Color(0.7f, 0.45f, 0.2f, 0.7f);
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(0.7f, 3f);
            ladder.transform.localScale = Vector3.one;
            BoxCollider2D trigger = ladder.GetComponent<BoxCollider2D>();
            trigger.size = new Vector2(0.7f, 3f);
            trigger.isTrigger = true;
        }

        private static void CreatePlatform(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject platform = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D));
            platform.transform.SetParent(parent);
            platform.transform.position = position;

            SpriteRenderer renderer = platform.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            renderer.color = new Color(0.16f, 0.28f, 0.34f);
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;

            // Draw Mode를 바꿀 때 Unity가 기존 월드 크기를 보존하려고 Scale을 변경하므로 되돌린다.
            platform.transform.localScale = Vector3.one;

            platform.GetComponent<BoxCollider2D>().size = size;
        }

        private static Transform CreateCamera(Transform parent, Transform target)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.transform.SetParent(parent);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color(0.04f, 0.07f, 0.1f);

            CameraFollow2D cameraFollow = cameraObject.AddComponent<CameraFollow2D>();
            cameraFollow.Configure(
                target,
                new Vector2(0f, 1f),
                0.12f);
            cameraFollow.ConfigurePlatformerSettings(
                0.25f,
                1.2f,
                0.8f,
                1.5f,
                new Vector2(-21f, 0f),
                new Vector2(21f, 4f));

            return cameraObject.transform;
        }

        private static void CreateParallaxBackground(Transform parent, Transform cameraTransform)
        {
            // 숫자가 작을수록 카메라보다 천천히 움직여 더 멀리 있는 것처럼 보인다.
            Transform far = CreateParallaxLayer(parent, "Far_하늘과먼산_0.10", cameraTransform, 0.10f, 0.04f);
            Transform middle = CreateParallaxLayer(parent, "Middle_산맥_0.35", cameraTransform, 0.35f, 0.10f);
            Transform near = CreateParallaxLayer(parent, "Near_나무실루엣_0.65", cameraTransform, 0.65f, 0.18f);

            CreatePixelBackgroundPair(far, "Far", FarBackgroundPath, 1.3f, 2f, -30);
            CreatePixelBackgroundPair(middle, "Middle", MiddleBackgroundPath, 0.95f, 0f, -20);
            CreatePixelBackgroundPair(near, "Near", NearBackgroundPath, 1f, -0.6f, -10);
        }

        private static void CreatePixelBackgroundPair(
            Transform parent,
            string name,
            string spritePath,
            float scale,
            float verticalPosition,
            int sortingOrder)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

            if (sprite == null)
            {
                throw new InvalidOperationException($"패럴랙스 Sprite를 불러오지 못했습니다: {spritePath}");
            }

            float width = sprite.bounds.size.x * scale;

            // 같은 그림을 좌우에 이어 붙여 긴 맵에서도 화면 가장자리가 보이지 않게 한다.
            for (int index = -1; index <= 1; index++)
            {
                GameObject tile = new GameObject($"{name}_Tile_{index + 2:00}", typeof(SpriteRenderer));
                tile.transform.SetParent(parent);
                tile.transform.localPosition = new Vector3(index * width, verticalPosition, 0f);
                tile.transform.localScale = Vector3.one * scale;

                SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = sortingOrder;
            }
        }

        private static Transform CreateParallaxLayer(
            Transform parent,
            string name,
            Transform cameraTransform,
            float horizontal,
            float vertical)
        {
            GameObject layer = new GameObject(name);
            layer.transform.SetParent(parent);
            layer.AddComponent<ParallaxLayer2D>().Configure(cameraTransform, horizontal, vertical);
            return layer.transform;
        }

        private static void CreateMountainRow(
            Transform parent,
            string prefix,
            float baseY,
            float size,
            Color color,
            int sortingOrder)
        {
            for (int index = -4; index <= 4; index++)
            {
                float height = size + Mathf.Abs(index % 3) * 0.7f;
                CreateBackgroundShape(
                    parent,
                    $"{prefix}_{index + 5:00}",
                    new Vector2(index * 4.5f, baseY),
                    new Vector2(5.5f, height),
                    color,
                    sortingOrder);
            }
        }

        private static void CreateTreeRow(Transform parent, float baseY, Color color, int sortingOrder)
        {
            for (int index = -10; index <= 10; index++)
            {
                float height = 1.5f + Mathf.Abs(index % 4) * 0.35f;
                CreateBackgroundShape(
                    parent,
                    $"Tree_{index + 11:00}",
                    new Vector2(index * 1.8f, baseY + height * 0.5f),
                    new Vector2(0.65f, height),
                    color,
                    sortingOrder);
            }
        }

        private static void CreateBackgroundShape(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder)
        {
            GameObject shape = new GameObject(name, typeof(SpriteRenderer));
            shape.transform.SetParent(parent);
            shape.transform.position = position;

            SpriteRenderer renderer = shape.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            renderer.color = color;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.sortingOrder = sortingOrder;
            shape.transform.localScale = Vector3.one;
        }

        private static GameObject CreateRoot(string name)
        {
            return new GameObject(name);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        public static void ConfigureRunScene()
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);

            if (sceneAsset == null)
            {
                return;
            }

            // 다른 씬을 열어둔 상태에서도 Play하면 항상 이 기초 이동 예제가 실행된다.
            EditorSceneManager.playModeStartScene = sceneAsset;
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }
    }
}
