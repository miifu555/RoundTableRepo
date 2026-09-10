using System.Collections.Generic;
using System.Text;
using RoundTable.Core;
using RoundTable.Data;
using UnityEditor;
using UnityEngine;

namespace RoundTable.EditorTools
{
    /// <summary>
    /// デッキアセットの編集画面。カードの「種類」と「枚数」をここで管理する。
    /// 合計が15枚かどうかを常に表示し、ズレていれば警告する。
    /// </summary>
    [CustomEditor(typeof(DeckAsset))]
    public sealed class DeckAssetEditor : Editor
    {
        SerializedProperty _deckId, _characterName, _archetype, _drawStyle, _portrait, _portraitIcon, _entries;

        static readonly Color AttackTint = new Color(0.95f, 0.62f, 0.60f);
        static readonly Color FieldTint = new Color(0.60f, 0.78f, 0.95f);

        void OnEnable()
        {
            _deckId = serializedObject.FindProperty(nameof(DeckAsset.DeckId));
            _characterName = serializedObject.FindProperty(nameof(DeckAsset.CharacterName));
            _archetype = serializedObject.FindProperty(nameof(DeckAsset.Archetype));
            _drawStyle = serializedObject.FindProperty(nameof(DeckAsset.DrawStyle));
            _portrait = serializedObject.FindProperty(nameof(DeckAsset.Portrait));
            _portraitIcon = serializedObject.FindProperty(nameof(DeckAsset.PortraitIcon));
            _entries = serializedObject.FindProperty(nameof(DeckAsset.Entries));
        }

        public override void OnInspectorGUI()
        {
            var deck = (DeckAsset)target;
            serializedObject.Update();

            DrawHeaderFields();
            EditorGUILayout.Space(6);
            DrawSummary(deck);
            EditorGUILayout.Space(4);
            DrawEntries(deck);
            EditorGUILayout.Space(6);
            DrawAddArea(deck);
            EditorGUILayout.Space(6);
            DrawTools(deck);

            serializedObject.ApplyModifiedProperties();
        }

        // =====================================================================

        void DrawHeaderFields()
        {
            EditorGUILayout.LabelField("キャラクター", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_deckId, new GUIContent("デッキID"));
            EditorGUILayout.PropertyField(_characterName, new GUIContent("名前"));
            EditorGUILayout.PropertyField(_archetype, new GUIContent("アーキタイプ"));
            EditorGUILayout.PropertyField(_drawStyle, new GUIContent("引き方"));
            EditorGUILayout.PropertyField(_portrait, new GUIContent($"立ち絵 ({ArtSizes.PortraitW}×{ArtSizes.PortraitH})"));
            EditorGUILayout.PropertyField(_portraitIcon, new GUIContent($"顔アイコン ({ArtSizes.PortraitIconW}×{ArtSizes.PortraitIconH})"));
        }

        void DrawSummary(DeckAsset deck)
        {
            int total = deck.TotalCards;
            int required = DeckAsset.RequiredCardCount;
            bool ok = total == required;

            var style = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
            };

            string color = ok ? "#7ac0a0" : "#db5a4a";
            string mark = ok ? "OK" : (total < required ? $"あと {required - total} 枚" : $"{total - required} 枚多い");

            EditorGUILayout.LabelField(
                $"<b><color={color}>合計 {total} / {required} 枚　（{mark}）</color></b>　　種類 {deck.UniqueCards}",
                style, GUILayout.Height(24));

            // 攻撃 / フィールドの内訳
            int attack = 0, field = 0;
            foreach (var e in deck.ValidEntries)
            {
                if (e.Card.Kind == CardKind.Attack) attack += e.Count;
                else field += e.Count;
            }
            EditorGUILayout.LabelField($"　内訳:  攻撃 {attack} 枚  /  フィールド {field} 枚", EditorStyles.miniLabel);
        }

        void DrawEntries(DeckAsset deck)
        {
            EditorGUILayout.LabelField("カード構成", EditorStyles.boldLabel);

            // 見出し
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("カード", EditorStyles.miniBoldLabel, GUILayout.Width(160));
                EditorGUILayout.LabelField("種類 / コスト", EditorStyles.miniBoldLabel, GUILayout.Width(96));
                EditorGUILayout.LabelField("効果", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("枚数", EditorStyles.miniBoldLabel, GUILayout.Width(96));
                EditorGUILayout.LabelField("", GUILayout.Width(66));
            }

            int removeAt = -1, moveFrom = -1, moveTo = -1;

            for (int i = 0; i < _entries.arraySize; i++)
            {
                var element = _entries.GetArrayElementAtIndex(i);
                var cardProp = element.FindPropertyRelative(nameof(DeckEntry.Card));
                var countProp = element.FindPropertyRelative(nameof(DeckEntry.Count));
                var card = cardProp.objectReferenceValue as CardData;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.PropertyField(cardProp, GUIContent.none, GUILayout.Width(160));

                    if (card != null)
                    {
                        var tint = card.Kind == CardKind.Attack ? AttackTint : FieldTint;
                        var kindStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = tint } };
                        EditorGUILayout.LabelField(
                            $"{(card.Kind == CardKind.Attack ? "攻撃" : "フィールド")}  コスト{card.Cost}",
                            kindStyle, GUILayout.Width(96));
                        EditorGUILayout.LabelField(new GUIContent(card.EffectText, card.EffectText), EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("（カード未設定）", EditorStyles.miniLabel);
                    }

                    if (GUILayout.Button("−", EditorStyles.miniButtonLeft, GUILayout.Width(24)))
                        countProp.intValue = Mathf.Max(0, countProp.intValue - 1);

                    countProp.intValue = Mathf.Max(0,
                        EditorGUILayout.IntField(countProp.intValue, GUILayout.Width(40)));

                    if (GUILayout.Button("＋", EditorStyles.miniButtonRight, GUILayout.Width(24)))
                        countProp.intValue++;

                    // 仕様書と枚数が違う行に印を付ける
                    var specProp = element.FindPropertyRelative(nameof(DeckEntry.SpecCount));
                    bool customized = specProp.intValue > 0 && specProp.intValue != countProp.intValue;
                    EditorGUILayout.LabelField(
                        customized ? new GUIContent("*", $"仕様書では {specProp.intValue} 枚") : GUIContent.none,
                        EditorStyles.miniBoldLabel, GUILayout.Width(10));

                    using (new EditorGUI.DisabledScope(i == 0))
                        if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(20)))
                        { moveFrom = i; moveTo = i - 1; }

                    using (new EditorGUI.DisabledScope(i == _entries.arraySize - 1))
                        if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(20)))
                        { moveFrom = i; moveTo = i + 1; }

                    if (GUILayout.Button("×", EditorStyles.miniButtonRight, GUILayout.Width(22)))
                        removeAt = i;
                }
            }

            if (moveFrom >= 0) _entries.MoveArrayElement(moveFrom, moveTo);
            if (removeAt >= 0) _entries.DeleteArrayElementAtIndex(removeAt);

            if (_entries.arraySize == 0)
                EditorGUILayout.HelpBox("カードが1枚も入っていません。下の枠にカードのプレハブをドロップしてください。", MessageType.Warning);
        }

        void DrawAddArea(DeckAsset deck)
        {
            var rect = GUILayoutUtility.GetRect(0, 46, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "ここにカードのプレハブをドロップして追加", EditorStyles.helpBox);

            var evt = Event.current;
            if (!rect.Contains(evt.mousePosition)) return;

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                bool any = false;
                foreach (var obj in DragAndDrop.objectReferences)
                    if (ExtractCard(obj) != null) { any = true; break; }

                DragAndDrop.visualMode = any ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

                if (evt.type == EventType.DragPerform && any)
                {
                    DragAndDrop.AcceptDrag();
                    Undo.RecordObject(deck, "カードを追加");
                    serializedObject.ApplyModifiedProperties();

                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        var card = ExtractCard(obj);
                        if (card != null) deck.Add(card);
                    }

                    EditorUtility.SetDirty(deck);
                    serializedObject.Update();
                }
                evt.Use();
            }
        }

        static CardData ExtractCard(Object obj)
        {
            if (obj is CardData cd) return cd;
            if (obj is GameObject go) return go.GetComponent<CardData>();
            return null;
        }

        void DrawTools(DeckAsset deck)
        {
            EditorGUILayout.LabelField("ツール", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("このデッキのフォルダから全カードを追加"))
                    AddAllFromFolder(deck);

                if (GUILayout.Button("枚数を仕様書の値に戻す"))
                    ResetCountsToSpec(deck);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("枚数0の行を削除"))
                {
                    Undo.RecordObject(deck, "枚数0の行を削除");
                    serializedObject.ApplyModifiedProperties();
                    deck.Entries.RemoveAll(e => e == null || e.Card == null || e.Count <= 0);
                    EditorUtility.SetDirty(deck);
                    serializedObject.Update();
                }

                if (GUILayout.Button("構成をコンソールに出力"))
                    DumpToConsole(deck);
            }
        }

        void AddAllFromFolder(DeckAsset deck)
        {
            string folder = $"{RoundTableBuilder.CardsRoot}/{deck.DeckId}";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[RoundTable] フォルダが見つかりません: {folder}");
                return;
            }

            Undo.RecordObject(deck, "全カードを追加");
            serializedObject.ApplyModifiedProperties();

            int added = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var card = go != null ? go.GetComponent<CardData>() : null;
                if (card == null) continue;
                if (deck.CountOf(card) > 0) continue;

                bool alreadyListed = false;
                foreach (var e in deck.Entries)
                    if (e != null && e.Card == card) { alreadyListed = true; break; }
                if (alreadyListed) continue;

                deck.Entries.Add(new DeckEntry(card, 1));
                added++;
            }

            EditorUtility.SetDirty(deck);
            serializedObject.Update();
            Debug.Log($"[RoundTable] {deck.CharacterName}: {added} 種類を追加しました。");
        }

        void ResetCountsToSpec(DeckAsset deck)
        {
            var seed = CardSeedData.Get(deck.DeckId);
            if (seed == null)
            {
                Debug.LogWarning($"[RoundTable] 仕様書データに {deck.DeckId} がありません。");
                return;
            }

            var counts = new Dictionary<string, int>();
            foreach (var c in seed.Cards) counts[c.Id] = c.Count;

            Undo.RecordObject(deck, "枚数を仕様書の値に戻す");
            serializedObject.ApplyModifiedProperties();

            int changed = 0;
            foreach (var e in deck.Entries)
            {
                if (e == null || e.Card == null) continue;
                if (!counts.TryGetValue(e.Card.CardId, out int c)) continue;
                if (e.Count == c) continue;
                e.Count = c;
                changed++;
            }

            EditorUtility.SetDirty(deck);
            serializedObject.Update();
            Debug.Log($"[RoundTable] {deck.CharacterName}: {changed} 種類の枚数を戻しました（合計 {deck.TotalCards} 枚）。");
        }

        static void DumpToConsole(DeckAsset deck)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[RoundTable] {deck.CharacterName}（{deck.Archetype} / {deck.DrawStyleLabel}）合計 {deck.TotalCards} 枚");
            foreach (var e in deck.ValidEntries)
                sb.AppendLine($"  ×{e.Count}  {e.Card.DisplayName}  [{(e.Card.Kind == CardKind.Attack ? "攻撃" : "フィールド")} コスト{e.Card.Cost}]  {e.Card.EffectText}");
            Debug.Log(sb.ToString());
        }

        // =====================================================================
        // 全デッキの検証
        // =====================================================================

        [MenuItem("Round Table/デッキ構成を検証", priority = 22)]
        public static void ValidateAllDecks()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(RoundTableBuilder.DatabasePath);
            if (db == null) { Debug.LogError("[RoundTable] RoundTableDatabase が見つかりません。"); return; }

            var sb = new StringBuilder("[RoundTable] デッキ構成の検証\n");
            int ng = 0;

            foreach (var deck in db.Decks)
            {
                if (deck == null) continue;

                int total = deck.TotalCards;
                bool ok = total == DeckAsset.RequiredCardCount;
                if (!ok) ng++;

                sb.Append(ok ? "  OK  " : "  NG  ")
                  .Append(deck.CharacterName.PadRight(8))
                  .Append($"合計 {total} 枚 / 種類 {deck.UniqueCards}");

                var problems = new List<string>();
                foreach (var e in deck.Entries)
                {
                    if (e == null || e.Card == null) { problems.Add("カード未設定の行がある"); continue; }
                    if (e.Count <= 0) problems.Add($"{e.Card.DisplayName} が0枚");
                    if (e.Card.DeckId != deck.DeckId) problems.Add($"{e.Card.DisplayName} は別デッキ({e.Card.DeckId})のカード");
                }
                if (problems.Count > 0) sb.Append("  ← ").Append(string.Join(" / ", problems));
                sb.AppendLine();
            }

            sb.Append(ng == 0 ? "すべて15枚です。" : $"{ng} デッキが15枚になっていません。");
            if (ng == 0) Debug.Log(sb.ToString());
            else Debug.LogWarning(sb.ToString());
        }
    }
}
