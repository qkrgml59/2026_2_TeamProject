using System.Collections.Generic;
using UnityEngine;

namespace Gimmick.RotationPuzzle
{
    public class RotationPuzzle : MonoBehaviour
    {
        [Header("회전 퍼즐 오브젝트 목록")]
        [Tooltip("회전시킬 퍼즐 오브젝트들의 Transform을 직접 드래그해서 등록하세요.")]
        [SerializeField] private List<Transform> puzzleObjects = new List<Transform>();

        [Header("회전 각도 설정")]
        [Tooltip("공격받았을 때 회전할 각도입니다. (기본값: 90)")]
        [SerializeField] private float rotationAngle = 90f;

        [Header("연결된 문")]
        [SerializeField] private Door targetDoor;

        private bool isSolved = false;

        private void Start()
        {
            //각 조각에 RotationPuzzlePiece 컴포넌트가 있다면 부모 참조 등록
            foreach (var obj in puzzleObjects)
            {
                if (obj != null)
                {
                    var piece = obj.GetComponent<RotationPuzzlePiece>();
                    if (piece == null)
                    {
                        piece = obj.gameObject.AddComponent<RotationPuzzlePiece>();
                    }
                    piece.Init(this);
                }
            }
        }

        public void CheckPuzzle(Transform hitObject)
        {
            Debug.Log($"[로그 4] RotationPuzzle.CheckPuzzle 호출됨! 들어온 오브젝트: {hitObject.name}");

            if (isSolved || hitObject == null || !puzzleObjects.Contains(hitObject)) return;

            //목록 검사
            bool contains = puzzleObjects.Contains(hitObject);
            Debug.Log($"[로그 5] Inspector 목록(Puzzle Objects)에 포함되어 있는가? : {contains}");

            if (!contains)
            {
                Debug.LogWarning($"[로그 5 실패] {hitObject.name} 이(가) RotationPuzzle Inspector의 'Puzzle Objects' 리스트에 등록되어 있지 않습니다!");
                return;
            }

            //회전 적용
            Vector3 currentEuler = hitObject.eulerAngles;
            float newZ = Mathf.Round((currentEuler.z + rotationAngle) % 360f);
            hitObject.eulerAngles = new Vector3(currentEuler.x, currentEuler.y, newZ);

            Debug.Log($"[회전 성공!] {hitObject.name} 회전 완료 -> 현재 Z각도: {hitObject.eulerAngles.z}");

            //정답 검사
            CheckAllObjectsSolved();

        }

        private void CheckAllObjectsSolved()
        {
            foreach (var obj in puzzleObjects)
            {
                if (obj == null) return;

                float zAngle = Mathf.Abs(Mathf.Round(obj.eulerAngles.z % 360f));
                if (zAngle > 1f && zAngle < 359f)
                {
                    return;
                }
            }

            SolvePuzzle();
        }

        private void SolvePuzzle()
        {
            isSolved = true;
            Debug.Log($"[{gameObject.name}] 회전 퍼즐 성공! 문을 엽니다.");

            if (targetDoor != null)
            {
                targetDoor.Open();
            }
        }
    }
}