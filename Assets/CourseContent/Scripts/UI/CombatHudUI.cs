//using FourGuardians.CourseContent.Combat;
//using UnityEngine;

//namespace FourGuardians.CourseContent.UI
//{
//    /// <summary>수업 중 피해 결과를 바로 확인할 수 있는 간단한 체력 표시입니다.</summary>
//    public sealed class CombatHudUI : MonoBehaviour
//    {
//        private Health2D playerHealth;
//        private Health2D enemyHealth;
//        private GUIStyle labelStyle;

//        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
//        private static void CreateHud()
//        {
//            if (FindFirstObjectByType<CombatHudUI>() == null)
//            {
//                new GameObject("Combat HUD", typeof(CombatHudUI));
//            }
//        }

//        private void Start()
//        {
//            Health2D[] healthComponents = FindObjectsByType<Health2D>(FindObjectsSortMode.None);

//            foreach (Health2D health in healthComponents)
//            {
//                if (health.Team == CombatTeam.Player)
//                {
//                    playerHealth = health;
//                }
//                else if (health.Team == CombatTeam.Enemy && enemyHealth == null)
//                {
//                    enemyHealth = health;
//                }
//            }
//        }

//        private void OnGUI()
//        {
//            labelStyle ??= new GUIStyle(GUI.skin.box)
//            {
//                alignment = TextAnchor.MiddleLeft,
//                fontSize = 16,
//                normal = { textColor = Color.white }
//            };

//            DrawHealth(new Rect(18f, 258f, 230f, 34f), "PLAYER", playerHealth, new Color(0.2f, 0.75f, 0.95f));
//            DrawHealth(new Rect(Screen.width - 248f, 18f, 230f, 34f), "ENEMY", enemyHealth, new Color(0.9f, 0.25f, 0.3f));
//        }

//        private void DrawHealth(Rect area, string label, Health2D health, Color color)
//        {
//            if (health == null)
//            {
//                return;
//            }

//            float ratio = health.MaxHealth <= 0 ? 0f : (float)health.CurrentHealth / health.MaxHealth;
//            GUI.Box(area, GUIContent.none);
//            Color previousColor = GUI.color;
//            GUI.color = color;
//            GUI.Box(new Rect(area.x + 3f, area.y + 3f, (area.width - 6f) * ratio, area.height - 6f), GUIContent.none);
//            GUI.color = previousColor;
//            GUI.Label(area, $"  {label}  {health.CurrentHealth} / {health.MaxHealth}", labelStyle);
//        }
//    }
//}
