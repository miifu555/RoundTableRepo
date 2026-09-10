using System;
using System.Collections.Generic;
using RoundTable.Core;
using RoundTable.Net;

namespace RoundTable.App
{
    public enum AiDifficulty
    {
        /// <summary>手なりで出す。たまにわざと弱い手を選ぶ。</summary>
        Easy,
        /// <summary>評価値が一番高い手を出す。</summary>
        Normal,
        /// <summary>決定打があれば自爆してでも取りに行く。フィールドの選び方も丁寧。</summary>
        Hard,
    }

    /// <summary>
    /// CPU対戦用の簡易AI。1回の呼び出しで「次にやること」を1つだけ返す。
    /// 返すのは通信対戦と同じ操作コードなので、適用経路は人間の操作とまったく同じ。
    /// </summary>
    public static class SimpleAi
    {
        /// <summary>
        /// 次の操作を決める。何もできなければ null (呼び出し側でターン終了させる)。
        /// </summary>
        public static string ChooseAction(RoundTableGame game, Random rng, AiDifficulty difficulty)
        {
            if (game == null) return null;

            if (game.Phase == GamePhase.AwaitingChoice) return ResolveChoice(game, rng, difficulty);
            if (game.Phase != GamePhase.Play) return null;

            var me = game.Current;
            var opp = game.Opponent;

            CardInstance best = null;
            int bestScore = 0;
            var affordable = new List<CardInstance>();

            foreach (var card in me.Hand)
            {
                if (!game.CanPlay(card)) continue;

                int score = Score(game, me, opp, card, difficulty);
                if (score <= 0) continue;

                // 決定打でない限り、コスト支払いで気力が0になる手は打たない (自爆回避)
                bool suicide = me.Energy - card.Cost <= 0;
                if (suicide && score < LethalScore) continue;

                affordable.Add(card);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = card;
                }
            }

            if (best == null) return null;

            // 弱いAIはときどき最善手を選ばない
            if (difficulty == AiDifficulty.Easy && affordable.Count > 1 && bestScore < LethalScore && rng.Next(100) < 45)
                best = affordable[rng.Next(affordable.Count)];

            return ActionCode.Play(best.Uid);
        }

        const int LethalScore = 1000;

        // =====================================================================
        // 選択待ちの解決
        // =====================================================================

        static string ResolveChoice(RoundTableGame game, Random rng, AiDifficulty difficulty)
        {
            var me = game.Current;
            var opp = game.Opponent;

            if (game.PendingChoice == ChoiceKind.DestroyOpponentFields)
            {
                var target = PickFieldTarget(opp.Fields, rng, difficulty);
                return target != null ? ActionCode.Field(target.Uid) : ActionCode.Confirm.ToString();
            }

            if (game.PendingChoice == ChoiceKind.SacrificeHand)
            {
                // 必要な枚数だけ切る。足りなければ全部切る。
                int needed = opp.Energy + game.DamageReductionOf(opp);
                int want = me.Hand.Count <= needed ? me.Hand.Count : needed;

                if (game.ChoiceSelection.Count >= want) return ActionCode.Confirm.ToString();

                // 価値の低い(コストが低い)カードから切る
                CardInstance pick = null;
                foreach (var c in me.Hand)
                {
                    if (game.ChoiceSelection.Contains(c)) continue;
                    if (pick == null || c.Cost < pick.Cost) pick = c;
                }
                return pick != null ? ActionCode.Hand(pick.Uid) : ActionCode.Confirm.ToString();
            }

            return ActionCode.Confirm.ToString();
        }

        static CardInstance PickFieldTarget(List<CardInstance> fields, Random rng, AiDifficulty difficulty)
        {
            if (fields == null || fields.Count == 0) return null;
            if (difficulty == AiDifficulty.Easy) return fields[rng.Next(fields.Count)];

            CardInstance best = null;
            int bestValue = int.MinValue;
            foreach (var f in fields)
            {
                int v = f.Cost * 2;
                // 放っておくと効き続けるものを優先して壊す
                if (f.Def.Trigger == TriggerKind.OnOwnTurnStart) v += 8;
                if (f.Def.Trigger == TriggerKind.None) v += 6;      // 常在 (軽減・最大値減など)
                if (f.Def.Trigger == TriggerKind.OnDamaged) v += 3;
                if (v > bestValue) { bestValue = v; best = f; }
            }
            return best;
        }

        // =====================================================================
        // 評価
        // =====================================================================

        static int Score(RoundTableGame game, PlayerState me, PlayerState opp, CardInstance card, AiDifficulty difficulty)
        {
            var e = card.Def.Effect;
            if (e == null) return 0;

            int reduction = game.DamageReductionOf(opp);
            int diceBonus = game.DiceBonusOf(me);

            switch (e.Kind)
            {
                case EffectKind.Damage:
                {
                    int dmg = Effective(e.Amount, reduction);
                    if (dmg >= opp.Energy) return LethalScore;
                    return dmg * 12;
                }
                case EffectKind.DamageDice:
                {
                    int avg = (e.DiceSides + 1) / 2 + diceBonus;
                    int dmg = Effective(avg, reduction);
                    int max = Effective(e.DiceSides + diceBonus, reduction);
                    if (max >= opp.Energy) return 400 + dmg * 10;
                    return dmg * 10;
                }
                case EffectKind.DamageBySacrificedHand:
                {
                    // このカード自身はもう手札から出ている
                    int dmg = Effective(me.Hand.Count - 1, reduction);
                    if (dmg >= opp.Energy) return LethalScore;
                    return dmg * 9;
                }
                case EffectKind.DestroyOpponentField:
                {
                    if (opp.Fields.Count == 0) return 0;
                    int n = e.Amount < opp.Fields.Count ? e.Amount : opp.Fields.Count;
                    return n * 16;
                }
                case EffectKind.DestroyOpponentFieldDice:
                {
                    if (opp.Fields.Count == 0) return 0;
                    int avg = (e.DiceSides + 1) / 2 + diceBonus + e.Modifier;
                    return avg <= 0 ? 4 : avg * 12;
                }
                case EffectKind.DrawCards:
                    return me.Hand.Count >= 8 ? e.Amount * 3 : e.Amount * 11;

                case EffectKind.DrawCardsDice:
                {
                    int avg = (e.DiceSides + 1) / 2 + diceBonus + e.Modifier;
                    if (avg <= 0) return 3;
                    return me.Hand.Count >= 8 ? avg * 3 : avg * 10;
                }
                case EffectKind.MillOpponentDeck:
                    if (opp.DrawPile.Count <= e.Amount) return LethalScore; // デッキアウト
                    return e.Amount * 5;

                case EffectKind.DiscardOpponentHand:
                {
                    int n = e.Amount < opp.Hand.Count ? e.Amount : opp.Hand.Count;
                    return n * 10;
                }
                case EffectKind.ExtraTurn:
                    return 60;
            }

            // フィールドカード (常在・誘発)
            if (card.Kind == CardKind.Field)
            {
                int v = 26;

                // 同名フィールドを並べ過ぎない
                int same = 0;
                foreach (var f in me.Fields) if (f.Def.Id == card.Def.Id) same++;
                v -= same * 5;

                // カウンターは相手のフィールドが多いほど腐りにくい
                if (card.Def.Trigger == TriggerKind.OnOpponentPlayField && difficulty != AiDifficulty.Easy)
                    v += 4;

                return v > 0 ? v : 1;
            }

            return 1;
        }

        static int Effective(int raw, int reduction)
        {
            int v = raw - reduction;
            return v > 0 ? v : 0;
        }
    }
}
