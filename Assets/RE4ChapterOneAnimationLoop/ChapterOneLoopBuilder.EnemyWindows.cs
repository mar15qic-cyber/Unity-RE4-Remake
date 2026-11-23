#if UNITY_EDITOR
namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        public static string RefreshEnemyWindows()
        {
            int count = RefreshClipStates("Ganado", EnemyWindowIds("Ganado"), "Ganado", sourceName: EnemyWindowSourceId);
            count += RefreshClipStates("GanadoMale", EnemyWindowIds("Ganado"), "GanadoMale", sourceName: EnemyWindowSourceId);
            count += RefreshClipStates("Salvador", EnemyWindowIds("Salvador"), "Salvador", sourceName: EnemyWindowSourceId);
            return "敌人原版翻窗与落地片段 " + count + "，场景根轨道 " + ChapterOnePairMotion.Refresh();
        }

        private static string[] EnemyWindowIds(string role) => new[] { role + "Window_1100", role + "Window_1102" };

        private static string EnemyWindowSourceId(string id) => id.Replace("Window_", "_");
    }
}
#endif
