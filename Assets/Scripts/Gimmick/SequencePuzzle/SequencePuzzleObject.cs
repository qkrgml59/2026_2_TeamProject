using UnityEngine;
using System.Collections;
using Puzzle.Sequence;

namespace Puzzle.Sequence
{
    public class SequencePuzzleObject : MonoBehaviour
    {
        [Header("시각 효과")]
        [SerializeField] private Color defaultColor = Color.white;
        [SerializeField] private Color sucessColor = Color.green;
        [SerializeField] private Color failColor = Color.red;

        private SpriteRenderer spriteRenderer;
        private SequencePuzzle ownerPuzzle;          //자신이 속한 독립 퍼즐
        private bool isActivated = false;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                spriteRenderer.color = defaultColor;
            }
        }

        ///<summary>
        ///자신이 속한 SequencePuzzle을 설정합니다.
        ///</summary>
        public void Init(SequencePuzzle puzzle)
        {
            ownerPuzzle = puzzle;
        }

        ///<summary>
        ///플레이어의 공격을 받았을 때 호출되는 함수
        /// </summary>
        public void OnAttacked()
        {
            if (isActivated) return;
            if (ownerPuzzle == null) return;

            //자신이 속한 동립 퍼즐에 공격 사실 전달
            ownerPuzzle.CheckPuzzle(this);
        }

        //정답일 경우
        public void SetSuccessState()
        {
            isActivated = true;
            if(spriteRenderer !=null)
            {
                spriteRenderer.color = sucessColor ;
            }
        }

        //오답일 경우
        public IEnumerator FailEffect()
        {
            if (spriteRenderer == null) yield break;

            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = failColor;
            yield return new WaitForSeconds(0.3f);
            spriteRenderer.color = originalColor;
        }

        //퍼즐 실패 시 초기화
        public void ResetObject()
        {
            isActivated = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = defaultColor;
            }
        }
    }
}
  
    


