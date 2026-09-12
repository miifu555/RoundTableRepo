using System.Collections.Generic;
using System.IO;
using RoundTable.Data;
using UnityEditor;
using UnityEngine;

namespace RoundTable.EditorTools
{
    /// <summary>
    /// 決められたフォルダに置いた PNG を、ファイル名を頼りにカードプレハブ・デッキ・
    /// データベースへ一括で割り当てるツール。
    /// 1枚ずつ Inspector にドラッグしても構わないが、59枚あるのでこちらが楽。
    /// </summary>
    public static class RoundTableArtImporter
    {
        public const string ArtRoot = "Assets/RoundTable/Art";
        public const string CardArtFolder = ArtRoot + "/CardArt";   // {cardId}.png     440x330
        public const string CardFullFolder = ArtRoot + "/CardFull"; // {cardId}.png     500x700 (任意)
        public const string PortraitFolder = ArtRoot + "/Portrait"; // {deckId}.png     512x768
                                                                    // {deckId}_icon.png 256x256
        public const string CommonFolder = ArtRoot + "/Common";     // card_back / bg_battle / bg_title / dao_on / dao_off

        [MenuItem("Round Table/イラストを一括割り当て", priority = 20)]
        public static void AssignAll()
        {
            EnsureFolders();

            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(RoundTableBuilder.DatabasePath);
            if (db == null)
            {
                Debug.LogError("[RoundTable] RoundTableDatabase が見つかりません。先に「すべて再生成」を実行してください。");
                return;
            }

            int cards = 0, fulls = 0, portraits = 0, icons = 0, commons = 0;

            var cardArt = LoadSprites(CardArtFolder);
            var cardFull = LoadSprites(CardFullFolder);
            var portraitArt = LoadSprites(PortraitFolder);
            var commonArt = LoadSprites(CommonFolder);

            foreach (var deck in db.Decks)
            {
                if (deck == null) continue;

                if (portraitArt.TryGetValue(deck.DeckId, out var p) && deck.Portrait != p)
                {
                    deck.Portrait = p;
                    portraits++;
                }
                if (portraitArt.TryGetValue(deck.DeckId + "_icon", out var ic) && deck.PortraitIcon != ic)
                {
                    deck.PortraitIcon = ic;
                    icons++;
                }
                EditorUtility.SetDirty(deck);

                foreach (var entry in deck.Entries)
                {
                    var card = entry != null ? entry.Card : null;
                    if (card == null) continue;
                    bool dirty = false;

                    if (cardArt.TryGetValue(card.CardId, out var art) && card.Illustration != art)
                    {
                        card.Illustration = art;
                        cards++;
                        dirty = true;
                    }
                    if (cardFull.TryGetValue(card.CardId, out var full) && card.FullCardImage != full)
                    {
                        card.FullCardImage = full;
                        fulls++;
                        dirty = true;
                    }

                    if (dirty)
                    {
                        var visual = card.GetComponent<CardVisual>();
                        if (visual != null) visual.Apply(card);
                        EditorUtility.SetDirty(card);
                        PrefabUtility.SavePrefabAsset(card.gameObject);
                    }
                }
            }

            commons += Assign(commonArt, "card_back", s => db.CardBack = s, db.CardBack);
            commons += Assign(commonArt, "bg_battle", s => db.BattleBackground = s, db.BattleBackground);
            commons += Assign(commonArt, "bg_title", s => db.TitleBackground = s, db.TitleBackground);
            commons += Assign(commonArt, "dao_on", s => db.DaoOn = s, db.DaoOn);
            commons += Assign(commonArt, "dao_off", s => db.DaoOff = s, db.DaoOff);
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RoundTable] 割り当て完了: カードイラスト {cards} / カード全体 {fulls} / " +
                      $"立ち絵 {portraits} / 顔アイコン {icons} / 共通 {commons}");
        }

        static int Assign(Dictionary<string, Sprite> map, string key, System.Action<Sprite> setter, Sprite current)
        {
            if (!map.TryGetValue(key, out var s) || s == current) return 0;
            setter(s);
            return 1;
        }

        [MenuItem("Round Table/割り当て状況を確認", priority = 21)]
        public static void ReportMissing()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(RoundTableBuilder.DatabasePath);
            if (db == null) { Debug.LogError("[RoundTable] RoundTableDatabase が見つかりません。"); return; }

            var missingCards = new List<string>();
            var missingPortraits = new List<string>();

            foreach (var deck in db.Decks)
            {
                if (deck == null) continue;
                if (deck.Portrait == null) missingPortraits.Add($"{deck.DeckId}.png (立ち絵 {ArtSizes.PortraitW}x{ArtSizes.PortraitH})");
                if (deck.PortraitIcon == null) missingPortraits.Add($"{deck.DeckId}_icon.png (顔 {ArtSizes.PortraitIconW}x{ArtSizes.PortraitIconH})");
                foreach (var entry in deck.Entries)
                {
                    var card = entry != null ? entry.Card : null;
                    if (card != null && card.Illustration == null && card.FullCardImage == null)
                        missingCards.Add($"{card.CardId}.png ({card.DisplayName})");
                }
            }

            var missingCommon = new List<string>();
            if (db.CardBack == null) missingCommon.Add($"card_back.png ({ArtSizes.CardBackW}x{ArtSizes.CardBackH})");
            if (db.BattleBackground == null) missingCommon.Add($"bg_battle.png ({ArtSizes.BackgroundW}x{ArtSizes.BackgroundH})");
            if (db.TitleBackground == null) missingCommon.Add($"bg_title.png ({ArtSizes.BackgroundW}x{ArtSizes.BackgroundH})");
            if (db.DaoOn == null) missingCommon.Add($"dao_on.png ({ArtSizes.IconDao}x{ArtSizes.IconDao})");
            if (db.DaoOff == null) missingCommon.Add($"dao_off.png ({ArtSizes.IconDao}x{ArtSizes.IconDao})");

            Debug.Log($"[RoundTable] 未設定のイラスト\n" +
                      $"■ カード ({missingCards.Count} 枚) → {CardArtFolder}\n   " + string.Join("\n   ", missingCards) + "\n" +
                      $"■ キャラ ({missingPortraits.Count} 件) → {PortraitFolder}\n   " + string.Join("\n   ", missingPortraits) + "\n" +
                      $"■ 共通 ({missingCommon.Count} 件) → {CommonFolder}\n   " + string.Join("\n   ", missingCommon));
        }

        static void EnsureFolders()
        {
            foreach (var d in new[] { ArtRoot, CardArtFolder, CardFullFolder, PortraitFolder, CommonFolder })
            {
                Directory.CreateDirectory(d);
                string readme = Path.Combine(d, "_ここに入れる.txt");
                if (!File.Exists(readme)) File.WriteAllText(readme, DescribeFolder(d), System.Text.Encoding.UTF8);
            }
            AssetDatabase.Refresh();
        }

        static string DescribeFolder(string folder)
        {
            switch (folder)
            {
                case CardArtFolder:
                    return $"カードのイラスト。{ArtSizes.CardArtW} x {ArtSizes.CardArtH} px。\n" +
                           "ファイル名は カードID.png（例: toshi_a1.png）。全60枚。\n" +
                           "置いたあと メニュー「Round Table / イラストを一括割り当て」を実行。\n";
                case CardFullFolder:
                    return $"カード全体を1枚絵で差し替える場合。{ArtSizes.CardW} x {ArtSizes.CardH} px。\n" +
                           "ファイル名は カードID.png。設定するとイラスト枠より優先される（任意）。\n";
                case PortraitFolder:
                    return $"キャラの立ち絵 {ArtSizes.PortraitW} x {ArtSizes.PortraitH} px → デッキID.png（例: toshi.png）\n" +
                           $"顔アイコン {ArtSizes.PortraitIconW} x {ArtSizes.PortraitIconH} px → デッキID_icon.png（例: toshi_icon.png）\n";
                case CommonFolder:
                    return $"card_back.png  {ArtSizes.CardBackW} x {ArtSizes.CardBackH}（カード裏面）\n" +
                           $"bg_battle.png  {ArtSizes.BackgroundW} x {ArtSizes.BackgroundH}（対戦背景）\n" +
                           $"bg_title.png   {ArtSizes.BackgroundW} x {ArtSizes.BackgroundH}（タイトル/デッキ選択背景）\n" +
                           $"dao_on.png     {ArtSizes.IconDao} x {ArtSizes.IconDao}（^^ 獲得済み）\n" +
                           $"dao_off.png    {ArtSizes.IconDao} x {ArtSizes.IconDao}（^^ 未獲得）\n";
                default:
                    return "Round Table のアートを入れるフォルダです。\n";
            }
        }

        /// <summary>フォルダ内の PNG を「拡張子なしのファイル名 → Sprite」で集める。</summary>
        static Dictionary<string, Sprite> LoadSprites(string folder)
        {
            var map = new Dictionary<string, Sprite>();
            if (!Directory.Exists(folder)) return map;

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Sprite として読めるようにインポート設定を直す
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) map[Path.GetFileNameWithoutExtension(path)] = sprite;
            }
            return map;
        }
    }
}
