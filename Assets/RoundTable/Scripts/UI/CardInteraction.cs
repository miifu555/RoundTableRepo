using System;
using RoundTable.Core;
using RoundTable.Data;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RoundTable.UI
{
    /// <summary>
    /// カードプレハブのクリック / ホバーを拾う。ルートに透明な Image (Raycast Target) が必要。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardInteraction : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [NonSerialized] public CardInstance Instance;
        [NonSerialized] public CardData Data;
        [NonSerialized] public Action<CardInteraction> Clicked;
        [NonSerialized] public Action<CardInteraction> Hovered;

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this);
        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);
    }
}
