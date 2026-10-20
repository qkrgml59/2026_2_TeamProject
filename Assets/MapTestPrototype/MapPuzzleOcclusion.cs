using Unity.Cinemachine;
using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary>
    /// 두 퍼즐 구도의 불필요한 주변 공간만 가리는 표시층입니다. 지형·충돌·Excel 데이터는 건드리지 않습니다.
    /// 필요한 방 사각형과 실제 입구를 열어 두고, 입구 바깥 끝에만 짧은 그라데이션을 적용합니다.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(1200)]
    [RequireComponent(typeof(MeshRenderer),typeof(MeshFilter))]
    public sealed class MapPuzzleOcclusion : MonoBehaviour
    {
        public enum EntranceDirection { Left, Top }
        public MapCinemachineShot shot;
        public MapCinemachineDirector director;
        public CinemachineBrain brain;
        public Rect visibleContent;
        public Rect entrance;
        [Tooltip("방 사각형 안에 포함된 외부 빈 공간을 추가로 가립니다. 실제 지형이나 퍼즐 영역에는 사용하지 않습니다.")]
        public Rect exteriorInset;
        public EntranceDirection entranceDirection;
        [Min(0.01f)] public float entranceFeather=.5f;
        public Color wallColor=Color.gray;
        public bool IsVisible { get; private set; }
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock properties;

        private void LateUpdate()
        {
            if(meshRenderer==null)meshRenderer=GetComponent<MeshRenderer>();
            // 전환 도중에도 먼 방이 비치지 않게 가림층을 유지합니다. 순간 귀환 시에는 Brain 상태가 초기화됩니다.
            IsVisible=shot!=null&&brain!=null&&(brain.IsLiveChild(shot.Camera)||director!=null&&director.ActiveShot==shot);
            meshRenderer.enabled=IsVisible;
            if(!IsVisible)return;
            properties??=new MaterialPropertyBlock();
            properties.SetVector("_VisibleRect",new Vector4(visibleContent.xMin,visibleContent.yMin,visibleContent.xMax,visibleContent.yMax));
            properties.SetVector("_EntranceRect",new Vector4(entrance.xMin,entrance.yMin,entrance.xMax,entrance.yMax));
            properties.SetVector("_ExteriorInset",new Vector4(exteriorInset.xMin,exteriorInset.yMin,exteriorInset.xMax,exteriorInset.yMax));
            properties.SetFloat("_Feather",Mathf.Max(.01f,entranceFeather));
            properties.SetFloat("_EntranceTop",entranceDirection==EntranceDirection.Top?1f:0f);
            // SetColor가 프로젝트 색 공간 변환을 처리하므로 여기서 다시 선형 변환하지 않습니다.
            properties.SetColor("_WallColor",wallColor);
            meshRenderer.SetPropertyBlock(properties);
        }
        private void OnDisable(){if(meshRenderer!=null)meshRenderer.enabled=false;IsVisible=false;}

        /// <summary>화면 검증 및 배치 확인용입니다. 0은 완전 공개, 1은 완전 가림을 뜻합니다.</summary>
        public float OpacityAt(Vector2 point)
        {
            if(exteriorInset.width>0f&&exteriorInset.height>0f&&exteriorInset.Contains(point))return 1f;
            if(visibleContent.Contains(point))return 0f;
            if(!entrance.Contains(point))return 1f;
            return entranceDirection==EntranceDirection.Top
                ?Mathf.Clamp01((point.y-(entrance.yMax-entranceFeather))/entranceFeather)
                :1f-Mathf.Clamp01((point.x-entrance.xMin)/entranceFeather);
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.cyan;Gizmos.DrawWireCube(visibleContent.center,visibleContent.size);
            Gizmos.color=Color.yellow;Gizmos.DrawWireCube(entrance.center,entrance.size);
        }
    }
}
