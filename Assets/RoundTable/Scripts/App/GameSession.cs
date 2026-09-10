using RoundTable.Data;
using RoundTable.Net;

namespace RoundTable.App
{
    public enum MatchMode
    {
        /// <summary>同じPCで2人が交互に操作する。</summary>
        HotSeat,
        /// <summary>簡易AIと対戦する。</summary>
        VsAi,
        /// <summary>GitHub 経由の通信対戦。</summary>
        Online,
    }

    /// <summary>
    /// シーンをまたいで持ち回す設定。Title → DeckSelect → Battle の順に埋まっていく。
    /// </summary>
    public static class GameSession
    {
        public static MatchMode Mode = MatchMode.HotSeat;

        /// <summary>プレイヤー0(先に定義される側)のデッキ。オンラインではホストのデッキ。</summary>
        public static DeckAsset Deck0;
        public static DeckAsset Deck1;

        /// <summary>自分がどちらのプレイヤーか。ホットシートでは常に0(両方操作できる)。</summary>
        public static int LocalPlayerIndex;

        /// <summary>シャッフルの乱数シード。オンラインでは両者で必ず一致させる。</summary>
        public static int Seed;

        /// <summary>CPU対戦の強さ (Mode == VsAi のときのみ使う)。</summary>
        public static AiDifficulty AiLevel = AiDifficulty.Normal;

        /// <summary>オンライン設定 (Mode == Online のときのみ使う)。</summary>
        public static OnlineConfig Online = new OnlineConfig();

        /// <summary>ホットシート時、手番でないプレイヤーの手札を隠すか。</summary>
        public static bool HideInactiveHand = true;

        public static void ResetToDefaults()
        {
            Mode = MatchMode.HotSeat;
            AiLevel = AiDifficulty.Normal;
            Deck0 = null;
            Deck1 = null;
            LocalPlayerIndex = 0;
            Seed = 0;
        }
    }
}
