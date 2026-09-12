using System.Collections.Generic;

using RoundTable.Core;

namespace RoundTable.EditorTools
{
    /// <summary>
    /// Notion 仕様書「Round Table(円卓カードゲーム)」のデッキ表 (2026-09-12 更新ぶんを反映)。
    /// これはプレハブ / DeckAsset を一括生成するための「初期データ」で、ゲーム実行時には使われない。
    /// 生成後の編集は Prefabs/Cards/ と Data/Decks/ の各アセットで行うこと。
    /// カード名が仕様書で空欄だったものは仮称を付けている (末尾に「(仮)」は付けず、
    /// ここを書き換えるだけで反映される)。今のところ空欄なのは すどー と 林 の全カード。
    /// </summary>
    public static class CardSeedData
    {
        static List<DeckDef> _all;

        public static IReadOnlyList<DeckDef> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        public static DeckDef Get(string id)
        {
            var all = All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Id == id) return all[i];
            return null;
        }

        // ---- 構築ヘルパ ------------------------------------------------------

        static DeckDef _cur;

        static DeckDef Deck(string id, string name, string archetype, DrawStyle style)
        {
            _cur = new DeckDef { Id = id, CharacterName = name, Archetype = archetype, DrawStyle = style };
            _all.Add(_cur);
            return _cur;
        }

        static void Atk(string localId, string name, int cost, int count, string text, EffectDef effect)
        {
            _cur.Cards.Add(new CardDef
            {
                Id = _cur.Id + "_" + localId,
                Name = name,
                DeckId = _cur.Id,
                Kind = CardKind.Attack,
                Cost = cost,
                Count = count,
                Text = text,
                Trigger = TriggerKind.None,
                Effect = effect,
            });
        }

        static void Fld(string localId, string name, int cost, int count, string text, TriggerKind trigger, EffectDef effect)
        {
            _cur.Cards.Add(new CardDef
            {
                Id = _cur.Id + "_" + localId,
                Name = name,
                DeckId = _cur.Id,
                Kind = CardKind.Field,
                Cost = cost,
                Count = count,
                Text = text,
                Trigger = trigger,
                Effect = effect,
            });
        }

        static EffectDef E(EffectKind k, int amount = 0, int diceSides = 0, int modifier = 0, bool trashSelf = false)
            => new EffectDef(k, amount, diceSides, modifier, trashSelf);

        // ---- デッキ定義 ------------------------------------------------------

        static void Build()
        {
            _all = new List<DeckDef>();

            // ===== とーしー　耐久系 / ドロータイプ =====
            Deck("toshi", "とーしー", "耐久系", DrawStyle.Draw);
            Atk("a1", "ｺﾞﾒﾝﾈ!!", 2, 3, "相手の気力を3削る", E(EffectKind.Damage, 3));
            Atk("a2", "足を踏む", 3, 2, "相手のフィールドを一つトラッシュする", E(EffectKind.DestroyOpponentField, 1));
            Atk("a3", "月6000円バイト", 3, 4, "2枚カードを引く", E(EffectKind.DrawCards, 2));
            Atk("a4", "爆睡の極み", 7, 1, "このターンのあと、エクストラターンを得る", E(EffectKind.ExtraTurn));
            Fld("f1", "昼寝", 4, 3, "番の終わり、気力を2回復する", TriggerKind.OnOwnTurnEnd, E(EffectKind.Heal, 2));
            Fld("f2", "保身に走る", 4, 2, "削られる気力を-1する", TriggerKind.None, E(EffectKind.PassiveDamageReduction, 1));

            // ===== 鈴木　アグロ(運メイン) / シャッフルタイプ =====
            Deck("suzuki", "鈴木", "アグロ(運メイン)", DrawStyle.Shuffle);
            Atk("a1", "軽い挑発", 2, 2, "相手の気力を2削る", E(EffectKind.Damage, 2));
            Atk("a2", "賭け事", 2, 5, "4面サイコロで出た目分気力を削る", E(EffectKind.DamageDice, diceSides: 4));
            Atk("a3", "飲酒", 2, 2, "2枚カードを引く", E(EffectKind.DrawCards, 2));
            Atk("a4", "勝負師の勘", 1, 2, "4面サイコロで出た目-2個相手のフィールドを破壊する", E(EffectKind.DestroyOpponentFieldDice, diceSides: 4, modifier: -2));
            Atk("a5", "パック開封", 1, 2, "4面サイコロで出た目-2枚カードを引く", E(EffectKind.DrawCardsDice, diceSides: 4, modifier: -2));
            Fld("f1", "猿知恵", 4, 2, "ダイスを振った時、その出目は+1になる", TriggerKind.None, E(EffectKind.PassiveDiceBonus, 1));

            // ===== ざき　コントロール(盤面妨害) / ドロータイプ =====
            Deck("zaki", "ざき", "コントロール(盤面妨害)", DrawStyle.Draw);
            Atk("a1", "暴力へのレッドカード", 1, 4, "相手の気力を2削る", E(EffectKind.Damage, 2));
            Atk("a2", "下調べ", 1, 2, "2枚カードを引く", E(EffectKind.DrawCards, 2));
            Atk("a3", "計画的撤去", 2, 2, "相手のフィールドを一つ破壊する", E(EffectKind.DestroyOpponentField, 1));
            Fld("f1", "望まぬタンク", 1, 3, "気力を削られた時、相手の気力も2削る", TriggerKind.OnDamaged, E(EffectKind.Damage, 2));
            Fld("f2", "連帯責任", 3, 2, "気力を削られた時、相手のフィールドを一つ破壊する", TriggerKind.OnDamaged, E(EffectKind.DestroyOpponentField, 1));
            Fld("f3", "便乗", 5, 2, "相手が攻撃カードの効果でドローしたとき、自分も同じ枚数引ける", TriggerKind.OnOpponentDraw, E(EffectKind.DrawSameAsOpponent));

            // ===== 奥野　LO / シャッフルタイプ =====
            Deck("okuno", "奥野", "LO", DrawStyle.Shuffle);
            Atk("a1", "四皇としての意地", 3, 2, "相手の気力を3削る", E(EffectKind.Damage, 3));
            Atk("a2", "爆弾発言", 3, 5, "相手の山札を6枚削る", E(EffectKind.MillOpponentDeck, 6));
            Atk("a3", "5億で買うえ", 2, 2, "3枚カードを引く", E(EffectKind.DrawCards, 3));
            Atk("a4", "脅迫", 1, 2, "4面サイコロで出た目-2個相手のフィールドを破壊する", E(EffectKind.DestroyOpponentFieldDice, diceSides: 4, modifier: -2));
            Fld("f1", "挑発", 2, 4, "気力を削られた時、相手の山札を上から3枚削る", TriggerKind.OnDamaged, E(EffectKind.MillOpponentDeck, 3));

            // ===== かっしー　アグロ(必殺) / ドロータイプ =====
            // 2026-09-11 の仕様更新で、すどーと丸ごとデッキが入れ替わった。
            Deck("kasshi", "かっしー", "アグロ(必殺)", DrawStyle.Draw);
            Atk("a1", "捨て身タックル", 7, 6, "自分の手札を好きなだけトラッシュして、その枚数分相手の気力を削る", E(EffectKind.DamageBySacrificedHand));
            Atk("a2", "気迫", 1, 4, "1枚カードを引く", E(EffectKind.DrawCards, 1));
            Atk("a3", "四皇としての意地", 2, 2, "相手のフィールドを１つ破壊する", E(EffectKind.DestroyOpponentField, 1));
            Atk("a4", "白虎進軍", 9, 1, "6面サイコロで6が出たら、相手の気力を10削る。それ以外ならターンを終わる。", E(EffectKind.AllOrNothingDamage, 10, diceSides: 6));
            Fld("f1", "闘争心", 2, 2, "カードで気力を削られたとき、2枚ドローする", TriggerKind.OnDamagedByCard, E(EffectKind.DrawCards, 2));

            // ===== しょー　アグロ(連撃) / シャッフルタイプ =====
            Deck("show", "しょー", "アグロ(連撃)", DrawStyle.Shuffle);
            Atk("a1", "古武術", 2, 5, "相手の気力を2削る", E(EffectKind.Damage, 2));
            Atk("a2", "山登り", 2, 3, "カードを2枚引く", E(EffectKind.DrawCards, 2));
            Atk("a3", "四皇としての意地", 1, 2, "相手のフィールドを1個破壊する", E(EffectKind.DestroyOpponentField, 1));
            Fld("f1", "神社参り", 3, 1, "自分のターン開始時、カードを1枚引く", TriggerKind.OnOwnTurnStart, E(EffectKind.DrawCards, 1));
            Fld("f2", "先制攻撃", 2, 4, "自分のターン開始時、相手の気力を2削る", TriggerKind.OnOwnTurnStart, E(EffectKind.Damage, 2));

            // ===== こうの　アグロ(除去) / シャッフルタイプ =====
            Deck("kono", "こうの", "アグロ(除去)", DrawStyle.Shuffle);
            Atk("a1", "切り込む", 2, 3, "相手の気力を2削る", E(EffectKind.Damage, 2));
            Atk("a2", "決死のドロー", 1, 2, "2枚カードを引く", E(EffectKind.DrawCards, 2));
            Atk("a3", "盤面除去", 2, 4, "相手のフィールドを2つ破壊する", E(EffectKind.DestroyOpponentField, 2));
            Fld("f1", "万一の保険", 2, 4, "相手にフィールドを出されたとき、それを破壊してこのカードをトラッシュする。", TriggerKind.OnOpponentPlayField, E(EffectKind.DestroyTriggeringField, trashSelf: true));
            Fld("f2", "誘爆", 2, 2, "気力を削られた時、相手の気力も1削る", TriggerKind.OnDamaged, E(EffectKind.Damage, 1));

            // ===== あぽろ　コントロール(手札干渉) / ドロータイプ =====
            Deck("apollo", "あぽろ", "コントロール(手札干渉)", DrawStyle.Draw);
            Atk("a1", "ツッコミを入れる", 2, 3, "相手の気力を3削る", E(EffectKind.Damage, 3));
            Atk("a2", "運命のドロー", 1, 3, "2枚カードを引く", E(EffectKind.DrawCards, 2));
            Atk("a3", "意思無き踊り", 1, 2, "相手の手札を2枚トラッシュする", E(EffectKind.DiscardOpponentHand, 2));
            Atk("a4", "四皇としての意地", 2, 2, "相手のフィールドを１つ破壊する", E(EffectKind.DestroyOpponentField, 1));
            Fld("f1", "攪乱", 3, 5, "カードで気力を削られたとき、相手の手札をランダムに2枚トラッシュする", TriggerKind.OnDamagedByCard, E(EffectKind.DiscardOpponentHand, 2));

            // ===== すどー　アグロ(一点集中) / ドロータイプ =====
            // 2026-09-11 の仕様更新で、かっしーと丸ごとデッキが入れ替わった。
            // 仕様書のカード名が空欄なので、旧かっしー当時の名前をそのまま仮称として使っている。
            Deck("sudo", "すどー", "アグロ(一点集中)", DrawStyle.Draw);
            Atk("a1", "右ストレート", 5, 2, "相手の気力を4削る", E(EffectKind.Damage, 4));
            Atk("a2", "タックル", 7, 2, "相手の気力を6削る", E(EffectKind.Damage, 6));
            Atk("a3", "深呼吸", 3, 3, "カードを1枚引く", E(EffectKind.DrawCards, 1));
            Atk("a4", "なぎ払い", 5, 2, "相手のフィールドを2個破壊する", E(EffectKind.DestroyOpponentField, 2));
            Atk("a5", "大振り", 4, 3, "6面サイコロで出た目分気力を削る", E(EffectKind.DamageDice, diceSides: 6));
            Fld("f1", "反抗心", 4, 3, "気力を削られた時、カードを1枚ドローする", TriggerKind.OnDamaged, E(EffectKind.DrawCards, 1));

            // ===== りくと　アグロ(ミッドレンジ) / シャッフルタイプ =====
            Deck("rikuto", "りくと", "アグロ(ミッドレンジ)", DrawStyle.Shuffle);
            Atk("a1", "スポ大の意地", 3, 3, "相手の気力を3削る", E(EffectKind.Damage, 3));
            Atk("a2", "盗撮", 2, 3, "相手の気力を2削る", E(EffectKind.Damage, 2));
            Atk("a3", "闘志", 2, 3, "1枚カードを引く", E(EffectKind.DrawCards, 1));
            Atk("a4", "コートの破壊", 3, 3, "相手のフィールドを2つ破壊する", E(EffectKind.DestroyOpponentField, 2));
            Fld("f1", "おちょくる", 1, 3, "このフィールドが破壊されたとき、互いにカードを2枚引く", TriggerKind.OnThisDestroyed, E(EffectKind.BothPlayersDraw, 2));

            // ===== 林　アグロ(設置) / シャッフルタイプ =====
            // 2026-09-11 の仕様更新でキャラ名が「没ステ」から変わった。カード内容は変更なし。
            // デッキID (botsu) はプレハブ/アセットのパス安定のためそのまま維持。
            Deck("botsu", "林", "アグロ(設置)", DrawStyle.Shuffle);
            Atk("a1", "試作の一撃", 3, 3, "相手の気力を2削る", E(EffectKind.Damage, 2));
            Atk("a2", "拾い読み", 1, 2, "2枚カードを引く", E(EffectKind.DrawCards, 2));
            Atk("a3", "解体", 2, 2, "相手のフィールドを１つ破壊する", E(EffectKind.DestroyOpponentField, 1));
            Fld("f1", "重圧", 3, 3, "相手の気力の最大値を1減らす", TriggerKind.None, E(EffectKind.PassiveOpponentMaxEnergyDown, 1));
            Fld("f2", "棘の設置", 3, 3, "自分のターン開始時、相手の気力を1削る", TriggerKind.OnOwnTurnStart, E(EffectKind.Damage, 1));
            Fld("f3", "意地", 2, 2, "気力を削られた時、相手の気力も1削る", TriggerKind.OnDamaged, E(EffectKind.Damage, 1));

            _cur = null;
        }
    }
}
