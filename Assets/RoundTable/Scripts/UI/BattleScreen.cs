using System.Collections.Generic;
using System.Text;
using RoundTable.App;
using RoundTable.Core;
using RoundTable.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoundTable.UI
{
    /// <summary>対戦画面。盤面の描画と入力を担当し、状態変更は必ず MatchController 経由で行う。</summary>
    public sealed class BattleScreen : MonoBehaviour
    {
        [Header("進行")]
        public MatchController Match;

        [Header("背景")]
        public Image Background;
        public GameObject BackgroundPlaceholder;

        [Header("情報バー")]
        public PlayerInfoPanel TopInfo;
        public PlayerInfoPanel BottomInfo;

        [Header("盤面コンテナ")]
        public RectTransform TopHandContainer;
        public RectTransform TopFieldContainer;
        public RectTransform BottomFieldContainer;
        public RectTransform HandContainer;
        public TMP_Text TopFieldLabel;
        public TMP_Text BottomFieldLabel;
        public TMP_Text HandEmptyLabel;

        [Header("中央バー")]
        public TMP_Text CenterText;
        public Button EndTurnButton;
        public Button ConfirmButton;
        public Button SurrenderButton;

        [Header("右カラム")]
        public RectTransform ZoomContainer;
        public TMP_Text ZoomHint;
        public TMP_Text LogText;
        [Tooltip("ログに出す行数")] public int LogLines = 16;

        [Header("結果オーバーレイ")]
        public GameObject ResultOverlay;
        public TMP_Text ResultTitle;
        public TMP_Text ResultSub;
        public Button NextRoundButton;
        public Button ExitButton;

        [Header("接続オーバーレイ")]
        public GameObject StatusOverlay;
        public TMP_Text StatusOverlayText;
        public Button StatusCancelButton;

        [Header("カード裏面")]
        public GameObject CardBackPrefab;

        [Header("配色")]
        public Color GoldColor = new Color(0.847f, 0.706f, 0.376f, 1f);
        public Color DangerColor = new Color(0.855f, 0.353f, 0.290f, 1f);

        bool _dirty = true;
        CardData _zoomed;
        GameObject _zoomInstance;
        readonly List<GameObject> _spawned = new List<GameObject>();

        RoundTableGame Game => Match != null ? Match.Game : null;

        void Start()
        {
            var db = GameDatabase.Instance;
            if (Background != null && db != null && db.BattleBackground != null)
            {
                Background.sprite = db.BattleBackground;
                Background.gameObject.SetActive(true);
                if (BackgroundPlaceholder != null) BackgroundPlaceholder.SetActive(false);
            }
            else if (Background != null)
            {
                Background.gameObject.SetActive(false);
            }

            if (EndTurnButton != null) EndTurnButton.onClick.AddListener(() => { Match.EndTurn(); _dirty = true; });
            if (ConfirmButton != null) ConfirmButton.onClick.AddListener(() => { Match.ConfirmChoice(); _dirty = true; });
            if (SurrenderButton != null) SurrenderButton.onClick.AddListener(() => { Match.Surrender(); _dirty = true; });
            if (NextRoundButton != null) NextRoundButton.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlayButton();
                Match.ProceedToNextRound();
                _dirty = true;
            });
            if (ExitButton != null) ExitButton.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlayButton();
                SceneFlow.GoTitle();
            });
            if (StatusCancelButton != null) StatusCancelButton.onClick.AddListener(SceneFlow.GoTitle);

            if (Match != null) Match.OnChanged += () => _dirty = true;

            AudioManager.Instance.PlayBattleBgm();
            _dirty = true;
        }

        void Update()
        {
            if (!_dirty) return;
            _dirty = false;
            Refresh();
        }

        // =====================================================================

        void Refresh()
        {
            RefreshStatusOverlay();
            if (Game == null) return;

            int bottom = Match.BottomPlayerIndex;
            int top = 1 - bottom;

            var pBottom = Game.Players[bottom];
            var pTop = Game.Players[top];

            ClearSpawned();

            if (TopInfo != null) TopInfo.Bind(Game, pTop, DeckOf(top), RoleLabel(top));
            if (BottomInfo != null) BottomInfo.Bind(Game, pBottom, DeckOf(bottom), RoleLabel(bottom));

            BuildOpponentHand(pTop);
            BuildField(TopFieldContainer, TopFieldLabel, pTop, true);
            BuildField(BottomFieldContainer, BottomFieldLabel, pBottom, false);
            BuildHand(pBottom);
            BuildCenter();
            BuildLog();
            BuildResultOverlay();
            BuildZoom();
        }

        DeckAsset DeckOf(int index)
        {
            var db = GameDatabase.Instance;
            if (db == null || Game == null) return null;
            return db.GetDeck(Game.Players[index].Deck.Id);
        }

        string RoleLabel(int index)
        {
            if (Match.IsVsAi) return index == GameSession.LocalPlayerIndex ? "YOU" : "CPU";
            if (Match.IsOnline) return index == GameSession.LocalPlayerIndex ? "YOU" : "OPPONENT";
            return index == 0 ? "PLAYER 1" : "PLAYER 2";
        }

        void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] == null) continue;
                _spawned[i].transform.SetParent(null, false);
                Destroy(_spawned[i]);
            }
            _spawned.Clear();
        }

        GameObject Spawn(GameObject prefab, RectTransform parent, float x, float y)
        {
            var go = Instantiate(prefab, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            _spawned.Add(go);
            return go;
        }

        static float StepFor(int count, float areaWidth, float cardWidth, float preferredGap)
        {
            if (count <= 1) return 0f;
            return Mathf.Min(cardWidth + preferredGap, (areaWidth - cardWidth) / (count - 1));
        }

        // ---- 相手の手札 (裏向き) ----

        void BuildOpponentHand(PlayerState p)
        {
            if (TopHandContainer == null || CardBackPrefab == null) return;

            int n = p.Hand.Count;
            if (n == 0) return;

            float areaW = TopHandContainer.rect.width;
            int w = ArtSizes.OppHandCardW;
            float step = StepFor(n, areaW - 40f, w, 6f);
            float total = step * (n - 1) + w;
            float startX = (areaW - total) * 0.5f;

            for (int i = 0; i < n; i++)
            {
                var go = Spawn(CardBackPrefab, TopHandContainer, startX + step * i, 2f);
                go.transform.localScale = Vector3.one * (w / (float)ArtSizes.CardW);
            }
        }

        // ---- フィールド ----

        void BuildField(RectTransform container, TMP_Text label, PlayerState p, bool isTop)
        {
            if (container == null) return;

            if (label != null)
                label.text = (isTop ? "相手のフィールド" : "自分のフィールド") + $"（{p.Fields.Count}）";

            int n = p.Fields.Count;
            if (n == 0) return;

            bool choosing = Match.CanAct
                            && Game.Phase == GamePhase.AwaitingChoice
                            && Game.PendingChoice == ChoiceKind.DestroyOpponentFields
                            && p.Index != Game.CurrentIndex;

            float areaW = container.rect.width;
            float areaH = container.rect.height;
            int w = ArtSizes.FieldCardW;
            int h = ArtSizes.HeightFor(w);
            float step = StepFor(n, areaW - 40f, w, 10f);
            float total = step * (n - 1) + w;
            float startX = (areaW - total) * 0.5f;
            float y = Mathf.Max(4f, (areaH - h) * 0.5f);

            for (int i = 0; i < n; i++)
            {
                var card = p.Fields[i];
                var data = GameDatabase.Instance.GetCard(card.Def.Id);
                if (data == null) continue;

                var go = Spawn(data.gameObject, container, startX + step * i, y);
                var visual = go.GetComponent<CardVisual>();
                if (visual != null)
                {
                    visual.Apply(data);
                    visual.SetMode(CardDisplayMode.Compact);
                    visual.SetWidth(w);
                    visual.SetHighlight(choosing, DangerColor);
                    visual.SetDim(false);
                }
                HideBadge(go);
                Hook(go, card, data, choosing ? OnFieldClicked : (System.Action<CardInteraction>)null);
            }
        }

        // ---- 自分の手札 ----

        void BuildHand(PlayerState p)
        {
            if (HandContainer == null) return;

            bool hidden = !Match.IsFixedSeat && GameSession.HideInactiveHand && p.Index != Game.CurrentIndex;
            int n = p.Hand.Count;

            if (HandEmptyLabel != null)
            {
                HandEmptyLabel.gameObject.SetActive(n == 0 || hidden);
                HandEmptyLabel.text = hidden ? "（手番のプレイヤーの手札のみ表示されます）" : "手札なし";
            }
            if (n == 0 || hidden) return;

            bool sacrificing = Game.Phase == GamePhase.AwaitingChoice && Game.PendingChoice == ChoiceKind.SacrificeHand;
            bool myTurn = Match.CanAct && p.Index == Game.CurrentIndex;

            float areaW = HandContainer.rect.width;
            float areaH = HandContainer.rect.height;
            int w = ArtSizes.HandCardW;
            int h = ArtSizes.HeightFor(w);
            float step = StepFor(n, areaW - 24f, w, 12f);
            float total = step * (n - 1) + w;
            float startX = (areaW - total) * 0.5f;
            float y = Mathf.Max(0f, (areaH - h) * 0.5f);

            for (int i = 0; i < n; i++)
            {
                var card = p.Hand[i];
                var data = GameDatabase.Instance.GetCard(card.Def.Id);
                if (data == null) continue;

                bool selectable = myTurn && Game.IsSelectableHandCard(card);

                var go = Spawn(data.gameObject, HandContainer, startX + step * i, y);
                var visual = go.GetComponent<CardVisual>();
                if (visual != null)
                {
                    visual.Apply(data);
                    visual.SetMode(CardDisplayMode.Full);
                    visual.SetWidth(w);

                    if (sacrificing && Game.ChoiceSelection.Contains(card)) visual.SetHighlight(true, DangerColor);
                    else if (selectable && !sacrificing) visual.SetHighlight(true, GoldColor);
                    else visual.SetHighlight(false, GoldColor);

                    visual.SetDim(!selectable);
                }
                HideBadge(go);
                Hook(go, card, data, selectable ? OnHandClicked : (System.Action<CardInteraction>)null);
            }
        }

        static void HideBadge(GameObject go)
        {
            var badge = go.GetComponent<CardCountBadge>();
            if (badge != null) badge.Hide();
        }

        void Hook(GameObject go, CardInstance card, CardData data, System.Action<CardInteraction> onClick)
        {
            var hook = go.GetComponent<CardInteraction>();
            if (hook == null) return;
            hook.Instance = card;
            hook.Data = data;
            hook.Clicked = onClick;
            hook.Hovered = h =>
            {
                if (_zoomed != h.Data) { _zoomed = h.Data; _dirty = true; }
            };
        }

        // ---- 中央バー ----

        void BuildCenter()
        {
            bool canAct = Match.CanAct;
            int cur = Game.CurrentIndex;

            string who;
            if (Match.IsVsAi)
                who = cur == GameSession.LocalPlayerIndex ? "あなたのターン" : "CPUのターン（思考中）";
            else if (Match.IsOnline)
                who = cur == GameSession.LocalPlayerIndex ? "あなたのターン" : "相手のターン（待機中）";
            else
                who = $"プレイヤー{cur + 1} のターン";

            string extra = "";
            if (canAct && Game.Phase == GamePhase.AwaitingChoice)
            {
                if (Game.PendingChoice == ChoiceKind.DestroyOpponentFields)
                    extra = $"　→　破壊する相手のフィールドを選択（残り {Game.ChoiceRemaining}）";
                else if (Game.PendingChoice == ChoiceKind.SacrificeHand)
                    extra = $"　→　トラッシュする手札を選択（{Game.ChoiceSelection.Count} 枚 = {Game.ChoiceSelection.Count} ダメージ）";
            }

            if (CenterText != null)
                CenterText.text = $"ラウンド {Game.RoundNumber}　<b>{who}</b>"
                                + $"　<size=18>({Game.TurnCount}/{RoundTableGame.RoundTurnLimit} 手番)</size>{extra}";

            if (EndTurnButton != null)
                EndTurnButton.gameObject.SetActive(canAct && Game.Phase == GamePhase.Play);

            if (ConfirmButton != null)
                ConfirmButton.gameObject.SetActive(canAct && Game.Phase == GamePhase.AwaitingChoice
                                                   && Game.PendingChoice == ChoiceKind.SacrificeHand);

            if (SurrenderButton != null)
                SurrenderButton.gameObject.SetActive(Game.Phase != GamePhase.MatchOver);
        }

        // ---- ログ ----

        void BuildLog()
        {
            if (LogText == null) return;
            var sb = new StringBuilder();
            int count = Game.Log.Count;
            int start = Mathf.Max(0, count - LogLines);
            for (int i = start; i < count; i++)
            {
                sb.Append(Game.Log[i]);
                if (i < count - 1) sb.Append('\n');
            }
            LogText.text = sb.ToString();
        }

        // ---- 拡大表示 ----

        void BuildZoom()
        {
            if (ZoomContainer == null) return;

            if (_zoomInstance != null)
            {
                _zoomInstance.transform.SetParent(null, false);
                Destroy(_zoomInstance);
                _zoomInstance = null;
            }

            if (ZoomHint != null) ZoomHint.gameObject.SetActive(_zoomed == null);
            if (_zoomed == null) return;

            int w = ArtSizes.ZoomCardW;
            float x = (ZoomContainer.rect.width - w) * 0.5f;

            _zoomInstance = Instantiate(_zoomed.gameObject, ZoomContainer);
            var rt = (RectTransform)_zoomInstance.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -4f);

            var visual = _zoomInstance.GetComponent<CardVisual>();
            if (visual != null)
            {
                visual.Apply(_zoomed);
                visual.SetMode(CardDisplayMode.Full);
                visual.SetWidth(w);
                visual.SetHighlight(false, GoldColor);
                visual.SetDim(false);
            }
            HideBadge(_zoomInstance);

            var hook = _zoomInstance.GetComponent<CardInteraction>();
            if (hook != null) { hook.Clicked = null; hook.Hovered = null; }
        }

        // ---- 結果 / 接続 ----

        void BuildResultOverlay()
        {
            bool show = Game.Phase == GamePhase.RoundOver || Game.Phase == GamePhase.MatchOver;
            if (ResultOverlay != null) ResultOverlay.SetActive(show);
            if (!show) return;

            int me = Match.IsFixedSeat ? GameSession.LocalPlayerIndex : 0;

            if (Game.Phase == GamePhase.MatchOver)
            {
                int w = Game.MatchWinnerIndex;
                if (ResultTitle != null)
                {
                    ResultTitle.text = w < 0 ? "DRAW" : (Match.IsFixedSeat ? (w == me ? "WIN" : "LOSE") : $"PLAYER {w + 1} WIN");
                    ResultTitle.color = w < 0 ? Color.white : ((!Match.IsFixedSeat || w == me) ? GoldColor : DangerColor);
                }
                if (ResultSub != null)
                    ResultSub.text = w < 0
                        ? "^^ が並んだため引き分け"
                        : $"{Game.Players[w].DisplayName} が ^^ を{RoundTableGame.DaoToWin}つ集めた";

                if (NextRoundButton != null) NextRoundButton.gameObject.SetActive(false);
                if (ExitButton != null) ExitButton.gameObject.SetActive(true);
            }
            else
            {
                int rw = Game.RoundWinnerIndex;
                if (ResultTitle != null)
                {
                    ResultTitle.text = rw < 0 ? "引き分け（両者 ^^ 獲得）"
                                     : (Match.IsFixedSeat ? (rw == me ? "^^ を獲得!" : "^^ を取られた…")
                                                       : $"プレイヤー{rw + 1} が ^^ を獲得!");
                    ResultTitle.color = rw < 0 ? Color.white : ((!Match.IsFixedSeat || rw == me) ? GoldColor : DangerColor);
                }
                if (ResultSub != null)
                    ResultSub.text = $"{Game.Players[0].DisplayName} {Game.Players[0].Dao}  -  {Game.Players[1].Dao} {Game.Players[1].DisplayName}";

                if (NextRoundButton != null)
                {
                    NextRoundButton.gameObject.SetActive(true);
                    var label = NextRoundButton.GetComponentInChildren<TMP_Text>();
                    if (label != null) label.text = $"ラウンド {Game.RoundNumber + 1} へ";
                }
                if (ExitButton != null) ExitButton.gameObject.SetActive(true);
            }
        }

        void RefreshStatusOverlay()
        {
            if (StatusOverlay == null) return;

            bool show = Match == null || Match.Status != MatchStatus.Playing;
            StatusOverlay.SetActive(show);
            if (!show) return;

            if (StatusOverlayText != null)
                StatusOverlayText.text = Match != null ? Match.StatusMessage : "初期化中…";
        }

        // ---- 入力 ----

        void OnHandClicked(CardInteraction hook)
        {
            if (hook.Instance == null) return;

            if (Game.Phase == GamePhase.AwaitingChoice && Game.PendingChoice == ChoiceKind.SacrificeHand)
                Match.ToggleHandSelection(hook.Instance);
            else
                Match.PlayCard(hook.Instance);

            _dirty = true;
        }

        void OnFieldClicked(CardInteraction hook)
        {
            if (hook.Instance == null) return;
            Match.ChooseFieldTarget(hook.Instance);
            _dirty = true;
        }
    }
}
