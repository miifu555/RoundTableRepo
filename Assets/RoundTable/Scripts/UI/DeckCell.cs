using System;
using RoundTable.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoundTable.UI
{
    /// <summary>デッキ選択画面のキャラ1人分のセル。Prefabs/Ui/DeckCell.prefab</summary>
    public sealed class DeckCell : MonoBehaviour, IPointerClickHandler
    {
        public Image Frame;
        public Image Portrait;
        public GameObject PortraitPlaceholder;
        public TMP_Text PortraitPlaceholderText;
        public TMP_Text NameText;
        public TMP_Text ArchetypeText;
        public TMP_Text DrawStyleText;
        public TMP_Text TagText;

        [Header("配色")]
        public Color NormalFrame = new Color(0.25f, 0.23f, 0.30f, 1f);
        public Color MineFrame = new Color(0.847f, 0.706f, 0.376f, 1f);
        public Color OpponentFrame = new Color(0.855f, 0.353f, 0.290f, 1f);
        public Color DrawTypeColor = new Color(0.298f, 0.510f, 0.686f, 1f);
        public Color ShuffleTypeColor = new Color(0.788f, 0.310f, 0.286f, 1f);

        public DeckAsset Deck { get; private set; }
        Action<DeckCell> _onClick;

        public void Bind(DeckAsset deck, Action<DeckCell> onClick)
        {
            Deck = deck;
            _onClick = onClick;

            if (NameText != null) NameText.text = deck.CharacterName;
            if (ArchetypeText != null) ArchetypeText.text = deck.Archetype;
            if (DrawStyleText != null)
            {
                DrawStyleText.text = deck.DrawStyleLabel;
                DrawStyleText.color = deck.DrawStyle == Core.DrawStyle.Draw ? DrawTypeColor : ShuffleTypeColor;
            }

            bool hasPortrait = deck.Portrait != null;
            if (Portrait != null)
            {
                Portrait.gameObject.SetActive(hasPortrait);
                if (hasPortrait) Portrait.sprite = deck.Portrait;
            }
            if (PortraitPlaceholder != null) PortraitPlaceholder.SetActive(!hasPortrait);
            if (PortraitPlaceholderText != null)
                PortraitPlaceholderText.text = $"立ち絵\n{ArtSizes.PortraitW}×{ArtSizes.PortraitH}\n\n{deck.DeckId}";

            SetSelection(SelectionState.None);
        }

        public enum SelectionState { None, Mine, Opponent }

        public void SetSelection(SelectionState state)
        {
            if (Frame != null)
                Frame.color = state == SelectionState.Mine ? MineFrame
                            : state == SelectionState.Opponent ? OpponentFrame
                            : NormalFrame;

            if (TagText != null)
            {
                TagText.gameObject.SetActive(state != SelectionState.None);
                if (state == SelectionState.Mine) { TagText.text = "YOU"; TagText.color = MineFrame; }
                else if (state == SelectionState.Opponent) { TagText.text = "VS"; TagText.color = OpponentFrame; }
            }
        }

        public void OnPointerClick(PointerEventData eventData) => _onClick?.Invoke(this);
    }
}
