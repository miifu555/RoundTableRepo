using RoundTable.Core;
using RoundTable.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoundTable.UI
{
    /// <summary>対戦画面の上下にあるプレイヤー情報バー。</summary>
    public sealed class PlayerInfoPanel : MonoBehaviour
    {
        [Header("キャラ")]
        public Image Portrait;
        public GameObject PortraitPlaceholder;
        public TMP_Text NameText;
        public TMP_Text ArchetypeText;

        [Header("気力")]
        public TMP_Text EnergyText;
        [Tooltip("Image Type = Filled / Horizontal にしておくこと。")]
        public Image EnergyBar;

        [Header("^^ (だお)")]
        public Image[] DaoIcons;

        [Header("枚数")]
        public TMP_Text CountsText;

        [Header("配色")]
        public Color EnergyColor = new Color(0.373f, 0.749f, 0.639f, 1f);
        public Color DangerColor = new Color(0.855f, 0.353f, 0.290f, 1f);
        public Color DaoOnColor = new Color(0.847f, 0.706f, 0.376f, 1f);
        public Color DaoOffColor = new Color(0.28f, 0.26f, 0.32f, 1f);

        public void Bind(RoundTableGame game, PlayerState p, DeckAsset deck, string roleLabel)
        {
            if (p == null) return;

            var sprite = deck != null ? deck.IconOrPortrait : null;
            if (Portrait != null)
            {
                Portrait.gameObject.SetActive(sprite != null);
                if (sprite != null) Portrait.sprite = sprite;
            }
            if (PortraitPlaceholder != null) PortraitPlaceholder.SetActive(sprite == null);

            if (NameText != null)
                NameText.text = string.IsNullOrEmpty(roleLabel) ? p.DisplayName : roleLabel + "  " + p.DisplayName;

            if (ArchetypeText != null && deck != null)
                ArchetypeText.text = $"{deck.Archetype} / {deck.DrawStyleLabel}";

            int max = game.MaxEnergyOf(p);
            bool low = p.Energy <= 3;

            if (EnergyText != null)
            {
                EnergyText.text = $"気力  <b>{p.Energy}</b> / {max}";
                EnergyText.color = low ? DangerColor : EnergyColor;
            }
            if (EnergyBar != null)
            {
                EnergyBar.fillAmount = max > 0 ? Mathf.Clamp01(p.Energy / (float)max) : 0f;
                EnergyBar.color = low ? DangerColor : EnergyColor;
            }

            if (DaoIcons != null)
            {
                var db = GameDatabase.Instance;
                for (int i = 0; i < DaoIcons.Length; i++)
                {
                    if (DaoIcons[i] == null) continue;
                    bool got = i < p.Dao;
                    var s = db != null ? (got ? db.DaoOn : db.DaoOff) : null;
                    if (s != null)
                    {
                        // 差し替え画像がある場合だけ入れ替える。
                        // null を代入するとプレハブの丸スプライトが外れて四角くなるので触らない。
                        DaoIcons[i].sprite = s;
                        DaoIcons[i].color = Color.white;
                    }
                    else
                    {
                        DaoIcons[i].color = got ? DaoOnColor : DaoOffColor;
                    }
                }
            }

            if (CountsText != null)
                CountsText.text = $"山札　　{p.DrawPile.Count}\n手札　　{p.Hand.Count}\nトラッシュ {p.PendingReturn.Count}\n削られた　{p.Trash.Count}";
        }
    }
}
