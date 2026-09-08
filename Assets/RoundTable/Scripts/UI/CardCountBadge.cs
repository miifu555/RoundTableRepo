using TMPro;
using UnityEngine;

namespace RoundTable.UI
{
    /// <summary>デッキ一覧で「×3」のように投入枚数を出すバッジ。普段は非表示。</summary>
    public sealed class CardCountBadge : MonoBehaviour
    {
        public GameObject Root;
        public TMP_Text Label;

        public void Show(int count)
        {
            if (Root != null) Root.SetActive(true);
            if (Label != null) Label.text = "×" + count;
        }

        public void Hide()
        {
            if (Root != null) Root.SetActive(false);
        }
    }
}
