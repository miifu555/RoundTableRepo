using RoundTable.App;
using UnityEngine;
using UnityEngine.UI;

namespace RoundTable.UI
{
    /// <summary>
    /// タイトル画面から開ける「ルール」パネル。ただの静的テキスト表示で、
    /// 通信もゲート(開発者コード)もない。中身は BuildRulesMenu 側の文字列で持つ。
    /// </summary>
    public sealed class RulesScreen : MonoBehaviour
    {
        public Button OpenButton;
        public GameObject Panel;
        public Button CloseButton;

        void Start()
        {
            if (OpenButton != null) OpenButton.onClick.AddListener(Open);
            if (CloseButton != null) CloseButton.onClick.AddListener(Close);
            if (Panel != null) Panel.SetActive(false);
        }

        void Open()
        {
            AudioManager.Instance.PlayButton();
            if (Panel != null) Panel.SetActive(true);
        }

        void Close()
        {
            AudioManager.Instance.PlayButton();
            if (Panel != null) Panel.SetActive(false);
        }
    }
}
