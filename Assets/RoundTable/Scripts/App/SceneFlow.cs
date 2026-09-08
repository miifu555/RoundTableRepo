using UnityEngine.SceneManagement;

namespace RoundTable.App
{
    /// <summary>シーンの名前と移動をまとめたもの。</summary>
    public static class SceneFlow
    {
        public const string Title = "01_Title";
        public const string DeckSelect = "02_DeckSelect";
        public const string Battle = "03_Battle";

        public static void GoTitle() => SceneManager.LoadScene(Title);
        public static void GoDeckSelect() => SceneManager.LoadScene(DeckSelect);
        public static void GoBattle() => SceneManager.LoadScene(Battle);
    }
}
