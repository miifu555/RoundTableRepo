using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RoundTable.Data
{
    /// <summary>
    /// ゲーム全体で使う参照をまとめたアセット。
    /// Assets/RoundTable/Resources/RoundTableDatabase.asset に1つだけ置く
    /// (シーンをまたいで参照するため Resources に置いている)。
    /// </summary>
    [CreateAssetMenu(fileName = "RoundTableDatabase", menuName = "Round Table/Game Database", order = 1)]
    public sealed class GameDatabase : ScriptableObject
    {
        const string ResourcePath = "RoundTableDatabase";

        [Header("デッキ")]
        public List<DeckAsset> Decks = new List<DeckAsset>();

        [Header("共通アート")]
        [Tooltip("カード裏面。500 x 700 px。")]
        public Sprite CardBack;
        [Tooltip("対戦画面の背景。1920 x 1080 px。")]
        public Sprite BattleBackground;
        [Tooltip("タイトル / デッキ選択の背景。1920 x 1080 px。")]
        public Sprite TitleBackground;
        [Tooltip("^^ (だお) 獲得済み。96 x 96 px。")]
        public Sprite DaoOn;
        [Tooltip("^^ (だお) 未獲得。96 x 96 px。")]
        public Sprite DaoOff;

        [Header("BGM")]
        [Tooltip("タイトルとデッキ選択で流す曲。")]
        public AudioClip BgmTitle;
        [Tooltip("対戦中に流す曲。")]
        public AudioClip BgmBattle;

        [Header("効果音")]
        [Tooltip("タイトル / デッキ選択のボタン。")]
        public AudioClip SeButton;
        [Tooltip("攻撃カードを使ったとき。")]
        public AudioClip SeAttackCard;
        [Tooltip("フィールドカードを使ったとき。")]
        public AudioClip SeFieldCard;
        [Tooltip("ターン終了時。")]
        public AudioClip SeEndTurn;
        [Tooltip("勝利時。")]
        public AudioClip SeWin;
        [Tooltip("敗北時。")]
        public AudioClip SeLose;

        [Header("フォント")]
        public TMP_FontAsset Font;
        public TMP_FontAsset FontBold;

        static GameDatabase _instance;

        public static GameDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GameDatabase>(ResourcePath);
                    if (_instance == null)
                        Debug.LogError($"[RoundTable] Resources/{ResourcePath}.asset が見つかりません。");
                }
                return _instance;
            }
        }

        Dictionary<string, CardData> _cardsById;
        Dictionary<string, DeckAsset> _decksById;

        public DeckAsset GetDeck(string deckId)
        {
            BuildLookup();
            return _decksById != null && _decksById.TryGetValue(deckId ?? "", out var d) ? d : null;
        }

        /// <summary>カードIDからプレハブの CardData を引く。</summary>
        public CardData GetCard(string cardId)
        {
            BuildLookup();
            return _cardsById != null && _cardsById.TryGetValue(cardId ?? "", out var c) ? c : null;
        }

        void BuildLookup()
        {
            if (_cardsById != null) return;
            _cardsById = new Dictionary<string, CardData>();
            _decksById = new Dictionary<string, DeckAsset>();

            foreach (var deck in Decks)
            {
                if (deck == null) continue;
                _decksById[deck.DeckId] = deck;
                foreach (var entry in deck.Entries)
                {
                    if (entry == null || entry.Card == null) continue;
                    _cardsById[entry.Card.CardId] = entry.Card;
                }
            }
        }

        /// <summary>Inspector で中身を変えたあと、キャッシュを捨てる。</summary>
        public void InvalidateLookup()
        {
            _cardsById = null;
            _decksById = null;
        }
    }
}
