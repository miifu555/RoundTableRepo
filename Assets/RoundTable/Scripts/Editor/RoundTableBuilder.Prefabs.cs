using System.Collections.Generic;
using System.IO;
using RoundTable.Core;
using RoundTable.Data;
using RoundTable.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RoundTable.EditorTools
{
    /// <summary>
    /// カードのプレハブ・デッキアセット・シーンを一括生成するエディタツール。
    /// 「Round Table / すべて再生成」で走る。
    /// 生成後の調整はプレハブとシーンを直接編集すればよく、このツールを再実行する必要はない
    /// (再実行するとカードプレハブの効果と絵の割り当ては維持したまま、レイアウトだけ作り直す)。
    /// </summary>
    public static partial class RoundTableBuilder
    {
        public const string Root = "Assets/RoundTable";
        public const string CardsRoot = Root + "/Prefabs/Cards";
        public const string CardBasePath = CardsRoot + "/_Base/Card.prefab";
        public const string UiPrefabRoot = Root + "/Prefabs/Ui";
        public const string DecksRoot = Root + "/Data/Decks";
        public const string ResourcesRoot = Root + "/Resources";
        public const string DatabasePath = ResourcesRoot + "/RoundTableDatabase.asset";
        public const string ScenesRoot = Root + "/Scenes";

        const string FontPath = "Assets/SU3DJPFont/TextMeshProFont/Dynamic/mplus-1p-medium SDF Dynamic.asset";
        const string FontBoldPath = "Assets/SU3DJPFont/TextMeshProFont/Dynamic/mplus-1p-bold SDF Dynamic.asset";

        [MenuItem("Round Table/すべて再生成", priority = 0)]
        public static void RebuildAll()
        {
            if (!EditorUtility.DisplayDialog("Round Table",
                    "カードプレハブ・デッキアセット・UIシーンを再生成します。\n" +
                    "カードに割り当て済みの絵と、Inspector で変更した効果は引き継がれます。\n" +
                    "ただしプレハブとシーンのレイアウト変更は失われます。",
                    "再生成する", "やめる"))
                return;

            Rebuild();
        }

        /// <summary>確認ダイアログなしの再生成 (自動化用)。</summary>
        public static void Rebuild()
        {
            LoadFont();

            EnsureFolders();
            var basePrefab = BuildCardBasePrefab();
            var cardsByDeck = BuildCardVariants(basePrefab);
            BuildCardBackPrefab();
            BuildDeckCellPrefab();
            var decks = BuildDeckAssets(cardsByDeck);
            var db = BuildDatabase(decks);
            BuildScenes(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RoundTable] 生成完了: カード {CountCards(cardsByDeck)} 枚 / デッキ {decks.Count} / シーン 3");
        }

        static int CountCards(Dictionary<string, List<CardData>> map)
        {
            int n = 0;
            foreach (var kv in map) n += kv.Value.Count;
            return n;
        }

        static void LoadFont()
        {
            UiBuilder.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (UiBuilder.Font == null)
                Debug.LogWarning($"[RoundTable] フォントが見つかりません: {FontPath}");
        }

        static void EnsureFolders()
        {
            foreach (var d in new[]
                     {
                         Root + "/Prefabs", CardsRoot, CardsRoot + "/_Base", UiPrefabRoot,
                         Root + "/Data", DecksRoot, ResourcesRoot, ScenesRoot, UiBuilder.SpriteFolder
                     })
                Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
        }

        // =====================================================================
        // カードのベースプレハブ
        // =====================================================================

        static GameObject BuildCardBasePrefab()
        {
            const float W = ArtSizes.CardW;
            const float H = ArtSizes.CardH;

            var root = UiBuilder.Node("Card", null);
            UiBuilder.Place(root, 0, 0, W, H);
            UiBuilder.HitArea(root.gameObject);

            var data = root.gameObject.AddComponent<CardData>();
            var visual = root.gameObject.AddComponent<CardVisual>();
            root.gameObject.AddComponent<CardInteraction>();
            var badge = root.gameObject.AddComponent<CardCountBadge>();

            // 選択枠
            var outline = UiBuilder.Panel3(root, "Outline", -6, -6, W + 12, H + 12, UiBuilder.Gold, true);
            outline.gameObject.SetActive(false);

            // カード全体差し替え
            var fullOverride = UiBuilder.Picture(root, "FullCardImage", 0, 0, W, H);
            fullOverride.gameObject.SetActive(false);

            // ---- Full レイアウト ----
            var full = UiBuilder.Node("Full", root);
            UiBuilder.Place(full, 0, 0, W, H);

            var frame = UiBuilder.Panel3(full, "Frame", 0, 0, W, H, UiBuilder.AttackColor, true);
            UiBuilder.Panel3(full, "Inner", 6, 6, W - 12, H - 12, UiBuilder.CardInner, true);

            var cost = UiBuilder.CircleImage(full, "Cost", 18, 18, 84, UiBuilder.EnergyColor);
            var costText = UiBuilder.Text(cost.transform, "CostText", 0, 0, 84, 84, "0", 48,
                new Color(0.06f, 0.09f, 0.09f, 1f), TextAlignmentOptions.Center, true);
            UiBuilder.Stretch(costText.rectTransform);

            var nameText = UiBuilder.Text(full, "Name", 112, 20, W - 130, 84, "カード名", 34,
                UiBuilder.TextMain, TextAlignmentOptions.Left, true);

            var art = UiBuilder.Picture(full, "Art", ArtSizes.CardArtX, ArtSizes.CardArtY, ArtSizes.CardArtW, ArtSizes.CardArtH);
            art.gameObject.SetActive(false);

            var artPh = UiBuilder.Panel3(full, "ArtPlaceholder", ArtSizes.CardArtX, ArtSizes.CardArtY,
                ArtSizes.CardArtW, ArtSizes.CardArtH, UiBuilder.Placeholder);
            var artPhText = UiBuilder.Text(artPh.transform, "Text", 0, 0, ArtSizes.CardArtW, ArtSizes.CardArtH,
                $"ILLUST\n{ArtSizes.CardArtW}×{ArtSizes.CardArtH}", 26, UiBuilder.PlaceholderText, TextAlignmentOptions.Center);
            UiBuilder.Stretch(artPhText.rectTransform);

            var band = UiBuilder.Panel3(full, "KindBand", 30, 482, 440, 44, UiBuilder.AttackColor);
            var bandText = UiBuilder.Text(band.transform, "Text", 0, 0, 440, 44, "攻撃", 26,
                Color.white, TextAlignmentOptions.Center, true);
            UiBuilder.Stretch(bandText.rectTransform);

            var body = UiBuilder.Text(full, "Body", 34, 538, 432, 140, "効果テキスト", 25,
                UiBuilder.TextMain, TextAlignmentOptions.TopLeft, false, true);

            // ---- Compact レイアウト (場のフィールドカード用: 小さくても読める) ----
            var compact = UiBuilder.Node("Compact", root);
            UiBuilder.Place(compact, 0, 0, W, H);

            var cFrame = UiBuilder.Panel3(compact, "Frame", 0, 0, W, H, UiBuilder.FieldColor, true);
            UiBuilder.Panel3(compact, "Inner", 10, 10, W - 20, H - 20, UiBuilder.CardInner, true);

            var cCost = UiBuilder.CircleImage(compact, "Cost", 20, 20, 112, UiBuilder.EnergyColor);
            var cCostText = UiBuilder.Text(cCost.transform, "CostText", 0, 0, 112, 112, "0", 66,
                new Color(0.06f, 0.09f, 0.09f, 1f), TextAlignmentOptions.Center, true);
            UiBuilder.Stretch(cCostText.rectTransform);

            var cArt = UiBuilder.Picture(compact, "Art", 40, 150, 420, 315);
            cArt.gameObject.SetActive(false);
            var cArtPh = UiBuilder.Panel3(compact, "ArtPlaceholder", 40, 150, 420, 315, UiBuilder.Placeholder);

            var cName = UiBuilder.Text(compact, "Name", 24, 486, 452, 190, "カード名", 48,
                UiBuilder.TextMain, TextAlignmentOptions.Top, true, true);

            compact.gameObject.SetActive(false);

            // ---- 共通オーバーレイ ----
            var dim = UiBuilder.Panel3(root, "Dim", 0, 0, W, H, new Color(0f, 0f, 0f, 0.55f), true);
            dim.gameObject.SetActive(false);

            var badgeImg = UiBuilder.Panel3(root, "Badge", 360, 596, 118, 80, UiBuilder.GoldDim);
            var badgeText = UiBuilder.Text(badgeImg.transform, "Text", 0, 0, 118, 80, "×1", 46,
                Color.white, TextAlignmentOptions.Center, true);
            UiBuilder.Stretch(badgeText.rectTransform);
            badgeImg.gameObject.SetActive(false);

            // ---- 参照を結線 ----
            visual.Root = root;
            visual.FullRoot = full.gameObject;
            visual.CompactRoot = compact.gameObject;
            visual.Outline = outline;
            visual.Dim = dim;
            visual.FullCardOverride = fullOverride;
            visual.FullFrame = frame;
            visual.FullCostText = costText;
            visual.FullNameText = nameText;
            visual.FullArt = art;
            visual.FullArtPlaceholder = artPh;
            visual.FullArtPlaceholderText = artPhText;
            visual.FullKindBand = band;
            visual.FullKindText = bandText;
            visual.FullBodyText = body;
            visual.CompactFrame = cFrame;
            visual.CompactCostText = cCostText;
            visual.CompactNameText = cName;
            visual.CompactArt = cArt;
            visual.CompactArtPlaceholder = cArtPh;

            badge.Root = badgeImg.gameObject;
            badge.Label = badgeText;

            data.CardId = "base";
            data.DisplayName = "カード名";

            Directory.CreateDirectory(Path.GetDirectoryName(CardBasePath));
            var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, CardBasePath);
            Object.DestroyImmediate(root.gameObject);
            return saved;
        }

        // =====================================================================
        // カード59枚 (デッキごとのフォルダに Prefab Variant として作る)
        // =====================================================================

        static Dictionary<string, List<CardData>> BuildCardVariants(GameObject basePrefab)
        {
            var result = new Dictionary<string, List<CardData>>();

            foreach (var deck in CardSeedData.All)
            {
                string folder = $"{CardsRoot}/{deck.Id}";
                Directory.CreateDirectory(folder);

                var list = new List<CardData>();
                foreach (var seed in deck.Cards)
                {
                    string path = $"{folder}/{seed.Id}.prefab";

                    // すでにあるプレハブから「絵」と「手で変えた効果」を引き継ぐ
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Sprite keepArt = null, keepFull = null;
                    if (existing != null)
                    {
                        var old = existing.GetComponent<CardData>();
                        if (old != null) { keepArt = old.Illustration; keepFull = old.FullCardImage; }
                    }

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                    instance.name = seed.Id;

                    var data = instance.GetComponent<CardData>();
                    data.CardId = seed.Id;
                    data.DisplayName = seed.Name;
                    data.DeckId = deck.Id;
                    data.Kind = seed.Kind;
                    data.Cost = seed.Cost;
                    data.EffectText = seed.Text;
                    data.Trigger = seed.Trigger;
                    data.Effect = seed.Effect != null ? seed.Effect.Kind : EffectKind.None;
                    data.Amount = seed.Effect != null ? seed.Effect.Amount : 0;
                    data.DiceSides = seed.Effect != null ? seed.Effect.DiceSides : 0;
                    data.Modifier = seed.Effect != null ? seed.Effect.Modifier : 0;
                    data.TrashSelfAfter = seed.Effect != null && seed.Effect.TrashSelfAfter;
                    data.Illustration = keepArt;
                    data.FullCardImage = keepFull;

                    var visual = instance.GetComponent<CardVisual>();
                    if (visual != null) visual.Apply(data);

                    var savedGo = PrefabUtility.SaveAsPrefabAsset(instance, path);
                    Object.DestroyImmediate(instance);

                    var savedData = savedGo != null ? savedGo.GetComponent<CardData>() : null;
                    if (savedData != null) list.Add(savedData);
                }
                result[deck.Id] = list;
            }
            return result;
        }

        // =====================================================================
        // カード裏面 / デッキ選択セル
        // =====================================================================

        static GameObject BuildCardBackPrefab()
        {
            const float W = ArtSizes.CardW;
            const float H = ArtSizes.CardH;

            var root = UiBuilder.Node("CardBack", null);
            UiBuilder.Place(root, 0, 0, W, H);
            UiBuilder.Panel3(root, "Frame", 0, 0, W, H, new Color(0.22f, 0.17f, 0.26f, 1f), true);
            UiBuilder.Panel3(root, "Inner", 60, 62, W - 120, H - 124, new Color(0.33f, 0.25f, 0.39f, 1f), true);

            string path = UiPrefabRoot + "/CardBack.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            return saved;
        }

        static DeckCell BuildDeckCellPrefab()
        {
            const float W = 220f, H = 400f;
            float pw = W - 24f;
            float ph = pw * ArtSizes.PortraitH / ArtSizes.PortraitW;

            var root = UiBuilder.Node("DeckCell", null);
            UiBuilder.Place(root, 0, 0, W, H);

            var frame = UiBuilder.Panel3(root, "Frame", 0, 0, W, H, new Color(0.25f, 0.23f, 0.30f, 1f), true);
            UiBuilder.Stretch(frame.rectTransform);
            frame.raycastTarget = true;

            UiBuilder.Panel3(root, "Inner", 4, 4, W - 8, H - 8, UiBuilder.Panel, true);

            var portrait = UiBuilder.Picture(root, "Portrait", 12, 12, pw, ph);
            portrait.gameObject.SetActive(false);

            var ph2 = UiBuilder.Panel3(root, "PortraitPlaceholder", 12, 12, pw, ph, UiBuilder.Placeholder);
            var phText = UiBuilder.Text(ph2.transform, "Text", 0, 0, pw, ph, "立ち絵", 16,
                UiBuilder.PlaceholderText, TextAlignmentOptions.Center);
            UiBuilder.Stretch(phText.rectTransform);

            float ty = 12 + ph + 8;
            var nameText = UiBuilder.Text(root, "Name", 10, ty, W - 20, 34, "名前", 26, UiBuilder.TextMain, TextAlignmentOptions.Center, true);
            var archText = UiBuilder.Text(root, "Archetype", 8, ty + 34, W - 16, 26, "", 16, UiBuilder.TextDim, TextAlignmentOptions.Center);
            var styleText = UiBuilder.Text(root, "DrawStyle", 8, ty + 58, W - 16, 26, "", 16, UiBuilder.FieldColor, TextAlignmentOptions.Center);
            var tagText = UiBuilder.Text(root, "Tag", 8, 8, W - 16, 30, "YOU", 20, UiBuilder.Gold, TextAlignmentOptions.TopRight, true);
            tagText.gameObject.SetActive(false);

            var cell = root.gameObject.AddComponent<DeckCell>();
            cell.Frame = frame;
            cell.Portrait = portrait;
            cell.PortraitPlaceholder = ph2.gameObject;
            cell.PortraitPlaceholderText = phText;
            cell.NameText = nameText;
            cell.ArchetypeText = archText;
            cell.DrawStyleText = styleText;
            cell.TagText = tagText;

            string path = UiPrefabRoot + "/DeckCell.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            return saved != null ? saved.GetComponent<DeckCell>() : null;
        }

        // =====================================================================
        // デッキアセット / データベース
        // =====================================================================

        static List<DeckAsset> BuildDeckAssets(Dictionary<string, List<CardData>> cardsByDeck)
        {
            var list = new List<DeckAsset>();

            foreach (var seed in CardSeedData.All)
            {
                string path = $"{DecksRoot}/{seed.Id}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<DeckAsset>(path);
                bool isNew = asset == null;
                if (isNew) asset = ScriptableObject.CreateInstance<DeckAsset>();

                asset.DeckId = seed.Id;
                asset.CharacterName = seed.CharacterName;
                asset.Archetype = seed.Archetype;
                asset.DrawStyle = seed.DrawStyle;
                // Portrait / PortraitIcon は手で割り当てたものを保持する

                asset.Entries = RebuildEntries(asset, seed, cardsByDeck);

                if (isNew) AssetDatabase.CreateAsset(asset, path);
                else EditorUtility.SetDirty(asset);

                list.Add(asset);
            }
            return list;
        }

        /// <summary>
        /// デッキのカード構成を作り直す。
        /// 枚数は「前回この カードID に入っていた値」を優先し、無ければ仕様書の値を使う。
        /// 仕様書に無いカードを手で足していた場合は末尾にそのまま残す。
        /// </summary>
        static List<DeckEntry> RebuildEntries(DeckAsset asset, DeckDef seed,
            Dictionary<string, List<CardData>> cardsByDeck)
        {
            // 前回の枚数を カードID で覚えておく
            var previousCounts = new Dictionary<string, int>();
            var carriedOver = new List<DeckEntry>();
            if (asset.Entries != null)
            {
                foreach (var e in asset.Entries)
                {
                    if (e == null || e.Card == null) continue;
                    previousCounts[e.Card.CardId] = e.Count;
                }
            }

            var seedIds = new HashSet<string>();
            foreach (var c in seed.Cards) seedIds.Add(c.Id);

            // 仕様書に無い手動追加ぶんを退避
            if (asset.Entries != null)
            {
                foreach (var e in asset.Entries)
                {
                    if (e == null || e.Card == null) continue;
                    if (!seedIds.Contains(e.Card.CardId)) carriedOver.Add(e);
                }
            }

            var cards = cardsByDeck.TryGetValue(seed.Id, out var c2) ? c2 : new List<CardData>();
            var byId = new Dictionary<string, CardData>();
            foreach (var card in cards)
                if (card != null) byId[card.CardId] = card;

            var entries = new List<DeckEntry>();
            foreach (var seedCard in seed.Cards)
            {
                if (!byId.TryGetValue(seedCard.Id, out var card)) continue;
                int count = previousCounts.TryGetValue(seedCard.Id, out var prev) ? prev : seedCard.Count;
                entries.Add(new DeckEntry(card, count));
            }
            entries.AddRange(carriedOver);
            return entries;
        }

        static GameDatabase BuildDatabase(List<DeckAsset> decks)
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            bool isNew = db == null;
            if (isNew) db = ScriptableObject.CreateInstance<GameDatabase>();

            db.Decks = decks;
            db.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            db.FontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBoldPath);

            if (isNew) AssetDatabase.CreateAsset(db, DatabasePath);
            else EditorUtility.SetDirty(db);

            db.InvalidateLookup();
            return db;
        }
    }
}
