using UnityEditor;

namespace FourGuardians.CourseContent.Editor
{
    [InitializeOnLoad]
    public static class BasicMovementAutoSetup
    {
        // 카메라 추적과 3단 패럴랙스가 추가된 씬 버전입니다.
        // 키가 바뀌면 Unity가 새 버전의 예제 씬을 한 번 다시 생성합니다.
        private const string SessionKey = "FourGuardians.BasicMovement.TeachingCommentsV7";

        static BasicMovementAutoSetup()
        {
            if (UnityEngine.Application.isBatchMode)
            {
                return;
            }

            EditorApplication.update += BuildWhenEditorIsReady;
        }

        private static void BuildWhenEditorIsReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= BuildWhenEditorIsReady;

            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            BasicMovementSceneBuilder.RebuildGeneratedContent();
        }
    }
}
