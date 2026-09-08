using System.Collections.Generic;

namespace RoundTable.Core
{
    /// <summary>プレイヤー1人分の盤面状態。</summary>
    public sealed class PlayerState
    {
        public int Index;
        public DeckDef Deck;
        public string DisplayName;
        public bool IsHuman;

        /// <summary>気力。HPとコストを兼ねる。</summary>
        public int Energy;
        /// <summary>獲得した ^^ (だお)。3つで勝利。</summary>
        public int Dao;

        /// <summary>山札。index 0 が一番上。</summary>
        public readonly List<CardInstance> DrawPile = new List<CardInstance>();
        public readonly List<CardInstance> Hand = new List<CardInstance>();
        public readonly List<CardInstance> Fields = new List<CardInstance>();

        /// <summary>
        /// 一時トラッシュ。使用したカード・破壊されたフィールド・手札から捨てたカードが入り、
        /// 自分の次のターン開始時に「使用順で山の下」へ戻る。
        /// </summary>
        public readonly List<CardInstance> PendingReturn = new List<CardInstance>();

        /// <summary>完全に失われたカード置き場。山札削り(LO)で削られたカードのみが入る。</summary>
        public readonly List<CardInstance> Trash = new List<CardInstance>();

        public void ResetForRound()
        {
            DrawPile.Clear();
            Hand.Clear();
            Fields.Clear();
            PendingReturn.Clear();
            Trash.Clear();
            Energy = 0;
        }
    }
}
