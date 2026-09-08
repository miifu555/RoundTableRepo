using System;

namespace RoundTable.Net
{
    /// <summary>
    /// 通信対戦のやり取りは「操作ログ」だけ。
    /// ルールエンジンはシード固定で完全に決定論的なので、
    /// 同じ操作を同じ順で流せば両者の盤面は必ず一致する。
    /// </summary>
    public static class ActionCode
    {
        public const char PlayCard = 'P';        // P{uid}  手札からカードを出す
        public const char ChooseField = 'F';     // F{uid}  破壊するフィールドを選ぶ
        public const char ToggleHand = 'H';      // H{uid}  トラッシュする手札を選ぶ/外す
        public const char Confirm = 'C';         // C       選択を確定
        public const char EndTurn = 'E';         // E       ターン終了
        public const char Surrender = 'X';       // X       投了

        public static string Play(int uid) => PlayCard + uid.ToString();
        public static string Field(int uid) => ChooseField + uid.ToString();
        public static string Hand(int uid) => ToggleHand + uid.ToString();

        public static bool TryParse(string code, out char kind, out int uid)
        {
            kind = '\0';
            uid = 0;
            if (string.IsNullOrEmpty(code)) return false;
            kind = code[0];
            if (code.Length == 1) return true;
            return int.TryParse(code.Substring(1), out uid);
        }
    }

    /// <summary>プレイヤー1人分のファイル (rooms/{room}/p0.json など) の中身。</summary>
    [Serializable]
    public sealed class PlayerFile
    {
        /// <summary>プロトコル版。合わない相手とは接続しない。</summary>
        public int v = 1;
        /// <summary>0 = ホスト, 1 = ゲスト。</summary>
        public int role;
        /// <summary>1試合を識別するID。ホストが決めてゲストが同じ値を書き返す。</summary>
        public string matchId = "";
        /// <summary>このプレイヤーが選んだデッキ。</summary>
        public string deckId = "";
        /// <summary>シャッフルのシード。ホストだけが書く。</summary>
        public int seed;
        /// <summary>このプレイヤーが行った操作の全ログ。</summary>
        public string[] actions = Array.Empty<string>();
        /// <summary>デバッグ用のタイムスタンプ (UTC ticks)。</summary>
        public long updatedAt;

        public const int ProtocolVersion = 1;

        public bool IsReady => !string.IsNullOrEmpty(matchId) && !string.IsNullOrEmpty(deckId);
    }
}
