using System;
using System.Collections;
using System.Collections.Generic;
using RoundTable.App;
using RoundTable.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoundTable.UI
{
    /// <summary>
    /// 開発者用のデバッグメニュー。今のところ「残ったルームを消す」だけ。
    /// 開発者コードのハッシュが焼き込まれていないと、入口ボタンごと出ない。
    /// </summary>
    public sealed class DebugMenuScreen : MonoBehaviour
    {
        [Header("入口")]
        [Tooltip("タイトル画面の隅に置く小さなボタン。コード未設定なら非表示。")]
        public Button OpenButton;

        [Header("パネル")]
        public GameObject Panel;
        public Button CloseButton;

        [Header("コード入力")]
        public GameObject LockedGroup;
        public TMP_InputField CodeField;
        public Button UnlockButton;

        [Header("メニュー本体")]
        public GameObject UnlockedGroup;
        public Button RefreshButton;
        public Button DeleteAllButton;
        public TMP_Text StatusText;

        [Header("ルーム一覧の行")]
        [Tooltip("表示できる行数ぶん、あらかじめ並べておく。足りないぶんは件数だけ知らせる。")]
        public GameObject[] RoomRows = new GameObject[8];
        public TMP_Text[] RoomLabels = new TMP_Text[8];
        public Button[] RoomDeleteButtons = new Button[8];

        readonly List<RoomInfo> _rooms = new List<RoomInfo>();
        RoomAdmin _admin;
        bool _busy;

        void Start()
        {
            if (OpenButton != null)
            {
                OpenButton.gameObject.SetActive(DevAccess.IsConfigured);
                OpenButton.onClick.AddListener(Open);
            }

            if (CloseButton != null) CloseButton.onClick.AddListener(Close);
            if (UnlockButton != null) UnlockButton.onClick.AddListener(TryUnlock);
            if (RefreshButton != null) RefreshButton.onClick.AddListener(() => Run(Refresh()));
            if (DeleteAllButton != null) DeleteAllButton.onClick.AddListener(() => Run(DeleteAll()));

            if (CodeField != null) CodeField.contentType = TMP_InputField.ContentType.Password;

            for (int i = 0; i < RoomDeleteButtons.Length; i++)
            {
                int index = i;
                if (RoomDeleteButtons[i] != null)
                    RoomDeleteButtons[i].onClick.AddListener(() => Run(DeleteAt(index)));
            }

            if (Panel != null) Panel.SetActive(false);
        }

        // =====================================================================

        void Open()
        {
            AudioManager.Instance.PlayButton();
            if (Panel != null) Panel.SetActive(true);

            _rooms.Clear();
            if (CodeField != null) CodeField.text = "";
            SetStatus(DevAccess.Unlocked ? "" : "開発者コードを入力してください。");
            RefreshView();
        }

        void Close()
        {
            AudioManager.Instance.PlayButton();
            if (Panel != null) Panel.SetActive(false);
        }

        void TryUnlock()
        {
            AudioManager.Instance.PlayButton();

            string code = CodeField != null ? CodeField.text : "";
            if (DevAccess.TryUnlock(code))
            {
                SetStatus("解除しました。");
                if (CodeField != null) CodeField.text = "";
                RefreshView();
                Run(Refresh());
            }
            else
            {
                SetStatus("コードが違います。", true);
            }
        }

        void RefreshView()
        {
            bool unlocked = DevAccess.Unlocked;
            if (LockedGroup != null) LockedGroup.SetActive(!unlocked);
            if (UnlockedGroup != null) UnlockedGroup.SetActive(unlocked);
            if (!unlocked) return;

            for (int i = 0; i < RoomLabels.Length; i++)
            {
                bool has = i < _rooms.Count;
                // 行の下敷きごと消さないと、空っぽの帯が並んだままになる
                if (i < RoomRows.Length && RoomRows[i] != null) RoomRows[i].SetActive(has);
                if (RoomLabels[i] != null && has) RoomLabels[i].text = _rooms[i].Describe();
                if (RoomDeleteButtons[i] != null)
                {
                    RoomDeleteButtons[i].gameObject.SetActive(has);
                    RoomDeleteButtons[i].interactable = !_busy;
                }
            }

            if (RefreshButton != null) RefreshButton.interactable = !_busy;
            if (DeleteAllButton != null) DeleteAllButton.interactable = !_busy && _rooms.Count > 0;
        }

        void SetStatus(string message, bool error = false)
        {
            if (StatusText == null) return;
            StatusText.text = message;
            StatusText.color = error
                ? new Color(0.855f, 0.353f, 0.290f)
                : new Color(0.639f, 0.616f, 0.588f);
        }

        void Run(IEnumerator routine)
        {
            if (_busy) return;
            StartCoroutine(Wrap(routine));
        }

        IEnumerator Wrap(IEnumerator routine)
        {
            _busy = true;
            RefreshView();
            yield return routine;
            _busy = false;
            RefreshView();
        }

        // =====================================================================
        // 通信
        // =====================================================================

        RoomAdmin Admin
        {
            get
            {
                if (_admin == null) _admin = new RoomAdmin(GameSession.Online);
                return _admin;
            }
        }

        /// <summary>トークンがあるか。一覧は公開リポジトリなら無くても取れるが、削除には要る。</summary>
        static bool HasToken
        {
            get
            {
                var cfg = GameSession.Online;
                cfg.Load();
                return !string.IsNullOrWhiteSpace(cfg.EffectiveToken);
            }
        }

        bool CheckReady()
        {
            if (!HasToken)
            {
                SetStatus("アクセストークンがありません。先に通信対戦の設定で入力してください。", true);
                return false;
            }
            return true;
        }

        IEnumerator Refresh()
        {
            if (!DevAccess.Unlocked) yield break;

            SetStatus("ルームを取得中…");
            _rooms.Clear();

            bool ok = false;
            string error = null;
            List<RoomInfo> rooms = null;

            yield return Admin.ListRooms((o, r, e) => { ok = o; rooms = r; error = e; });

            if (!ok)
            {
                SetStatus("取得に失敗: " + error, true);
                yield break;
            }

            if (rooms != null) _rooms.AddRange(rooms);

            string extra = _rooms.Count > RoomLabels.Length
                ? $"（{RoomLabels.Length} 件まで表示。残り {_rooms.Count - RoomLabels.Length} 件は「全部削除」で消せます）"
                : "";
            string warn = HasToken ? "" : "　※削除にはアクセストークンが要ります";
            SetStatus(_rooms.Count == 0
                ? "残っているルームはありません。" + warn
                : $"ルーム {_rooms.Count} 件{extra}{warn}");
        }

        IEnumerator DeleteAt(int index)
        {
            if (!DevAccess.Unlocked || !CheckReady()) yield break;
            if (index < 0 || index >= _rooms.Count) yield break;

            var room = _rooms[index];
            SetStatus($"「{room.Id}」を削除中…");

            bool ok = false;
            int deleted = 0;
            string error = null;
            yield return Admin.DeleteRoom(room.Id, (o, n, e) => { ok = o; deleted = n; error = e; });

            if (!ok)
            {
                SetStatus($"「{room.Id}」の削除に失敗: {error}", true);
                yield break;
            }

            SetStatus($"「{room.Id}」を削除しました（{deleted} ファイル）。");
            yield return Refresh();
        }

        IEnumerator DeleteAll()
        {
            if (!DevAccess.Unlocked || !CheckReady()) yield break;
            if (_rooms.Count == 0) yield break;

            var targets = new List<string>();
            foreach (var r in _rooms) targets.Add(r.Id);

            int done = 0, files = 0;
            foreach (var id in targets)
            {
                SetStatus($"削除中… {done + 1}/{targets.Count}（{id}）");

                bool ok = false;
                int n = 0;
                string error = null;
                yield return Admin.DeleteRoom(id, (o, c, e) => { ok = o; n = c; error = e; });

                if (!ok)
                {
                    SetStatus($"「{id}」で失敗: {error}", true);
                    yield return Refresh();
                    yield break;
                }
                done++;
                files += n;
            }

            SetStatus($"{done} ルーム / {files} ファイルを削除しました。");
            yield return Refresh();
        }
    }
}
