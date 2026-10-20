using Hwanjo.MapPlay;
using UnityEditor;
using UnityEngine;

namespace Hwanjo.MapPlay.Editor
{
    /// <summary> Transform 기준 사각형을 Scene에서 이동·좌우·상하 핸들로 편집하는 공용 도구 </summary>
    internal static class CameraRectHandles
    {
        public static Rect Draw(Rect value, Transform transform, Color color, string label)
        {
            Vector3 center = (Vector3)value.center + transform.position;
            Handles.color = color;
            Handles.Label(center, label);
            float handle = HandleUtility.GetHandleSize(center) * 0.06f;

            //가운데 핸들로 사각형 전체를 옮긴 뒤, 네 변 핸들로 크기를 조정
            Vector3 moved = Handles.FreeMoveHandle(center, handle, Vector3.zero, Handles.RectangleHandleCap);
            value.position += (Vector2)(moved - center);
            center = moved;
            float left = Handles.Slider(center + Vector3.left * value.width / 2f, Vector3.right, handle, Handles.CubeHandleCap, 0.25f).x;
            float right = Handles.Slider(center + Vector3.right * value.width / 2f, Vector3.right, handle, Handles.CubeHandleCap, 0.25f).x;
            float bottom = Handles.Slider(center + Vector3.down * value.height / 2f, Vector3.up, handle, Handles.CubeHandleCap, 0.25f).y;
            float top = Handles.Slider(center + Vector3.up * value.height / 2f, Vector3.up, handle, Handles.CubeHandleCap, 0.25f).y;

            //월드 좌표를 다시 Transform 기준으로 되돌리고, 반대로 끌어도 최소 0.1 크기를 유지
            return Rect.MinMaxRect(left - transform.position.x, bottom - transform.position.y,
                Mathf.Max(left + 0.1f, right) - transform.position.x, Mathf.Max(bottom + 0.1f, top) - transform.position.y);
        }
    }

    [CustomEditor(typeof(MapCameraZone2D))]
    public sealed class MapCameraZoneEditor : UnityEditor.Editor
    {
        private int handleTarget;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene에서 편집할 영역");
            int selected = GUILayout.Toolbar(handleTarget, new[] { "화면 경계", "활성화 영역" });
            if (selected != handleTarget)
            {
                handleTarget = selected;
                SceneView.RepaintAll();
            }
        }

        private void OnSceneGUI()
        {
            var zone = (MapCameraZone2D)target;
            EditorGUI.BeginChangeCheck();

            //선택한 탭의 사각형에만 핸들을 그림
            Rect view = handleTarget == 0 ? CameraRectHandles.Draw(zone.viewBounds, zone.transform, Color.cyan, "화면 경계") : zone.viewBounds;
            Rect activation = handleTarget == 1 ? CameraRectHandles.Draw(zone.activationArea, zone.transform, new Color(1f, 0.7f, 0.15f), "활성화 영역") : zone.activationArea;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zone, "Edit camera zone");
                zone.viewBounds = view;
                zone.activationArea = activation;
                EditorUtility.SetDirty(zone);
            }
        }
    }

    [CustomEditor(typeof(MapCameraTransition2D))]
    public sealed class MapCameraTransitionEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            var transition = (MapCameraTransition2D)target;
            EditorGUI.BeginChangeCheck();
            Rect area = CameraRectHandles.Draw(transition.area, transition.transform, new Color(0.8f, 0.4f, 1f), "양방향 전환 영역");
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(transition, "Edit camera transition");
                transition.area = area;
                EditorUtility.SetDirty(transition);
            }
        }
    }
}
