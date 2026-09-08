using UnityEngine;

namespace FourGuardians.CourseContent.UI
{
    // 씬에 UI를 직접 배치하지 않아도 Play할 때 조작 안내를 자동으로 만든다.
    public sealed class ControlsGuideUI : MonoBehaviour
    {
        private const string GuideText =
            "조작 방법\n" +
            "A / D, ← / →   이동\n" +
            "W, ↑, Space    점프 · 벽 점프\n" +
            "W / S, ↑ / ↓   사다리 이동\n" +
            "S, ↓ + 이동     슬라이드\n" +
            "J               공격\n" +
            "Shift / K       대시\n" +
            "대시 중 J       대시 공격\n" +
            "H / L           피격 · 사망 테스트";

        private GUIStyle boxStyle;

        // RuntimeInitializeOnLoadMethod를 사용하면 어느 씬에서 시작해도 안내 UI가 나타난다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateGuide()
        {
            if (FindFirstObjectByType<ControlsGuideUI>() != null)
            {
                return;
            }

            new GameObject("Controls Guide UI", typeof(ControlsGuideUI));
        }

        private void OnGUI()
        {
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 18,
                    padding = new RectOffset(16, 16, 12, 12),
                    normal = { textColor = Color.white }
                };
            }

            // 화면 해상도와 관계없이 왼쪽 위에 고정한다.
            GUI.Box(new Rect(18f, 18f, 310f, 230f), GuideText, boxStyle);
        }
    }
}
