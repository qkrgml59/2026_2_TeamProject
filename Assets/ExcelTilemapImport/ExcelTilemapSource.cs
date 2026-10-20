using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Hwanjo.MapTools
{
    /// <summary>
    /// 통합문서 하나의 가져오기 설정과 안정적인 지역별 Tilemap 참조를 보관
    /// 게임 동작은 없음
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ExcelTilemapSource : MonoBehaviour
    {
        public string workbookPath = "";
        public string sheetName = "Map";
        public string paletteSheetName = "지역별 색상코드";
        public string cellRange = "F15:ST373";
        public string excludedRanges = "JS15:ST114";
        [Min(0.01f)] public float cellSize = 0.25f;
        public Vector2 worldOrigin;

        //명시적으로 동기화한 시점의 공통 셀 상태(기준). 씬과 Undo 기록에 함께 저장됨
        [HideInInspector] public string syncBaseline = "";
        [HideInInspector] public List<RegionLayers> regions = new List<RegionLayers>();

        //이전 3층 구조 씬을 설정 손실 없이 옮기기 위해서만 남겨 둔 필드
        [HideInInspector] public Tilemap oneWayTilemap;
        [HideInInspector] public Tilemap damageTilemap;

        [Serializable]
        public sealed class RegionLayers
        {
            public string id;
            public Transform root;
            public Tilemap solid;
            public Tilemap oneWay;
            public Tilemap damage;
        }
    }
}
