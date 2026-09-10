using System.Collections.Generic;
using RoundTable.App;
using RoundTable.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoundTable.UI
{
    /// <summary>キャラ(デッキ)選択画面。</summary>
    public sealed class DeckSelectScreen : MonoBehaviour
    {
        [Header("グリッド")]
        public RectTransform Grid;
        public DeckCell CellPrefab;
        [Tooltip("1行あたりのセル数")] public int PerRow = 6;
        public Vector2 CellSize = new Vector2(220f, 400f);
        public float GapX = 20f;
        public float Row1Y = 168f;
        public float Row2Y = 578f;

        [Header("下部バー")]
        public TMP_Text StatusText;
        public Button ViewDeckButton;
        public Button StartButton;
        public Button BackButton;

        [Header("デッキ一覧オーバーレイ")]
        public GameObject DeckListOverlay;
        public RectTransform DeckListContainer;
        public TMP_Text DeckListTitle;
        public Button DeckListCloseButton;
        public int DeckListCardWidth = 230;

        readonly List<DeckCell> _cells = new List<DeckCell>();
        DeckAsset _mine, _opponent;
        bool _pickingOpponent;

        bool IsOnline => GameSession.Mode == MatchMode.Online;
        bool IsVsAi => GameSession.Mode == MatchMode.VsAi;

        static string AiLevelLabel
        {
            get
            {
                switch (GameSession.AiLevel)
                {
                    case AiDifficulty.Easy: return "よわい";
                    case AiDifficulty.Hard: return "つよい";
                    default: return "ふつう";
                }
            }
        }

        void Start()
        {
            var db = GameDatabase.Instance;
            if (db == null || CellPrefab == null || Grid == null) return;

            for (int i = 0; i < db.Decks.Count; i++)
            {
                var deck = db.Decks[i];
                if (deck == null) continue;

                var cell = Instantiate(CellPrefab, Grid);
                cell.Bind(deck, OnCellClicked);

                int row = i / PerRow;
                int col = i % PerRow;
                int inRow = Mathf.Min(PerRow, db.Decks.Count - row * PerRow);
                float rowWidth = inRow * CellSize.x + (inRow - 1) * GapX;
                float startX = (ArtSizes.ReferenceWidth - rowWidth) * 0.5f;

                var rt = (RectTransform)cell.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = CellSize;
                rt.anchoredPosition = new Vector2(startX + col * (CellSize.x + GapX), -(row == 0 ? Row1Y : Row2Y));

                _cells.Add(cell);
            }

            if (ViewDeckButton != null) ViewDeckButton.onClick.AddListener(() => { Click(); ShowDeckList(); });
            if (StartButton != null) StartButton.onClick.AddListener(() => { Click(); StartMatch(); });
            if (BackButton != null) BackButton.onClick.AddListener(() => { Click(); SceneFlow.GoTitle(); });
            if (DeckListCloseButton != null) DeckListCloseButton.onClick.AddListener(() => { Click(); DeckListOverlay.SetActive(false); });

            // タイトルと同じ曲なので、画面が変わっても途切れずに続く
            AudioManager.Instance.PlayTitleBgm();
            if (DeckListOverlay != null) DeckListOverlay.SetActive(false);

            Refresh();
        }

        static void Click() => AudioManager.Instance.PlayButton();

        void OnCellClicked(DeckCell cell)
        {
            Click();

            if (IsOnline)
            {
                // 通信対戦では自分のデッキだけ選ぶ (相手のデッキは相手が選ぶ)
                _mine = cell.Deck;
            }
            else if (!_pickingOpponent)
            {
                _mine = cell.Deck;
                if (_opponent == cell.Deck) _opponent = null;
                _pickingOpponent = true;
            }
            else
            {
                _opponent = cell.Deck;
                _pickingOpponent = false;
            }
            Refresh();
        }

        void Refresh()
        {
            foreach (var c in _cells)
            {
                var state = c.Deck == _mine ? DeckCell.SelectionState.Mine
                          : c.Deck == _opponent ? DeckCell.SelectionState.Opponent
                          : DeckCell.SelectionState.None;
                c.SetSelection(state);
            }

            if (StatusText != null)
            {
                if (IsOnline)
                {
                    string role = GameSession.Online.IsHost ? "ホスト(プレイヤー1)" : "ゲスト(プレイヤー2)";
                    StatusText.text = $"通信対戦 / {role}　部屋「{GameSession.Online.RoomId}」　　"
                                    + $"あなたのデッキ: <b>{(_mine != null ? _mine.CharacterName : "―")}</b>"
                                    + "　　（相手のデッキは相手が選びます）";
                }
                else if (IsVsAi)
                {
                    string step = _pickingOpponent ? "② CPUのデッキを選択"
                                : (_mine == null ? "① あなたのデッキを選択" : "キャラをクリックで選び直し");
                    StatusText.text = $"CPU対戦（{AiLevelLabel}）　{step}　　"
                                    + $"あなた: <b>{Name(_mine)}</b>　VS　CPU: <b>{Name(_opponent)}</b>";
                }
                else
                {
                    string step = _pickingOpponent ? "② 相手のデッキを選択"
                                : (_mine == null ? "① あなたのデッキを選択" : "キャラをクリックで選び直し");
                    StatusText.text = $"{step}　　　プレイヤー1: <b>{Name(_mine)}</b>　VS　プレイヤー2: <b>{Name(_opponent)}</b>";
                }
            }

            if (StartButton != null)
                StartButton.interactable = IsOnline ? _mine != null : (_mine != null && _opponent != null);
            if (ViewDeckButton != null)
                ViewDeckButton.interactable = _mine != null;
        }

        static string Name(DeckAsset d) => d != null ? d.CharacterName : "―";

        void StartMatch()
        {
            if (_mine == null) return;

            if (IsOnline)
            {
                if (GameSession.Online.IsHost) GameSession.Deck0 = _mine;
                else GameSession.Deck1 = _mine;
            }
            else
            {
                if (_opponent == null) return;
                GameSession.Deck0 = _mine;
                GameSession.Deck1 = _opponent;
            }

            SceneFlow.GoBattle();
        }

        // ---- デッキ一覧 ----

        void ShowDeckList()
        {
            if (_mine == null || DeckListOverlay == null || DeckListContainer == null) return;

            DeckListOverlay.SetActive(true);
            for (int i = DeckListContainer.childCount - 1; i >= 0; i--)
            {
                var c = DeckListContainer.GetChild(i);
                c.SetParent(null, false);
                Destroy(c.gameObject);
            }

            if (DeckListTitle != null)
                DeckListTitle.text = $"{_mine.CharacterName}　{_mine.Archetype} / {_mine.DrawStyleLabel}　（全 {_mine.TotalCards} 枚）";

            int w = DeckListCardWidth;
            int h = ArtSizes.HeightFor(w);
            int n = _mine.Entries.Count;
            if (n == 0) return;

            float gap = 18f;
            float step = n > 1
                ? Mathf.Min(w + gap, (ArtSizes.ReferenceWidth - 60f - w) / (n - 1))
                : 0f;
            float total = step * (n - 1) + w;
            float startX = Mathf.Max(30f, (ArtSizes.ReferenceWidth - total) * 0.5f);
            float y = (ArtSizes.ReferenceHeight - h) * 0.5f - 20f;

            for (int i = 0; i < n; i++)
            {
                var entry = _mine.Entries[i];
                if (entry == null || !entry.IsValid) continue;
                var data = entry.Card;

                var go = Instantiate(data.gameObject, DeckListContainer);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(startX + step * i, -y);

                var visual = go.GetComponent<CardVisual>();
                if (visual != null)
                {
                    visual.Apply(data);
                    visual.SetMode(CardDisplayMode.Full);
                    visual.SetWidth(w);
                }

                var badge = go.GetComponent<CardCountBadge>();
                if (badge != null) badge.Show(entry.Count);
            }
        }
    }
}
