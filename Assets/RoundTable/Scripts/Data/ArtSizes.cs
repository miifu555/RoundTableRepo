namespace RoundTable.Data
{
    /// <summary>
    /// 差し替え用アートの寸法定義。UI とプレハブ生成はすべてこの値から組み立てられる。
    /// (Assets/RoundTable/Docs/ArtSpec.md と同じ内容)
    /// </summary>
    public static class ArtSizes
    {
        // ---- 画面 ----
        public const int ReferenceWidth = 1920;
        public const int ReferenceHeight = 1080;

        /// <summary>背景画像 (円卓 / タイトル)。16:9。</summary>
        public const int BackgroundW = 1920;
        public const int BackgroundH = 1080;

        // ---- カード ----
        /// <summary>カードプレハブの原寸 (ポーカーサイズ 5:7)。表示時はここから縮小される。</summary>
        public const int CardW = 500;
        public const int CardH = 700;

        /// <summary>カード枠内のイラスト窓 (4:3)。位置はカード左上原点。</summary>
        public const int CardArtX = 30;
        public const int CardArtY = 140;
        public const int CardArtW = 440;
        public const int CardArtH = 330;

        /// <summary>カード裏面。</summary>
        public const int CardBackW = CardW;
        public const int CardBackH = CardH;

        // ---- 画面上の表示幅 ----
        public const int HandCardW = 190;   // 自分の手札
        public const int FieldCardW = 132;  // 場のフィールドカード (Compact レイアウト)
        public const int OppHandCardW = 62; // 相手の手札 (裏向き)
        public const int ZoomCardW = 430;   // 拡大表示

        public static int HeightFor(int width)
            => UnityEngine.Mathf.RoundToInt(width * (float)CardH / CardW);

        // ---- キャラクター ----
        public const int PortraitW = 512;
        public const int PortraitH = 768;
        public const int PortraitIconW = 256;
        public const int PortraitIconH = 256;

        // ---- アイコン ----
        public const int IconEnergy = 64;
        public const int IconDao = 96;
        public const int IconKind = 48;
        public const int IconDice = 128;
    }
}
