using System.Collections;
using RoundTable.App;
using RoundTable.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoundTable.UI
{
    /// <summary>タイトル画面。対戦モードの選択と、通信対戦の設定を行う。</summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [Header("モード選択")]
        public GameObject ModePanel;
        public Button HotSeatButton;
        public Button OnlineButton;

        [Header("通信設定")]
        public GameObject OnlinePanel;
        public TMP_InputField OwnerField;
        public TMP_InputField RepoField;
        public TMP_InputField BranchField;
        public TMP_InputField TokenField;
        public TMP_InputField RoomField;
        public Toggle HostToggle;
        public TMP_Text RoleHintText;
        public Button TestButton;
        public Button OnlineStartButton;
        public Button OnlineBackButton;
        public TMP_Text StatusText;

        OnlineConfig _cfg;
        bool _testing;

        void Start()
        {
            GameSession.ResetToDefaults();

            _cfg = GameSession.Online;
            _cfg.Load();

            if (OwnerField != null) OwnerField.text = _cfg.Owner;
            if (RepoField != null) RepoField.text = _cfg.Repo;
            if (BranchField != null) BranchField.text = string.IsNullOrEmpty(_cfg.Branch) ? "main" : _cfg.Branch;
            if (TokenField != null)
            {
                TokenField.contentType = TMP_InputField.ContentType.Password;
                TokenField.text = _cfg.Token;
            }
            if (RoomField != null) RoomField.text = _cfg.RoomId;
            if (HostToggle != null) HostToggle.isOn = _cfg.IsHost;

            Wire(HotSeatButton, StartHotSeat);
            Wire(OnlineButton, () => ShowOnlinePanel(true));
            Wire(OnlineBackButton, () => ShowOnlinePanel(false));
            Wire(TestButton, () => { if (!_testing) StartCoroutine(TestConnection()); });
            Wire(OnlineStartButton, StartOnline);

            if (HostToggle != null) HostToggle.onValueChanged.AddListener(_ => UpdateRoleHint());

            ShowOnlinePanel(false);
            UpdateRoleHint();
            SetStatus("");
        }

        static void Wire(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(action);
        }

        void ShowOnlinePanel(bool show)
        {
            if (OnlinePanel != null) OnlinePanel.SetActive(show);
            if (ModePanel != null) ModePanel.SetActive(!show);
            SetStatus("");
        }

        void UpdateRoleHint()
        {
            if (RoleHintText == null) return;
            bool host = HostToggle == null || HostToggle.isOn;
            RoleHintText.text = host
                ? "あなたは<b>ホスト（プレイヤー1）</b>。部屋を作り、シャッフルのシードを決めます。"
                : "あなたは<b>ゲスト（プレイヤー2）</b>。ホストが部屋を作るのを待ちます。";
        }

        void SetStatus(string msg, bool error = false)
        {
            if (StatusText == null) return;
            StatusText.text = msg;
            StatusText.color = error ? new Color(0.855f, 0.353f, 0.290f) : new Color(0.639f, 0.616f, 0.588f);
        }

        void StartHotSeat()
        {
            GameSession.Mode = MatchMode.HotSeat;
            GameSession.LocalPlayerIndex = 0;
            SceneFlow.GoDeckSelect();
        }

        void PullFields()
        {
            if (OwnerField != null) _cfg.Owner = OwnerField.text.Trim();
            if (RepoField != null) _cfg.Repo = RepoField.text.Trim();
            if (BranchField != null) _cfg.Branch = string.IsNullOrWhiteSpace(BranchField.text) ? "main" : BranchField.text.Trim();
            if (TokenField != null) _cfg.Token = TokenField.text.Trim();
            if (RoomField != null) _cfg.RoomId = RoomField.text.Trim();
            if (HostToggle != null) _cfg.IsHost = HostToggle.isOn;
        }

        IEnumerator TestConnection()
        {
            PullFields();
            if (!_cfg.IsComplete)
            {
                SetStatus("所有者 / リポジトリ / トークン / 部屋名 をすべて入力してください。", true);
                yield break;
            }

            _testing = true;
            SetStatus("接続を確認中…");

            var client = new GitHubFileClient(_cfg);
            bool ok = false;
            string msg = "";
            yield return client.TestConnection((o, m) => { ok = o; msg = m; });

            SetStatus(ok ? "接続OK。対戦を開始できます。" : msg, !ok);
            if (ok) _cfg.Save();
            _testing = false;
        }

        void StartOnline()
        {
            PullFields();
            if (!_cfg.IsComplete)
            {
                SetStatus("所有者 / リポジトリ / トークン / 部屋名 をすべて入力してください。", true);
                return;
            }
            _cfg.Save();

            GameSession.Mode = MatchMode.Online;
            GameSession.LocalPlayerIndex = _cfg.LocalRole;
            SceneFlow.GoDeckSelect();
        }
    }
}
