using System;
using System.Collections;
using System.Collections.Generic;
using RoundTable.Core;
using RoundTable.Data;
using RoundTable.Net;
using UnityEngine;

namespace RoundTable.App
{
    public enum MatchStatus
    {
        Connecting,
        WaitingForOpponent,
        Playing,
        Error,
    }

    /// <summary>
    /// UI とルールエンジンの間に立って、ホットシートと通信対戦の差を吸収する。
    /// UI は必ずここ経由で操作すること (直接 RoundTableGame を触らない)。
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        public RoundTableGame Game { get; private set; }
        public MatchStatus Status { get; private set; } = MatchStatus.Connecting;
        public string StatusMessage { get; private set; } = "";
        public bool IsOnline => GameSession.Mode == MatchMode.Online;

        /// <summary>画面下側に表示するプレイヤー。ホットシートでは手番側、通信では自分。</summary>
        public int BottomPlayerIndex =>
            IsOnline ? GameSession.LocalPlayerIndex : (Game != null ? Game.CurrentIndex : 0);

        public int LocalPlayerIndex => IsOnline ? GameSession.LocalPlayerIndex : (Game != null ? Game.CurrentIndex : 0);

        /// <summary>いま自分が操作してよいか。</summary>
        public bool CanAct
        {
            get
            {
                if (Game == null || Status != MatchStatus.Playing) return false;
                if (Game.Phase != GamePhase.Play && Game.Phase != GamePhase.AwaitingChoice) return false;
                return !IsOnline || Game.CurrentIndex == GameSession.LocalPlayerIndex;
            }
        }

        public event Action OnChanged;

        // ---- 通信用 ----
        GitHubFileClient _client;
        readonly List<string> _localActions = new List<string>();
        readonly List<string> _remoteQueue = new List<string>();
        int _remoteApplied;
        string _localSha;
        string _matchId = "";
        bool _pushPending;
        bool _pushing;
        float _roundOverAt = -1f;

        void Start()
        {
            if (IsOnline) StartCoroutine(RunOnline());
            else StartLocal();
        }

        // =====================================================================
        // ホットシート
        // =====================================================================

        void StartLocal()
        {
            if (GameSession.Deck0 == null || GameSession.Deck1 == null)
            {
                Fail("デッキが選択されていません。タイトルからやり直してください。");
                return;
            }

            int seed = GameSession.Seed != 0 ? GameSession.Seed : UnityEngine.Random.Range(1, int.MaxValue);
            CreateGame(GameSession.Deck0, GameSession.Deck1, seed);
            Status = MatchStatus.Playing;
            StatusMessage = "";
            Changed();
        }

        void CreateGame(DeckAsset d0, DeckAsset d1, int seed)
        {
            Game = new RoundTableGame(d0.ToDefinition(), d1.ToDefinition(), true, seed);
            Game.OnStateChanged += Changed;
            Game.OnLogAppended += _ => Changed();
            Game.StartMatch();
        }

        // =====================================================================
        // 操作 (UI から呼ぶ)
        // =====================================================================

        public bool PlayCard(CardInstance card)
        {
            if (!CanAct || card == null) return false;
            if (!Game.CanPlay(card)) return false;
            return Submit(ActionCode.Play(card.Uid));
        }

        public bool ChooseFieldTarget(CardInstance card)
        {
            if (!CanAct || card == null) return false;
            return Submit(ActionCode.Field(card.Uid));
        }

        public bool ToggleHandSelection(CardInstance card)
        {
            if (!CanAct || card == null) return false;
            return Submit(ActionCode.Hand(card.Uid));
        }

        public bool ConfirmChoice()
        {
            if (!CanAct) return false;
            return Submit(ActionCode.Confirm.ToString());
        }

        public bool EndTurn()
        {
            if (!CanAct || Game.Phase != GamePhase.Play) return false;
            return Submit(ActionCode.EndTurn.ToString());
        }

        public bool Surrender()
        {
            if (Game == null || Status != MatchStatus.Playing) return false;
            return Submit(ActionCode.Surrender.ToString());
        }

        /// <summary>ラウンド終了後、次のラウンドへ。決定論的なので通信では送らず各自で進める。</summary>
        public void ProceedToNextRound()
        {
            if (Game == null || Game.Phase != GamePhase.RoundOver) return;
            Game.ProceedToNextRound();
            _roundOverAt = -1f;
            Changed();
        }

        bool Submit(string code)
        {
            if (!ApplyAction(code, LocalPlayerIndex)) return false;
            _localActions.Add(code);
            if (IsOnline) _pushPending = true;
            Changed();
            return true;
        }

        /// <summary>操作コードをエンジンに適用する。適用できなければ false。</summary>
        bool ApplyAction(string code, int actorIndex)
        {
            if (Game == null) return false;
            if (!ActionCode.TryParse(code, out char kind, out int uid)) return false;

            switch (kind)
            {
                case ActionCode.PlayCard:
                {
                    var c = Game.FindByUid(uid);
                    return c != null && Game.PlayCard(c);
                }
                case ActionCode.ChooseField:
                {
                    var c = Game.FindByUid(uid);
                    return c != null && Game.ChooseFieldTarget(c);
                }
                case ActionCode.ToggleHand:
                {
                    var c = Game.FindByUid(uid);
                    return c != null && Game.ToggleHandSelection(c);
                }
                case ActionCode.Confirm:
                    if (Game.Phase != GamePhase.AwaitingChoice) return false;
                    Game.ConfirmChoice();
                    return true;

                case ActionCode.EndTurn:
                    if (Game.Phase != GamePhase.Play) return false;
                    Game.EndTurn();
                    return true;

                case ActionCode.Surrender:
                    Game.Surrender(actorIndex);
                    return true;
            }
            return false;
        }

        // =====================================================================
        // 通信対戦
        // =====================================================================

        IEnumerator RunOnline()
        {
            var cfg = GameSession.Online;
            if (!cfg.IsComplete) { Fail("通信設定が未入力です。"); yield break; }

            _client = new GitHubFileClient(cfg);
            Status = MatchStatus.Connecting;
            StatusMessage = "接続中…";
            Changed();

            // --- 1. 自分のファイルを用意する ---
            var myFile = new PlayerFile
            {
                v = PlayerFile.ProtocolVersion,
                role = cfg.LocalRole,
                deckId = (cfg.IsHost ? GameSession.Deck0 : GameSession.Deck1)?.DeckId ?? "",
                actions = Array.Empty<string>(),
            };

            if (cfg.IsHost)
            {
                _matchId = DateTime.UtcNow.Ticks.ToString();
                myFile.matchId = _matchId;
                myFile.seed = UnityEngine.Random.Range(1, int.MaxValue);
                GameSession.Seed = myFile.seed;

                yield return Push(myFile);
                if (Status == MatchStatus.Error) yield break;

                StatusMessage = $"部屋「{cfg.RoomId}」で相手を待っています…";
                Status = MatchStatus.WaitingForOpponent;
                Changed();
            }

            // --- 2. 相手のファイルを待つ ---
            PlayerFile remote = null;
            while (true)
            {
                FileResult res = default;
                yield return _client.Get(cfg.RemotePath, r => res = r);

                if (!res.Ok) { Fail("相手の読み込みに失敗: " + res.Error); yield break; }

                if (!res.NotFound)
                {
                    var parsed = SafeParse(res.Text);
                    if (parsed != null && parsed.IsReady)
                    {
                        if (parsed.v != PlayerFile.ProtocolVersion)
                        {
                            Fail($"プロトコル版が違います (自分 {PlayerFile.ProtocolVersion} / 相手 {parsed.v})。両者を同じビルドにしてください。");
                            yield break;
                        }
                        if (cfg.IsHost)
                        {
                            // ゲストが同じ matchId を書き返してくるのを待つ
                            if (parsed.matchId == _matchId) { remote = parsed; break; }
                        }
                        else
                        {
                            remote = parsed;
                            break;
                        }
                    }
                }
                else if (!cfg.IsHost)
                {
                    StatusMessage = $"部屋「{cfg.RoomId}」のホストを待っています…";
                    Status = MatchStatus.WaitingForOpponent;
                    Changed();
                }

                yield return new WaitForSeconds(Mathf.Max(1f, cfg.PollIntervalSeconds));
            }

            // --- 3. ゲスト側は matchId とシードを受け取ってから書き込む ---
            if (!cfg.IsHost)
            {
                _matchId = remote.matchId;
                myFile.matchId = _matchId;
                myFile.seed = remote.seed;
                GameSession.Seed = remote.seed;

                yield return Push(myFile);
                if (Status == MatchStatus.Error) yield break;
            }

            // --- 4. デッキを確定してゲーム開始 ---
            var db = GameDatabase.Instance;
            var hostDeck = cfg.IsHost ? GameSession.Deck0 : db.GetDeck(remote.deckId);
            var guestDeck = cfg.IsHost ? db.GetDeck(remote.deckId) : GameSession.Deck1;

            if (hostDeck == null || guestDeck == null)
            {
                Fail("相手のデッキ ID を解決できませんでした: " + remote.deckId);
                yield break;
            }

            GameSession.Deck0 = hostDeck;
            GameSession.Deck1 = guestDeck;
            GameSession.LocalPlayerIndex = cfg.LocalRole;

            CreateGame(hostDeck, guestDeck, GameSession.Seed);
            Status = MatchStatus.Playing;
            StatusMessage = "";
            Changed();

            // --- 5. 送受信ループ ---
            StartCoroutine(PushLoop(myFile));
            StartCoroutine(PollLoop());
        }

        IEnumerator PushLoop(PlayerFile myFile)
        {
            var cfg = GameSession.Online;
            while (Status == MatchStatus.Playing)
            {
                if (_pushPending && !_pushing)
                {
                    _pushPending = false;
                    myFile.actions = _localActions.ToArray();
                    yield return Push(myFile);
                }
                yield return new WaitForSeconds(0.35f);
            }
        }

        IEnumerator Push(PlayerFile file)
        {
            var cfg = GameSession.Online;
            _pushing = true;
            file.updatedAt = DateTime.UtcNow.Ticks;

            FileResult res = default;
            if (_localSha == null)
            {
                // 既存ファイルの sha を先に取る (前回の対戦で残っている場合がある)
                FileResult probe = default;
                yield return _client.Get(cfg.LocalPath, r => probe = r);
                if (probe.Ok && !probe.NotFound) _localSha = probe.Sha;
            }

            yield return _client.Put(cfg.LocalPath, JsonUtility.ToJson(file, true), _localSha,
                $"RoundTable p{cfg.LocalRole} {file.actions.Length} actions", r => res = r);

            _pushing = false;

            if (!res.Ok) { Fail("送信に失敗: " + res.Error); yield break; }
            if (!string.IsNullOrEmpty(res.Sha)) _localSha = res.Sha;
            else _localSha = null; // 次回 Get で取り直す
        }

        IEnumerator PollLoop()
        {
            var cfg = GameSession.Online;
            while (Status == MatchStatus.Playing)
            {
                yield return new WaitForSeconds(Mathf.Max(1f, cfg.PollIntervalSeconds));

                FileResult res = default;
                yield return _client.Get(cfg.RemotePath, r => res = r);
                if (!res.Ok || res.NotFound) continue;

                var parsed = SafeParse(res.Text);
                if (parsed == null || parsed.matchId != _matchId) continue;

                for (int i = _remoteApplied; i < parsed.actions.Length; i++)
                    _remoteQueue.Add(parsed.actions[i]);
                _remoteApplied = parsed.actions.Length;
            }
        }

        void Update()
        {
            // 相手の操作は「適用できるようになった順」に流し込む
            while (_remoteQueue.Count > 0)
            {
                if (Game == null) break;
                if (Game.Phase == GamePhase.RoundOver) break; // ラウンド送りが済むまで待つ
                if (!ApplyAction(_remoteQueue[0], 1 - LocalPlayerIndex)) break;
                _remoteQueue.RemoveAt(0);
                Changed();
            }

            // ラウンド終了は決定論的なので各自で進める
            if (Game != null && Game.Phase == GamePhase.RoundOver)
            {
                if (_roundOverAt < 0f) _roundOverAt = Time.time;
                else if (IsOnline && Time.time - _roundOverAt > 6f) ProceedToNextRound();
            }
            else
            {
                _roundOverAt = -1f;
            }
        }

        static PlayerFile SafeParse(string json)
        {
            try { return JsonUtility.FromJson<PlayerFile>(json); }
            catch { return null; }
        }

        void Fail(string message)
        {
            Status = MatchStatus.Error;
            StatusMessage = message;
            Debug.LogError("[RoundTable] " + message);
            Changed();
        }

        void Changed() => OnChanged?.Invoke();
    }
}
