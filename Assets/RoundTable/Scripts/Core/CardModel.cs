using System.Collections.Generic;

namespace RoundTable.Core
{
    /// <summary>カードのジャンル。仕様書の「攻撃 / フィールド」に対応。</summary>
    public enum CardKind
    {
        Attack,
        Field,
    }

    /// <summary>
    /// カードの引き方。仕様書より:
    /// Draw   = ドロータイプ (番の始まりに1枚ずつ引く)
    /// Shuffle= シャッフルタイプ (番の初めに山をシャッフルして4枚引く)
    /// </summary>
    public enum DrawStyle
    {
        Draw,
        Shuffle,
    }

    /// <summary>効果の種類。1枚のカードは1つの EffectDef を持つ。</summary>
    public enum EffectKind
    {
        None,

        // ---- 即時効果 (攻撃カード / フィールドの誘発効果) ----
        Damage,                     // Amount 分、相手の気力を削る
        DamageDice,                 // DiceSides 面ダイスの出目分、相手の気力を削る
        DamageBySacrificedHand,     // 手札を好きなだけトラッシュし、その枚数分削る
        Heal,                       // Amount 分、自分の気力を回復する
        DrawCards,                  // Amount 枚引く
        DrawCardsDice,              // DiceSides 面ダイスの出目 + Modifier 枚引く
        DestroyOpponentField,       // 相手のフィールドを Amount 個破壊する
        DestroyOpponentFieldDice,   // 相手のフィールドを (出目 + Modifier) 個破壊する
        MillOpponentDeck,           // 相手の山札を上から Amount 枚削る (完全に失われる)
        DiscardOpponentHand,        // 相手の手札をランダムに Amount 枚トラッシュする
        ExtraTurn,                  // このターンのあと、エクストラターンを得る
        DrawSameAsOpponent,         // 相手が引いた枚数と同じだけ引く (OnOpponentDraw 専用)
        DestroyTriggeringField,     // 誘発元の相手フィールドを破壊する (OnOpponentPlayField 専用)
        BothPlayersDraw,            // 互いに Amount 枚引く

        AllOrNothingDamage,         // DiceSides面ダイスで最大目が出たら Amount 削る。外れたら自分のターンを即終了する

        // ---- 常在効果 (フィールドカード / Trigger == None) ----
        PassiveDamageReduction,         // 削られる気力を Amount 減らす
        PassiveDiceBonus,               // ダイスの出目を Amount 増やす
        PassiveOpponentMaxEnergyDown,   // 相手の気力の最大値を Amount 減らす
        PassiveDamageBoost,             // 自分が与えるダメージを Amount 増やす
    }

    /// <summary>フィールドカードの効果が発動するタイミング。</summary>
    public enum TriggerKind
    {
        /// <summary>誘発しない。攻撃カードの即時効果、またはフィールドの常在効果。</summary>
        None,
        /// <summary>自分のターン開始時。</summary>
        OnOwnTurnStart,
        /// <summary>自分の番の終わり。</summary>
        OnOwnTurnEnd,
        /// <summary>自分の気力を(相手に)削られた時。コスト支払いでは誘発しない。</summary>
        OnDamaged,
        /// <summary>「カードで」気力を削られた時。攻撃カードの解決によるダメージのみ。</summary>
        OnDamagedByCard,
        /// <summary>相手がカードをドローした時。</summary>
        OnOpponentDraw,
        /// <summary>相手がフィールドカードを出した時。</summary>
        OnOpponentPlayField,
        /// <summary>このフィールドが破壊された時。</summary>
        OnThisDestroyed,
    }

    /// <summary>1つの効果の定義。</summary>
    public sealed class EffectDef
    {
        public EffectKind Kind = EffectKind.None;
        public int Amount;
        public int DiceSides;
        public int Modifier;
        /// <summary>解決後にこのフィールドカード自身をトラッシュするか (すどーのカウンター用)。</summary>
        public bool TrashSelfAfter;

        public EffectDef(EffectKind kind, int amount = 0, int diceSides = 0, int modifier = 0, bool trashSelfAfter = false)
        {
            Kind = kind;
            Amount = amount;
            DiceSides = diceSides;
            Modifier = modifier;
            TrashSelfAfter = trashSelfAfter;
        }
    }

    /// <summary>カードの静的定義 (デッキ内の同名カードで共有される)。</summary>
    public sealed class CardDef
    {
        /// <summary>一意なID。イラストのファイル名にもなる (Resources/RoundTable/CardArt/{Id}.png)。</summary>
        public string Id;
        /// <summary>カード名。仕様書で未定のものは仮称 (CardNames.cs で変更可)。</summary>
        public string Name;
        /// <summary>所属デッキID。</summary>
        public string DeckId;
        public CardKind Kind;
        public int Cost;
        /// <summary>効果テキスト (仕様書の「効果」列そのまま)。</summary>
        public string Text;
        public TriggerKind Trigger = TriggerKind.None;
        public EffectDef Effect;
        /// <summary>このデッキに入っている枚数。</summary>
        public int Count;

        public bool IsPassiveField => Kind == CardKind.Field && Trigger == TriggerKind.None;
    }

    /// <summary>場に存在する実体としてのカード1枚。</summary>
    public sealed class CardInstance
    {
        public readonly int Uid;
        public readonly CardDef Def;
        /// <summary>所有プレイヤー番号 (0 or 1)。</summary>
        public int OwnerIndex;

        public CardInstance(int uid, CardDef def, int ownerIndex)
        {
            Uid = uid;
            Def = def;
            OwnerIndex = ownerIndex;
        }

        public string Name => Def.Name;
        public int Cost => Def.Cost;
        public CardKind Kind => Def.Kind;
        public override string ToString() => $"{Def.Name}({Def.Cost})";
    }

    /// <summary>キャラクター1人分のデッキ定義。</summary>
    public sealed class DeckDef
    {
        public string Id;
        /// <summary>キャラクター名 (仕様書の名前)。</summary>
        public string CharacterName;
        /// <summary>アーキタイプ表記 (例: 「耐久系」「アグロ(運メイン)」)。</summary>
        public string Archetype;
        public DrawStyle DrawStyle;
        public readonly List<CardDef> Cards = new List<CardDef>();

        /// <summary>デッキ総枚数 (仕様上は必ず15)。</summary>
        public int TotalCards
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Cards.Count; i++) n += Cards[i].Count;
                return n;
            }
        }

        public string DrawStyleLabel => DrawStyle == DrawStyle.Draw ? "ドロータイプ" : "シャッフルタイプ";
    }
}
