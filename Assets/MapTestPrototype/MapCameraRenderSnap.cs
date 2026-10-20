using UnityEngine;
using UnityEngine.Rendering;

namespace Hwanjo.MapPlay
{
    /// <summary>
    /// Cinemachine의 부드러운 추적 상태는 유지하고, 렌더링 순간의 시점만 출력 픽셀 격자에 정렬
    /// 전체 구도가 잘리는 강제 정수 줌은 사용하지 않음. 줌 전환 중의 크기 변화는 그대로 허용
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class MapCameraRenderSnap : MonoBehaviour
    {
        public Vector2 LastSnapPixels { get; private set; }

        private Camera output;
        private bool adjusted;

        private void Awake()
        {
            output = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Begin;
            RenderPipelineManager.endCameraRendering += End;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.endCameraRendering -= End;
            if (adjusted && output != null) output.ResetWorldToCameraMatrix();
            adjusted = false;
        }

        private void Begin(ScriptableRenderContext context, Camera camera)
        {
            if (camera != output || !camera.orthographic || camera.pixelHeight < 1) return;

            //화면 1픽셀에 해당하는 월드 길이 단위로 시점 위치를 반올림
            float unit = 2f * camera.orthographicSize / camera.pixelHeight;
            Vector3 logical = camera.transform.position;
            Vector3 snapped = logical;
            snapped.x = Mathf.Round(logical.x / unit) * unit;
            snapped.y = Mathf.Round(logical.y / unit) * unit;
            LastSnapPixels = (Vector2)(snapped - logical) / unit;

            //Transform은 바꾸지 않고 이번 렌더링의 뷰 행렬만 교체. 카메라 공간은 -Z가 앞쪽이라 Z축을 뒤집음
            camera.worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f))
                * Matrix4x4.TRS(snapped, camera.transform.rotation, Vector3.one).inverse;
            adjusted = true;
        }

        private void End(ScriptableRenderContext context, Camera camera)
        {
            if (camera != output || !adjusted) return;

            //행렬 값을 대입하면 수동 행렬 상태가 남으므로 Transform을 따르는 자동 모드로 복원
            camera.ResetWorldToCameraMatrix();
            adjusted = false;
        }
    }
}
