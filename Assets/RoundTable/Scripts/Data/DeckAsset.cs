using System;
using System.Collections.Generic;
using RoundTable.Core;
using UnityEngine;

namespace RoundTable.Data
{
    /// <summary>
    /// デッキに入れるカード1種類ぶんの記述。「どのカードを何枚」を表す。
    /// </summary>
    [Serializable]
    public sealed class DeckEntry
    {
        [Tooltip("カードのプレハブ (Prefabs/Cards/{デッキID}/ 以下)。")]
        public CardData Card;

        [Min(0)]
        [Tooltip("このデッキに入れる枚数。")]
        public int Count = 1;

        public DeckEntry() { }

        public DeckEntry(CardData card, int count)
        {
            Card = card;
            Count = count;
        }

        public bool IsValid => Card != null && Count > 0;
    }

    /// <summary>
    /// キャラクター1人分のデッキ。Assets/RoundTable/Data/Decks/{deckId}.asset
    ///
    /// カードの「種類」と「枚数」はこのアセットで管理する。
    /// Inspector に専用の編集画面が出るので、そこで足し引きすればよい。
    /// </summary>
    [CreateAssetMenu(fileName = "Deck", menuName = "Round Table/Deck", order = 0)]
    public sealed class DeckAsset : ScriptableObject
    {
        /// <summary>仕様書で決まっているデッキ枚数。</summary>
        public const int RequiredCardCount = 15;

        [Header("識別")]
        public string DeckId;
        public string CharacterName;
        [Tooltip("アーキタイプ表記。例: 耐久系 / アグロ(運メイン)")]
        public string Archetype;

        [Header("ルール")]
        [Tooltip("ドロータイプ = 番の始まりに1枚 / シャッフルタイプ = 番の初めに山をシャッフルして4枚")]
        public DrawStyle DrawStyle = DrawStyle.Draw;

        [Header("絵")]
        [Tooltip("デッキ選択画面の立ち絵。512 x 768 px。")]
        public Sprite Portrait;
        [Tooltip("対戦画面の顔アイコン。256 x 256 px。未設定なら立ち絵を流用。")]
        public Sprite PortraitIcon;

        [Header("カード構成")]
        [Tooltip("このデッキに入るカードと枚数。合計が15枚になるようにする。")]
        public List<DeckEntry> Entries = new List<DeckEntry>();

        public Sprite IconOrPortrait => PortraitIcon != null ? PortraitIcon : Portrait;

        public string DrawStyleLabel => DrawStyle == DrawStyle.Draw ? "ドロータイプ" : "シャッフルタイプ";

        /// <summary>デッキ合計枚数。</summary>
        public int TotalCards
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Entries.Count; i++)
                {
                    var e = Entries[i];
                    if (e != null && e.IsValid) n += e.Count;
                }
                return n;
            }
        }

        /// <summary>カードの種類数 (枚数ではなく何種類入っているか)。</summary>
        public int UniqueCards
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Entries.Count; i++)
                    if (Entries[i] != null && Entries[i].IsValid) n++;
                return n;
            }
        }

        public bool HasValidCardCount => TotalCards == RequiredCardCount;

        /// <summary>指定カードの投入枚数。入っていなければ 0。</summary>
        public int CountOf(CardData card)
        {
            if (card == null) return 0;
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i] != null && Entries[i].Card == card) return Entries[i].Count;
            return 0;
        }

        /// <summary>カードを追加する (すでにあれば枚数を足す)。</summary>
        public void Add(CardData card, int count = 1)
        {
            if (card == null || count <= 0) return;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i] != null && Entries[i].Card == card)
                {
                    Entries[i].Count += count;
                    return;
                }
            }
            Entries.Add(new DeckEntry(card, count));
        }

        /// <summary>有効な行だけを順に返す。</summary>
        public IEnumerable<DeckEntry> ValidEntries
        {
            get
            {
                for (int i = 0; i < Entries.Count; i++)
                    if (Entries[i] != null && Entries[i].IsValid) yield return Entries[i];
            }
        }

        /// <summary>ルールエンジン用の定義に変換する。</summary>
        public DeckDef ToDefinition()
        {
            var def = new DeckDef
            {
                Id = DeckId,
                CharacterName = CharacterName,
                Archetype = Archetype,
                DrawStyle = DrawStyle,
            };
            foreach (var e in ValidEntries)
                def.Cards.Add(e.Card.ToDefinition(e.Count));
            return def;
        }
    }
}
