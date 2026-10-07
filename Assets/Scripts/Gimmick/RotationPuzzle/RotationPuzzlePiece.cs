using UnityEngine;

namespace Gimmick.RotationPuzzle
{
    public class RotationPuzzlePiece : MonoBehaviour
    {
        private RotationPuzzle ownerPuzzle;

        public void Init(RotationPuzzle puzzle)
        {
            ownerPuzzle = puzzle;
        }

        //플레이어 공격 시 호출
        public void OnAttacked()
        {
            if (ownerPuzzle != null)
            {
                ownerPuzzle.CheckPuzzle(transform);
            }
        }
    }
}