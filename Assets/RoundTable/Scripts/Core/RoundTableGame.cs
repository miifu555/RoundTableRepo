using System;
using System.Collections.Generic;

namespace RoundTable.Core
{
    public enum GamePhase
    {
        NotStarted,
        /// <summary>手番プレイヤーがカードを出せる状態。</summary>
        Play,
        /// <summary>プレイヤーの選択待ち (フィールド破壊対象 / 手札トラッシュ)。</summary>
        AwaitingChoice,
        /// <summary>ラウンド終了 (だおが入った)。次ラウンド開始待ち。</summary>
        RoundOver,
        /// <summary>だお3つ獲得でマッチ終了。</summary>
        MatchOver,
    }

    public enum ChoiceKind
    {
        None,
        /// <summary>相手のフィールドを ChoiceRemaining 個まで選んで破壊する。</summary>
        DestroyOpponentFields,
        /// <summary>自分の手札を好きなだけ選んでトラッシュする (こうの「全身全霊」)。</summary>
        SacrificeHand,
    }

    /// <summary>
    /// Round Table のルールエンジン。UI から独立した純 C# 実装。
    /// </summary>
    public sealed class RoundTableGame
    {
        // ---- ルール定数 (仕様書由来) ----
        public const int BaseMaxEnergy = 10;   // 「気力が10枚与えられ」「ターンが返ってくると、10に回復」
        public const int DaoToWin = 3;         // 「だおを3つ集めたプレイヤーの勝利」
        public const int ShuffleTypeDrawCount = 4; // 「山をシャッフルして4枚引く」
        public const int DrawTypeDrawCount = 1;    // 「番の始まりに1枚ずつ引ける」

        /// <summary>
        /// 初手の手札枚数。仕様書に記載が無いための補完。
        /// シャッフルタイプは自分の番の頭で4枚引くため0枚から開始する。
        /// </summary>
        public const int DrawTypeOpeningHand = 4;
        public const int ShuffleTypeOpeningHand = 0;

        /// <summary>
        /// 1ラウンドの手番数の上限 (ハウスルール)。仕様書に千日手の規定が無く、
        /// 使ったカードは山に戻るためデッキも尽きない。そのため
        /// 「削られる気力を-1」×3 と 3ダメージ主体のデッキが噛み合うと永久に決着しない。
        /// 上限に達したラウンドは引き分けとし、両者に ^^ を1つ与えて必ず決着させる。
        /// </summary>
        public const int RoundTurnLimit = 60;

        const int MaxResolveDepth = 32; // 誘発の相互ループ防止

        public readonly PlayerState[] Players = new PlayerState[2];
        public GamePhase Phase { get; private set; } = GamePhase.NotStarted;
        public int CurrentIndex { get; private set; }
        public int RoundNumber { get; private set; }
        /// <summary>このラウンドで消化した手番数。</summary>
        public int TurnCount { get; private set; }
        /// <summary>ラウンド勝者。-1 は未決着または引き分け。</summary>
        public int RoundWinnerIndex { get; private set; } = -1;
        /// <summary>マッチ勝者。Phase が MatchOver で -1 なら引き分け。</summary>
        public int MatchWinnerIndex { get; private set; } = -1;
        /// <summary>直前のラウンドが手番上限による引き分けだったか。</summary>
        public bool LastRoundWasDraw { get; private set; }

        public ChoiceKind PendingChoice { get; private set; } = ChoiceKind.None;
        public int ChoiceRemaining { get; private set; }
        public readonly List<CardInstance> ChoiceSelection = new List<CardInstance>();

        public readonly List<string> Log = new List<string>();
        public event Action<string> OnLogAppended;
        public event Action OnStateChanged;
        /// <summary>ダイスを振ったときの通知 (roller, sides, rawRoll, finalValue)。</summary>
        public event Action<int, int, int, int> OnDiceRolled;

        readonly Random _rng;
        int _nextUid = 1;
        int _resolveDepth;
        int _roundStarterIndex;
        readonly bool[] _extraTurn = new bool[2];
        CardInstance _choiceSourceCard;

        public PlayerState Current => Players[CurrentIndex];
        public PlayerState Opponent => Players[1 - CurrentIndex];
        public PlayerState Other(PlayerState p) => Players[1 - p.Index];

        public RoundTableGame(DeckDef deck0, DeckDef deck1, bool player0IsHuman = true, int seed = 0)
        {
            _rng = seed == 0 ? new Random() : new Random(seed);

            Players[0] = new PlayerState { Index = 0, Deck = deck0, DisplayName = deck0.CharacterName, IsHuman = player0IsHuman };
            Players[1] = new PlayerState { Index = 1, Deck = deck1, DisplayName = deck1.CharacterName, IsHuman = false };
        }

        // =====================================================================
        // マッチ / ラウンド進行
        // =====================================================================

        public void StartMatch()
        {
            Players[0].Dao = 0;
            Players[1].Dao = 0;
            RoundNumber = 0;
            MatchWinnerIndex = -1;
            _roundStarterIndex = _rng.Next(2);
            AddLog("=== Round Table 開始 ===");
            StartRound();
        }

        public void StartRound()
        {
            RoundNumber++;
            RoundWinnerIndex = -1;
            TurnCount = 0;
            LastRoundWasDraw = false;
            _extraTurn[0] = _extraTurn[1] = false;
            PendingChoice = ChoiceKind.None;
            ChoiceSelection.Clear();
            _choiceSourceCard = null;

            for (int i = 0; i < 2; i++)
            {
                var p = Players[i];
                p.ResetForRound();
                BuildDeck(p);
                Shuffle(p.DrawPile);
                p.Energy = BaseMaxEnergy;
                int opening = p.Deck.DrawStyle == DrawStyle.Draw ? DrawTypeOpeningHand : ShuffleTypeOpeningHand;
                for (int n = 0; n < opening && p.DrawPile.Count > 0; n++)
                    MoveTop(p.DrawPile, p.Hand);
            }

            CurrentIndex = _roundStarterIndex;
            _roundStarterIndex = 1 - _roundStarterIndex; // 次ラウンドは先攻交代

            AddLog($"--- ラウンド {RoundNumber} 開始 (先攻: {Current.DisplayName}) ---");
            Phase = GamePhase.Play;
            BeginTurn();
        }

        void BuildDeck(PlayerState p)
        {
            foreach (var def in p.Deck.Cards)
                for (int i = 0; i < def.Count; i++)
                    p.DrawPile.Add(new CardInstance(_nextUid++, def, p.Index));
        }

        void BeginTurn()
        {
            TurnCount++;
            if (TurnCount > RoundTurnLimit)
            {
                DeclareDrawRound();
                return;
            }

            var p = Current;

            // 1. 気力が最大値まで回復する
            p.Energy = MaxEnergyOf(p);
            AddLog($"[{p.DisplayName}] のターン開始 (気力 {p.Energy}/{MaxEnergyOf(p)})");

            // 2. 一時トラッシュのカードを使用順で山の下へ戻す
            if (p.PendingReturn.Count > 0)
            {
                AddLog($"  トラッシュ {p.PendingReturn.Count} 枚を山の下へ戻した");
                p.DrawPile.AddRange(p.PendingReturn);
                p.PendingReturn.Clear();
            }

            // 3. 「自分のターン開始時」のフィールド効果
            FireTriggers(p, TriggerKind.OnOwnTurnStart, null);
            if (Phase != GamePhase.Play) return;

            // 4. ドロー
            if (p.Deck.DrawStyle == DrawStyle.Shuffle)
            {
                // 仕様: 「番の初めに持っている手札ごと山をシャッフルして4枚引く」
                // 手札は毎ターン作り直しになるので、抱え込みができない。
                if (p.Hand.Count > 0)
                {
                    AddLog($"  手札 {p.Hand.Count} 枚を山札に戻した");
                    p.DrawPile.AddRange(p.Hand);
                    p.Hand.Clear();
                }
                Shuffle(p.DrawPile);
                AddLog("  山札をシャッフル");
                DrawCards(p, ShuffleTypeDrawCount);
            }
            else
            {
                DrawCards(p, DrawTypeDrawCount);
            }

            Changed();
        }

        public void EndTurn()
        {
            if (Phase != GamePhase.Play) return;

            var p = Current;
            FireTriggers(p, TriggerKind.OnOwnTurnEnd, null);
            if (Phase != GamePhase.Play) { Changed(); return; }

            if (_extraTurn[p.Index])
            {
                _extraTurn[p.Index] = false;
                AddLog($"[{p.DisplayName}] エクストラターン!");
            }
            else
            {
                CurrentIndex = 1 - CurrentIndex;
            }

            BeginTurn();
            Changed();
        }

        /// <summary>ラウンド終了後、次のラウンドへ進む。</summary>
        public void ProceedToNextRound()
        {
            if (Phase != GamePhase.RoundOver) return;
            StartRound();
            Changed();
        }

        // =====================================================================
        // カードのプレイ
        // =====================================================================

        public bool CanPlay(CardInstance card)
        {
            if (Phase != GamePhase.Play) return false;
            if (card == null) return false;
            var p = Current;
            if (!p.Hand.Contains(card)) return false;
            return p.Energy >= card.Cost;
        }

        /// <summary>手札からカードを出す。選択が必要な場合は Phase が AwaitingChoice になる。</summary>
        public bool PlayCard(CardInstance card)
        {
            if (!CanPlay(card)) return false;

            var p = Current;
            p.Hand.Remove(card);
            AddLog($"[{p.DisplayName}] 《{card.Name}》 を使用 (コスト {card.Cost})");

            // コスト支払い。0になったら自爆。
            PayCost(p, card.Cost);
            if (Phase != GamePhase.Play)
            {
                p.PendingReturn.Add(card);
                Changed();
                return true;
            }

            if (card.Kind == CardKind.Field)
            {
                PlayFieldCard(p, card);
                Changed();
                return true;
            }

            // 攻撃カード
            if (RequiresChoice(p, card, out var kind, out int amount))
            {
                _choiceSourceCard = card;
                PendingChoice = kind;
                ChoiceRemaining = amount;
                ChoiceSelection.Clear();
                Phase = GamePhase.AwaitingChoice;
                Changed();
                return true;
            }

            ResolveEffect(p, card.Def.Effect, card, null);
            FinishCardResolution(p, card);
            Changed();
            return true;
        }

        void PlayFieldCard(PlayerState p, CardInstance card)
        {
            // 相手の「相手にフィールドを出されたとき」カウンターを確認
            var opp = Other(p);
            CardInstance counter = null;
            foreach (var f in opp.Fields)
            {
                if (f.Def.Trigger == TriggerKind.OnOpponentPlayField) { counter = f; break; }
            }

            if (counter != null)
            {
                AddLog($"  [{opp.DisplayName}] の《{counter.Name}》が発動");
                DestroyField(card, wasOnField: false);
                MoveFieldToPending(opp, counter, "トラッシュ");
                Changed();
                return;
            }

            p.Fields.Add(card);
            AddLog($"  フィールドに《{card.Name}》を設置");
        }

        bool RequiresChoice(PlayerState p, CardInstance card, out ChoiceKind kind, out int amount)
        {
            kind = ChoiceKind.None;
            amount = 0;
            var e = card.Def.Effect;
            if (e == null) return false;

            if (e.Kind == EffectKind.DamageBySacrificedHand)
            {
                kind = ChoiceKind.SacrificeHand;
                amount = p.Hand.Count;
                return true; // 手札0枚でも「0枚トラッシュ」の確定が要る
            }

            if (e.Kind == EffectKind.DestroyOpponentField)
            {
                var opp = Other(p);
                // 相手のフィールドが破壊数より多いときだけ選択させる
                if (opp.Fields.Count > e.Amount && e.Amount > 0)
                {
                    kind = ChoiceKind.DestroyOpponentFields;
                    amount = e.Amount;
                    return true;
                }
            }
            return false;
        }

        void FinishCardResolution(PlayerState p, CardInstance card)
        {
            p.PendingReturn.Add(card);
        }

        // ---- 選択の解決 ----

        /// <summary>フィールド破壊の対象を1つ選ぶ。</summary>
        public bool ChooseFieldTarget(CardInstance field)
        {
            if (Phase != GamePhase.AwaitingChoice || PendingChoice != ChoiceKind.DestroyOpponentFields) return false;
            var opp = Other(Current);
            if (!opp.Fields.Contains(field)) return false;

            DestroyField(field, wasOnField: true);
            ChoiceRemaining--;

            if (ChoiceRemaining <= 0 || opp.Fields.Count == 0)
                CompleteChoice();
            Changed();
            return true;
        }

        /// <summary>手札トラッシュの選択をトグルする。</summary>
        public bool ToggleHandSelection(CardInstance card)
        {
            if (Phase != GamePhase.AwaitingChoice || PendingChoice != ChoiceKind.SacrificeHand) return false;
            if (!Current.Hand.Contains(card)) return false;
            if (ChoiceSelection.Contains(card)) ChoiceSelection.Remove(card);
            else ChoiceSelection.Add(card);
            Changed();
            return true;
        }

        /// <summary>選択を確定する。</summary>
        public void ConfirmChoice()
        {
            if (Phase != GamePhase.AwaitingChoice) return;

            if (PendingChoice == ChoiceKind.SacrificeHand)
            {
                var p = Current;
                int n = ChoiceSelection.Count;
                foreach (var c in ChoiceSelection)
                {
                    p.Hand.Remove(c);
                    p.PendingReturn.Add(c);
                }
                AddLog($"  手札 {n} 枚をトラッシュ");
                if (n > 0) DealDamage(Other(p), p, n, true);
            }

            CompleteChoice();
            Changed();
        }

        void CompleteChoice()
        {
            var p = Current;
            if (_choiceSourceCard != null)
            {
                FinishCardResolution(p, _choiceSourceCard);
                _choiceSourceCard = null;
            }
            PendingChoice = ChoiceKind.None;
            ChoiceRemaining = 0;
            ChoiceSelection.Clear();
            if (Phase == GamePhase.AwaitingChoice) Phase = GamePhase.Play;
        }

        // =====================================================================
        // 効果解決
        // =====================================================================

        void ResolveEffect(PlayerState self, EffectDef e, CardInstance source, CardInstance triggeringCard)
        {
            if (e == null || Phase == GamePhase.RoundOver || Phase == GamePhase.MatchOver) return;
            if (_resolveDepth >= MaxResolveDepth)
            {
                AddLog("  (誘発が深すぎるため打ち切り)");
                return;
            }

            _resolveDepth++;
            try
            {
                var opp = Other(self);
                bool byAttackCard = source != null && source.Kind == CardKind.Attack;
                switch (e.Kind)
                {
                    case EffectKind.Damage:
                        DealDamage(opp, self, e.Amount, byAttackCard);
                        break;

                    case EffectKind.DamageDice:
                    {
                        int v = RollDice(self, e.DiceSides);
                        DealDamage(opp, self, v, byAttackCard);
                        break;
                    }

                    case EffectKind.Heal:
                    {
                        int max = MaxEnergyOf(self);
                        int before = self.Energy;
                        self.Energy = Math.Min(max, self.Energy + e.Amount);
                        AddLog($"  [{self.DisplayName}] 気力 +{self.Energy - before} (→ {self.Energy}/{max})");
                        break;
                    }

                    case EffectKind.DrawCards:
                        DrawCards(self, e.Amount, byAttackCard);
                        break;

                    case EffectKind.DrawCardsDice:
                    {
                        int v = Math.Max(0, RollDice(self, e.DiceSides) + e.Modifier);
                        DrawCards(self, v, byAttackCard);
                        break;
                    }

                    case EffectKind.DestroyOpponentField:
                        AutoDestroyFields(opp, e.Amount);
                        break;

                    case EffectKind.DestroyOpponentFieldDice:
                    {
                        int v = Math.Max(0, RollDice(self, e.DiceSides) + e.Modifier);
                        AutoDestroyFields(opp, v);
                        break;
                    }

                    case EffectKind.MillOpponentDeck:
                        MillDeck(opp, e.Amount);
                        break;

                    case EffectKind.DiscardOpponentHand:
                        DiscardRandomFromHand(opp, e.Amount);
                        break;

                    case EffectKind.ExtraTurn:
                        _extraTurn[self.Index] = true;
                        AddLog($"  [{self.DisplayName}] このターンのあとエクストラターンを得た");
                        break;

                    case EffectKind.DrawSameAsOpponent:
                        DrawCards(self, e.Amount);
                        break;

                    case EffectKind.BothPlayersDraw:
                        DrawCards(self, e.Amount);
                        DrawCards(opp, e.Amount);
                        break;

                    case EffectKind.DestroyTriggeringField:
                        if (triggeringCard != null) DestroyField(triggeringCard, wasOnField: false);
                        break;

                    case EffectKind.DamageBySacrificedHand:
                        // 選択フローで解決済み
                        break;
                }
            }
            finally
            {
                _resolveDepth--;
            }
        }

        // ---- 個別処理 ----

        void PayCost(PlayerState p, int cost)
        {
            if (cost <= 0) return;
            p.Energy -= cost;
            if (p.Energy <= 0)
            {
                p.Energy = 0;
                AddLog($"  [{p.DisplayName}] コストの使い過ぎで気力が0に… 自爆!");
                AwardDao(Other(p), $"{p.DisplayName} の自爆");
            }
        }

        public int MaxEnergyOf(PlayerState p)
        {
            int down = 0;
            var opp = Other(p);
            foreach (var f in opp.Fields)
                if (f.Def.Effect != null && f.Def.Effect.Kind == EffectKind.PassiveOpponentMaxEnergyDown)
                    down += f.Def.Effect.Amount;
            return Math.Max(1, BaseMaxEnergy - down);
        }

        public int DamageReductionOf(PlayerState p)
        {
            int r = 0;
            foreach (var f in p.Fields)
                if (f.Def.Effect != null && f.Def.Effect.Kind == EffectKind.PassiveDamageReduction)
                    r += f.Def.Effect.Amount;
            return r;
        }

        public int DiceBonusOf(PlayerState p)
        {
            int b = 0;
            foreach (var f in p.Fields)
                if (f.Def.Effect != null && f.Def.Effect.Kind == EffectKind.PassiveDiceBonus)
                    b += f.Def.Effect.Amount;
            return b;
        }

        void DealDamage(PlayerState target, PlayerState source, int amount, bool byCard)
        {
            if (Phase == GamePhase.RoundOver || Phase == GamePhase.MatchOver) return;
            if (amount <= 0) return;

            int reduction = DamageReductionOf(target);
            int actual = Math.Max(0, amount - reduction);
            if (reduction > 0)
                AddLog($"  [{target.DisplayName}] 軽減 -{Math.Min(reduction, amount)}");

            if (actual <= 0)
            {
                AddLog($"  [{target.DisplayName}] 気力は削られなかった");
                return;
            }

            target.Energy = Math.Max(0, target.Energy - actual);
            AddLog($"  [{target.DisplayName}] 気力 -{actual} (→ {target.Energy}/{MaxEnergyOf(target)})");

            if (target.Energy <= 0)
            {
                AwardDao(source, $"{target.DisplayName} の気力を削り切った");
                return;
            }

            FireTriggers(target, TriggerKind.OnDamaged, null);
            if (byCard) FireTriggers(target, TriggerKind.OnDamagedByCard, null);
        }

        /// <param name="fromAttackCard">
        /// 攻撃カードの効果によるドローか。ざきの《便乗》は
        /// 「相手が攻撃カードの効果でドローしたとき」だけ誘発するので、
        /// 番の始まりの通常ドローやフィールドの誘発ドローでは反応しない。
        /// </param>
        void DrawCards(PlayerState p, int count, bool fromAttackCard = false)
        {
            if (count <= 0) return;
            if (Phase == GamePhase.RoundOver || Phase == GamePhase.MatchOver) return;

            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (p.DrawPile.Count == 0)
                {
                    AddLog($"  [{p.DisplayName}] 山札が尽きた! (デッキアウト)");
                    AwardDao(Other(p), $"{p.DisplayName} のデッキアウト");
                    return;
                }
                MoveTop(p.DrawPile, p.Hand);
                drawn++;
            }

            AddLog($"  [{p.DisplayName}] {drawn} 枚ドロー (手札 {p.Hand.Count} / 山札 {p.DrawPile.Count})");

            // 「相手が攻撃カードの効果でドローしたとき、自分も同じ枚数引ける」
            if (fromAttackCard) FireOpponentDrawTriggers(Other(p), drawn);
        }

        void FireOpponentDrawTriggers(PlayerState watcher, int drawn)
        {
            if (drawn <= 0) return;
            if (_resolveDepth >= MaxResolveDepth) return;

            var snapshot = watcher.Fields.ToArray();
            foreach (var f in snapshot)
            {
                if (f.Def.Trigger != TriggerKind.OnOpponentDraw) continue;
                if (!watcher.Fields.Contains(f)) continue;
                AddLog($"  [{watcher.DisplayName}] 《{f.Name}》が発動");
                _resolveDepth++;
                try { DrawCards(watcher, drawn); }
                finally { _resolveDepth--; }
                if (Phase == GamePhase.RoundOver || Phase == GamePhase.MatchOver) return;
            }
        }

        void MillDeck(PlayerState target, int count)
        {
            int n = 0;
            for (int i = 0; i < count && target.DrawPile.Count > 0; i++)
            {
                var c = target.DrawPile[0];
                target.DrawPile.RemoveAt(0);
                target.Trash.Add(c);
                n++;
            }
            AddLog($"  [{target.DisplayName}] 山札を {n} 枚削られた (残り {target.DrawPile.Count})");
        }

        void DiscardRandomFromHand(PlayerState target, int count)
        {
            int n = 0;
            for (int i = 0; i < count && target.Hand.Count > 0; i++)
            {
                int idx = _rng.Next(target.Hand.Count);
                var c = target.Hand[idx];
                target.Hand.RemoveAt(idx);
                target.PendingReturn.Add(c);
                n++;
            }
            AddLog($"  [{target.DisplayName}] 手札を {n} 枚トラッシュされた (残り {target.Hand.Count})");
        }

        void AutoDestroyFields(PlayerState target, int count)
        {
            if (count <= 0)
            {
                AddLog("  破壊対象なし");
                return;
            }
            int n = 0;
            for (int i = 0; i < count && target.Fields.Count > 0; i++)
            {
                // 自動選択時は最もコストが高いフィールドを狙う
                int best = 0;
                for (int j = 1; j < target.Fields.Count; j++)
                    if (target.Fields[j].Cost > target.Fields[best].Cost) best = j;
                DestroyField(target.Fields[best], wasOnField: true);
                n++;
                if (Phase == GamePhase.RoundOver || Phase == GamePhase.MatchOver) return;
            }
            if (n == 0) AddLog("  破壊できるフィールドが無かった");
        }

        /// <summary>フィールドカードを破壊する。「このフィールドが破壊されたとき」を誘発させる。</summary>
        void DestroyField(CardInstance card, bool wasOnField)
        {
            var owner = Players[card.OwnerIndex];
            if (wasOnField) owner.Fields.Remove(card);
            AddLog($"  [{owner.DisplayName}] のフィールド《{card.Name}》が破壊された");
            owner.PendingReturn.Add(card);

            if (card.Def.Trigger == TriggerKind.OnThisDestroyed)
            {
                AddLog($"  《{card.Name}》の破壊時効果が発動");
                ResolveEffect(owner, card.Def.Effect, card, null);
            }
        }

        void MoveFieldToPending(PlayerState owner, CardInstance card, string reason)
        {
            owner.Fields.Remove(card);
            owner.PendingReturn.Add(card);
            AddLog($"  《{card.Name}》を{reason}");
        }

        void FireTriggers(PlayerState owner, TriggerKind kind, CardInstance triggeringCard)
        {
            if (Phase == GamePhase.RoundOver || Phase == GamePhase.MatchOver) return;

            var snapshot = owner.Fields.ToArray();
            foreach (var f in snapshot)
            {
                if (f.Def.Trigger != kind) continue;
                if (!owner.Fields.Contains(f)) continue; // 解決中に破壊された

                AddLog($"  [{owner.DisplayName}] 《{f.Name}》が発動");
                ResolveEffect(owner, f.Def.Effect, f, triggeringCard);

                if (f.Def.Effect != null && f.Def.Effect.TrashSelfAfter && owner.Fields.Contains(f))
                    MoveFieldToPending(owner, f, "トラッシュ");

                if (Phase == GamePhase.RoundOver || Phase == GamePhase.MatchOver) return;
            }
        }

        int RollDice(PlayerState roller, int sides)
        {
            if (sides <= 0) return 0;
            int raw = _rng.Next(1, sides + 1);
            int bonus = DiceBonusOf(roller);
            int final = raw + bonus;
            AddLog($"  ダイス D{sides} → {raw}" + (bonus > 0 ? $" (+{bonus} = {final})" : ""));
            OnDiceRolled?.Invoke(roller.Index, sides, raw, final);
            return final;
        }

        void AwardDao(PlayerState winner, string reason)
        {
            if (Phase == GamePhase.RoundOver || Phase == GamePhase.MatchOver) return;

            winner.Dao++;
            RoundWinnerIndex = winner.Index;
            AddLog($"★ {reason} → [{winner.DisplayName}] が ^^ を獲得! (^^ {winner.Dao}/{DaoToWin})");
            ResolveMatchEnd();
        }

        /// <summary>手番上限に達したラウンドを引き分けにする (ハウスルール)。</summary>
        void DeclareDrawRound()
        {
            if (Phase == GamePhase.RoundOver || Phase == GamePhase.MatchOver) return;

            LastRoundWasDraw = true;
            RoundWinnerIndex = -1;
            Players[0].Dao++;
            Players[1].Dao++;
            AddLog($"★ 手番上限 ({RoundTurnLimit}) に到達。このラウンドは引き分け、両者に ^^ が入る " +
                   $"(^^ {Players[0].Dao} - {Players[1].Dao})");
            ResolveMatchEnd();
        }

        void ResolveMatchEnd()
        {
            bool a = Players[0].Dao >= DaoToWin;
            bool b = Players[1].Dao >= DaoToWin;

            if (a || b)
            {
                if (a && b)
                    MatchWinnerIndex = Players[0].Dao == Players[1].Dao ? -1 : (Players[0].Dao > Players[1].Dao ? 0 : 1);
                else
                    MatchWinnerIndex = a ? 0 : 1;

                Phase = GamePhase.MatchOver;
                AddLog(MatchWinnerIndex < 0
                    ? "=== 引き分け ==="
                    : $"=== [{Players[MatchWinnerIndex].DisplayName}] の勝利! ===");
            }
            else
            {
                Phase = GamePhase.RoundOver;
            }

            PendingChoice = ChoiceKind.None;
            ChoiceSelection.Clear();
            _choiceSourceCard = null;
        }

        // =====================================================================
        // ユーティリティ
        // =====================================================================

        void Shuffle(List<CardInstance> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static void MoveTop(List<CardInstance> from, List<CardInstance> to)
        {
            var c = from[0];
            from.RemoveAt(0);
            to.Add(c);
        }

        public void AddLog(string msg)
        {
            Log.Add(msg);
            if (Log.Count > 400) Log.RemoveRange(0, 100);
            OnLogAppended?.Invoke(msg);
        }

        void Changed() => OnStateChanged?.Invoke();

        /// <summary>通信対戦用: 一意IDから場/手札のカードを探す。</summary>
        public CardInstance FindByUid(int uid)
        {
            for (int i = 0; i < 2; i++)
            {
                var p = Players[i];
                for (int j = 0; j < p.Hand.Count; j++) if (p.Hand[j].Uid == uid) return p.Hand[j];
                for (int j = 0; j < p.Fields.Count; j++) if (p.Fields[j].Uid == uid) return p.Fields[j];
            }
            return null;
        }

        /// <summary>投了。相手の勝ちでマッチを終了する。</summary>
        public void Surrender(int playerIndex)
        {
            if (Phase == GamePhase.MatchOver) return;

            var loser = Players[playerIndex];
            var winner = Players[1 - playerIndex];
            winner.Dao = DaoToWin;
            RoundWinnerIndex = winner.Index;
            MatchWinnerIndex = winner.Index;
            Phase = GamePhase.MatchOver;
            PendingChoice = ChoiceKind.None;
            ChoiceSelection.Clear();
            _choiceSourceCard = null;

            AddLog($"[{loser.DisplayName}] が投了");
            AddLog($"=== [{winner.DisplayName}] の勝利! ===");
            Changed();
        }

        /// <summary>UI 用: このカードは今クリックできるか。</summary>
        public bool IsSelectableHandCard(CardInstance card)
        {
            if (Phase == GamePhase.AwaitingChoice && PendingChoice == ChoiceKind.SacrificeHand)
                return Current.Hand.Contains(card);
            return CanPlay(card);
        }
    }
}
