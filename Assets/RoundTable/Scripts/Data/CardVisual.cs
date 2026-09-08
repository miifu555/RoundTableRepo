using RoundTable.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoundTable.Data
{
    public enum CardDisplayMode
    {
        /// <summary>手札・拡大表示用。効果テキストまで全部出す。</summary>
        Full,
        /// <summary>場のフィールドカード用。名前とコストとイラストだけ。</summary>
        Compact,
    }

    /// <summary>
    /// カードプレハブの見た目。子オブジェクトへの参照を持ち、CardData の内容を流し込む。
    /// レイアウトを変えたい場合はプレハブの階層を直接いじればよい。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardVisual : MonoBehaviour
    {
        [Header("ルート")]
        public RectTransform Root;
        public GameObject FullRoot;
        public GameObject CompactRoot;

        [Header("共通")]
        public Image Outline;
        public Image Dim;
        [Tooltip("カード全体差し替え画像 (CardData.FullCardImage) の表示先。")]
        public Image FullCardOverride;

        [Header("Full レイアウト")]
        public Image FullFrame;
        public TMP_Text FullCostText;
        public TMP_Text FullNameText;
        public Image FullArt;
        public Image FullArtPlaceholder;
        public TMP_Text FullArtPlaceholderText;
        public Image FullKindBand;
        public TMP_Text FullKindText;
        public TMP_Text FullBodyText;

        [Header("Compact レイアウト")]
        public Image CompactFrame;
        public TMP_Text CompactCostText;
        public TMP_Text CompactNameText;
        public Image CompactArt;
        public Image CompactArtPlaceholder;

        [Header("配色")]
        public Color AttackColor = new Color(0.788f, 0.310f, 0.286f, 1f);
        public Color FieldColor = new Color(0.298f, 0.510f, 0.686f, 1f);

        /// <summary>CardData の内容を見た目に反映する。</summary>
        public void Apply(CardData data)
        {
            if (data == null) return;

            bool isAttack = data.Kind == CardKind.Attack;
            Color accent = isAttack ? AttackColor : FieldColor;
            Color frameColor = accent * 0.55f + new Color(0.06f, 0.06f, 0.08f, 1f);
            frameColor.a = 1f;

            string kindLabel = isAttack
                ? "攻撃"
                : (data.Trigger == TriggerKind.None ? "フィールド（常在）" : "フィールド");

            // --- カード全体差し替え ---
            bool hasFull = data.FullCardImage != null;
            if (FullCardOverride != null)
            {
                FullCardOverride.gameObject.SetActive(hasFull);
                if (hasFull) FullCardOverride.sprite = data.FullCardImage;
            }

            // --- Full ---
            SetText(FullCostText, data.Cost.ToString());
            SetText(FullNameText, data.DisplayName);
            SetText(FullKindText, kindLabel);
            SetText(FullBodyText, data.EffectText);
            if (FullFrame != null) FullFrame.color = frameColor;
            if (FullKindBand != null) FullKindBand.color = accent;
            ApplyArt(FullArt, FullArtPlaceholder, data.Illustration);
            SetText(FullArtPlaceholderText,
                $"ILLUST\n{ArtSizes.CardArtW}×{ArtSizes.CardArtH}\n{data.CardId}.png");

            // --- Compact ---
            SetText(CompactCostText, data.Cost.ToString());
            SetText(CompactNameText, data.DisplayName);
            if (CompactFrame != null) CompactFrame.color = frameColor;
            ApplyArt(CompactArt, CompactArtPlaceholder, data.Illustration);
        }

        static void ApplyArt(Image art, Image placeholder, Sprite sprite)
        {
            bool has = sprite != null;
            if (art != null)
            {
                art.gameObject.SetActive(has);
                if (has) art.sprite = sprite;
            }
            if (placeholder != null) placeholder.gameObject.SetActive(!has);
        }

        static void SetText(TMP_Text t, string s)
        {
            if (t != null) t.text = s;
        }

        /// <summary>Full / Compact を切り替える。</summary>
        public void SetMode(CardDisplayMode mode)
        {
            bool hasOverride = FullCardOverride != null && FullCardOverride.gameObject.activeSelf;
            if (FullRoot != null) FullRoot.SetActive(!hasOverride && mode == CardDisplayMode.Full);
            if (CompactRoot != null) CompactRoot.SetActive(!hasOverride && mode == CardDisplayMode.Compact);
        }

        /// <summary>原寸 500x700 を指定の表示幅に縮小する。</summary>
        public void SetWidth(float width)
        {
            float s = width / ArtSizes.CardW;
            transform.localScale = new Vector3(s, s, 1f);
        }

        public void SetHighlight(bool on, Color color)
        {
            if (Outline == null) return;
            Outline.gameObject.SetActive(on);
            if (on) Outline.color = color;
        }

        public void SetDim(bool on)
        {
            if (Dim != null) Dim.gameObject.SetActive(on);
        }
    }
}
