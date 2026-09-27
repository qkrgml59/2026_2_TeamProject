using UnityEngine;

namespace FourGuardians.CourseContent.Combat
{
    public static class ElementHelper
    {
        //attack : 플레이어 공격 속성
        //enemy  : 적의 속성
        public static bool IsWeakness(ElementType attack, ElementType enemy)
        {
            //상호작용 임시힙니당
            //어느 한쪽이라도 무속성이면 상성 없음
            if (attack == ElementType.None || enemy == ElementType.None)
                return false;

            // 물 <-> 불 (상호 약점)
            if ((attack == ElementType.Water && enemy == ElementType.Fire) ||
                (attack == ElementType.Fire && enemy == ElementType.Water))
            {
                return true;
            }

            //전기 <-> 바람 (상호 약점)
            if ((attack == ElementType.Electric && enemy == ElementType.Wind) ||
                (attack == ElementType.Wind && enemy == ElementType.Electric))
            {
                return true;
            }

            return false;
        }
    }
}

