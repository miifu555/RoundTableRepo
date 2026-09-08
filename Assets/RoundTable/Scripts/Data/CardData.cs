using RoundTable.Core;
using UnityEngine;

namespace RoundTable.Data
{
    /// <summary>
    /// カード1枚分のデータ。カードのプレハブ (Prefabs/Cards/{deckId}/{cardId}.prefab) のルートに付く。
    /// 効果も絵もここを Inspector で書き換えるだけで反映される。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardData : MonoBehaviour
    {
        [Header("識別")]
        [Tooltip("一意なID。プレハブ名と揃えておくこと。")]
        public string CardId;
        [Tooltip("カード名。仕様書で未確定だったものは仮称。")]
        public string DisplayName;
        [Tooltip("所属デッキのID。")]
        public string DeckId;

        [Header("基本")]
        public CardKind Kind = CardKind.Attack;
        [Min(0)] public int Cost;
        [TextArea(2, 4)]
        [Tooltip("カードに印刷される効果テキスト。表示専用で、実際の挙動は下の設定で決まる。")]
        public string EffectText;

        [Header("効果 (実際の挙動)")]
        [Tooltip("フィールドカードの発動タイミング。攻撃カードと常在効果は None。")]
        public TriggerKind Trigger = TriggerKind.None;
        public EffectKind Effect = EffectKind.None;
        [Tooltip("ダメージ量 / 回復量 / 枚数など。")]
        public int Amount;
        [Tooltip("ダイスの面数。0ならダイスを振らない。")]
        public int DiceSides;
        [Tooltip("ダイスの出目に足す補正。「出た目-2」なら -2。")]
        public int Modifier;
        [Tooltip("解決後にこのフィールド自身をトラッシュするか (すどー「待ち伏せ」用)。")]
        public bool TrashSelfAfter;

        [Header("絵")]
        [Tooltip("カード枠内のイラスト。440 x 330 px。")]
        public Sprite Illustration;
        [Tooltip("カード全体を1枚絵で差し替える場合。500 x 700 px。設定するとイラスト枠より優先される。")]
        public Sprite FullCardImage;

        /// <summary>
        /// ルールエンジン用の定義に変換する。
        /// 投入枚数はカードではなくデッキ (DeckAsset) が持つので引数で受け取る。
        /// </summary>
        public CardDef ToDefinition(int copiesInDeck)
        {
            return new CardDef
            {
                Id = string.IsNullOrEmpty(CardId) ? name : CardId,
                Name = string.IsNullOrEmpty(DisplayName) ? name : DisplayName,
                DeckId = DeckId,
                Kind = Kind,
                Cost = Cost,
                Count = Mathf.Max(1, copiesInDeck),
                Text = EffectText,
                Trigger = Trigger,
                Effect = new EffectDef(Effect, Amount, DiceSides, Modifier, TrashSelfAfter),
            };
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Inspector でいじったら見た目も追従させる
            var visual = GetComponent<CardVisual>();
            if (visual != null) visual.Apply(this);
        }
#endif
    }
}
