using UnityEngine;

namespace Hwanjo.MapPlay
{
    /// <summary> 퍼즐 근처에서만 보이는 짧은 조작 안내 (화면 표시는 MapGimmickPresentation이 담당) </summary>
    public sealed class MapInteractionHint : MonoBehaviour
    {
        [TextArea] public string message;
        [Min(.25f)] public float range = 2f;
    }
}
