using System.Collections;
using RoundTable.App;
using RoundTable.Data;
using RoundTable.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoundTable.UI
{
    /// <summary>タイトル画面。対戦モードの選択と、通信対戦の設定を行う。</summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [Header("背景")]
        public Image Background;
        public GameObject BackgroundPlaceholder;

        [Header("モード選択")]
        public GameObject ModePanel;
        public Button HotSeatButton;
        public Button VsAiButton;
        public Button OnlineButton;

        [Header("CPUの強さ")]
        [Tooltip("よわい / ふつう / つよい の3つ。順番どおりに割り当てること。")]
        public Button[] DifficultyButtons = new Button[3];
        public Color DifficultySelected = new Color(0.847f, 0.706f, 0.376f, 1f);
        public Color DifficultyNormal = new Color(0.196f, 0.180f, 0.235f, 0.94f);

        [Header("通信設定")]
        public GameObject OnlinePanel;
        public TMP_Text RepoInfoText;
        public TMP_InputField TokenField;
        public TMP_InputField RoomField;
        public Toggle HostToggle;
        public TMP_Text RoleHintText;
        public Button TestButton;
        public Button OnlineStartButton;
        public Button OnlineBackButton;
        public TMP_Text StatusText;

        [Header("旧レイアウト用（シーンを作り直せば不要）")]
        [Tooltip("リポジトリは固定になったので、繋がっていても入力不可にして固定値を出すだけ。")]
        public TMP_InputField OwnerField;
        public TMP_InputField RepoField;
        public TMP_InputField BranchField;

        OnlineConfig _cfg;
        bool _testing;

        void Start()
        {
            GameSession.ResetToDefaults();

            // シーン生成時に焼き込んだ背景ではなく、今の GameDatabase の値を使う。
            // ビルド直前に絵の割り当てを差し替える仕組み (実写⇔公開用) と噛み合わせるため。
            var db = GameDatabase.Instance;
            bool hasBg = db != null && db.TitleBackground != null;
            if (Background != null)
            {
                Background.sprite = hasBg ? db.TitleBackground : null;
                Background.gameObject.SetActive(hasBg);
            }
            if (BackgroundPlaceholder != null) BackgroundPlaceholder.SetActive(!hasBg);

            _cfg = GameSession.Online;
            _cfg.Load();

            if (RepoInfoText != null)
                RepoInfoText.text = OnlineConfig.HasBuiltInToken
                    ? $"対戦に使うリポジトリ: <b>{OnlineConfig.RepoDisplay}</b>（固定 / トークン組み込み済み）"
                    : $"対戦に使うリポジトリ: <b>{OnlineConfig.RepoDisplay}</b>（固定）";

            // 旧レイアウトのシーンだと入力欄が残っているので、固定値を出して触れないようにする
            LockToFixed(OwnerField, _cfg.Owner);
            LockToFixed(RepoField, _cfg.Repo);
            LockToFixed(BranchField, _cfg.Branch);

            if (TokenField != null)
            {
                TokenField.contentType = TMP_InputField.ContentType.Password;
                if (OnlineConfig.HasBuiltInToken)
                {
                    // ビルドにトークンが焼き込まれているので、入力させない
                    TokenField.text = "";
                    TokenField.readOnly = true;
                    TokenField.interactable = false;
                    if (TokenField.placeholder is TMP_Text ph) ph.text = "ビルドに組み込み済み（入力不要）";
                }
                else
                {
                    TokenField.text = _cfg.Token;
                }
            }
            if (RoomField != null) RoomField.text = _cfg.RoomId;
            if (HostToggle != null) HostToggle.isOn = _cfg.IsHost;

            Wire(HotSeatButton, StartHotSeat);
            Wire(VsAiButton, StartVsAi);
            Wire(OnlineButton, () => ShowOnlinePanel(true));

            for (int i = 0; i < DifficultyButtons.Length; i++)
            {
                int level = i;
                Wire(DifficultyButtons[i], () => SetDifficulty((AiDifficulty)level));
            }
            RefreshDifficultyButtons();
            Wire(OnlineBackButton, () => ShowOnlinePanel(false));
            Wire(TestButton, () => { if (!_testing) StartCoroutine(TestConnection()); });
            Wire(OnlineStartButton, StartOnline);

            if (HostToggle != null) HostToggle.onValueChanged.AddListener(_ => UpdateRoleHint());

            ShowOnlinePanel(false);
            UpdateRoleHint();
            SetStatus("");

            AudioManager.Instance.PlayTitleBgm();
        }

        /// <summary>固定値を表示するだけの、触れない入力欄にする。</summary>
        static void LockToFixed(TMP_InputField field, string value)
        {
            if (field == null) return;
            field.text = value;
            field.readOnly = true;
            field.interactable = false;
        }

        static void Wire(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlayButton();
                action();
            });
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

        static string MissingInputMessage()
            => OnlineConfig.HasBuiltInToken
                ? "部屋名を入力してください。"
                : "アクセストークンと部屋名を入力してください。";

        void StartHotSeat()
        {
            GameSession.Mode = MatchMode.HotSeat;
            GameSession.LocalPlayerIndex = 0;
            SceneFlow.GoDeckSelect();
        }

        void StartVsAi()
        {
            GameSession.Mode = MatchMode.VsAi;
            GameSession.LocalPlayerIndex = 0; // 自分は常にプレイヤー1、CPUがプレイヤー2
            SceneFlow.GoDeckSelect();
        }

        void SetDifficulty(AiDifficulty level)
        {
            GameSession.AiLevel = level;
            RefreshDifficultyButtons();
        }

        void RefreshDifficultyButtons()
        {
            for (int i = 0; i < DifficultyButtons.Length; i++)
            {
                var b = DifficultyButtons[i];
                if (b == null) continue;
                bool selected = (int)GameSession.AiLevel == i;
                var img = b.targetGraphic as UnityEngine.UI.Image;
                if (img != null) img.color = selected ? DifficultySelected : DifficultyNormal;

                var label = b.GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = selected ? Color.black : new Color(0.945f, 0.933f, 0.905f, 1f);
            }
        }

        void PullFields()
        {
            if (TokenField != null && !OnlineConfig.HasBuiltInToken) _cfg.Token = TokenField.text.Trim();
            if (RoomField != null) _cfg.RoomId = RoomField.text.Trim();
            if (HostToggle != null) _cfg.IsHost = HostToggle.isOn;
        }

        IEnumerator TestConnection()
        {
            PullFields();
            if (!_cfg.IsComplete)
            {
                SetStatus(MissingInputMessage(), true);
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
                SetStatus(MissingInputMessage(), true);
                return;
            }
            _cfg.Save();

            GameSession.Mode = MatchMode.Online;
            GameSession.LocalPlayerIndex = _cfg.LocalRole;
            SceneFlow.GoDeckSelect();
        }
    }
}
