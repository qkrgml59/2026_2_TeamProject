using UnityEditor;
using UnityEngine;

namespace Hwanjo.MapPlay.Editor
{
    /// <summary> 원본 방 경계 참조와 별도 퍼즐 구도를 Scene 핸들로 직접 편집 </summary>
    [CustomEditor(typeof(MapCinemachineShot))]
    public sealed class MapCinemachineShotEditor : UnityEditor.Editor
    {
        private int selectedRect;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var shot = (MapCinemachineShot)target;
            EditorGUILayout.HelpBox("줌은 이 컴포넌트의 Orthographic Size, 부드러운 추적은 Cinemachine Position Composer에서 조정합니다.", MessageType.Info);
            if (shot.sourceZone != null) EditorGUILayout.HelpBox("기존 방 경계를 참조합니다. 아래 Scene 핸들은 원본 MapCameraZone2D의 진입 영역 또는 화면 경계를 수정합니다.", MessageType.Info);
            if (shot.fixedOverview) EditorGUILayout.HelpBox("전체 내용 영역은 카메라 구도입니다. 퍼즐 오브젝트를 배치하거나 지형을 바꾸지는 않습니다.", MessageType.Info);

            //두 번째 탭은 고정 구도면 전체 내용 영역, 원본 방 경계를 참조하면 화면 경계
            selectedRect = GUILayout.Toolbar(selectedRect, shot.fixedOverview ? new[] { "진입 영역", "전체 내용 영역" }
                : shot.sourceZone != null ? new[] { "진입 영역", "화면 경계" }
                : new[] { "진입 영역" });
        }

        private void OnSceneGUI()
        {
            var shot = (MapCinemachineShot)target;
            bool content = shot.fixedOverview && selectedRect == 1;
            bool boundary = shot.sourceZone != null && selectedRect == 1;

            //원본 방 경계를 참조하는 샷은 이 샷이 아니라 MapCameraZone2D의 값을 보여주고 편집
            Rect rect = content ? shot.requiredContent
                : boundary ? shot.sourceZone.WorldView
                : shot.sourceZone != null ? shot.sourceZone.WorldActivation
                : shot.activationArea;
            Handles.color = content || boundary ? Color.cyan : new Color(1f, .7f, .15f);
            Handles.Label(rect.center, content ? "퍼즐 전체 내용 영역" : boundary ? "기존 방 화면 경계" : "카메라 진입 영역");

            float size = HandleUtility.GetHandleSize(rect.center) * .05f;
            EditorGUI.BeginChangeCheck();
            rect.center = Handles.FreeMoveHandle(rect.center, size, Vector3.one * .25f, Handles.RectangleHandleCap);
            float x0 = Handles.Slider(new Vector3(rect.xMin, rect.center.y), Vector3.right, size, Handles.CubeHandleCap, .25f).x;
            float x1 = Handles.Slider(new Vector3(rect.xMax, rect.center.y), Vector3.right, size, Handles.CubeHandleCap, .25f).x;
            float y0 = Handles.Slider(new Vector3(rect.center.x, rect.yMin), Vector3.up, size, Handles.CubeHandleCap, .25f).y;
            float y1 = Handles.Slider(new Vector3(rect.center.x, rect.yMax), Vector3.up, size, Handles.CubeHandleCap, .25f).y;
            if (!EditorGUI.EndChangeCheck()) return;

            //핸들을 반대로 끌어도 최소 0.25 크기를 유지
            rect = Rect.MinMaxRect(x0, y0, Mathf.Max(x0 + .25f, x1), Mathf.Max(y0 + .25f, y1));

            if (shot.sourceZone != null && !content)
            {
                //MapCameraZone2D는 자기 Transform 기준 좌표로 저장하므로 월드 좌표를 로컬 좌표로 되돌려 기록
                Undo.RecordObject(shot.sourceZone, "Edit authored camera boundary");
                rect.position -= (Vector2)shot.sourceZone.transform.position;
                if (boundary)
                    shot.sourceZone.viewBounds = rect;
                else
                    shot.sourceZone.activationArea = rect;
                EditorUtility.SetDirty(shot.sourceZone);
                shot.RefreshSourceZone();
            }
            else
            {
                Undo.RecordObject(shot, "Edit camera area");
                if (content)
                    shot.requiredContent = rect;
                else
                    shot.activationArea = rect;
                EditorUtility.SetDirty(shot);
            }

            //편집 결과를 렌즈와 Confiner 경계에 바로 반영
            shot.Prepare(Camera.main);
        }
    }
}
