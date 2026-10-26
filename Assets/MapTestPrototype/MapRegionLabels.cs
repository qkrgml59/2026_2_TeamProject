using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary> 지도에 표시할 이름만 관리. 실제 구역 ID와 진행도는 변경하지 않음 </summary>
    [CreateAssetMenu(menuName = "환조/지도 구역 표시 이름", fileName = "MapRegionLabels")]
    public sealed class MapRegionLabels : ScriptableObject
    {
        [Serializable]
        public sealed class RegionLabel
        {
            [Tooltip("기존 구역 ID. 지형과 연결되는 값이므로 이름 변경 시에는 유지하세요.")]
            public string id;
            public string displayName;
        }

        public List<RegionLabel> regions = new List<RegionLabel>();

        public string GetLabel(string id)
        {
            foreach (var region in regions)
                if (region.id == id) return string.IsNullOrWhiteSpace(region.displayName) ? id : region.displayName;
            return id;
        }
    }
}

