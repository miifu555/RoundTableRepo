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
    ///
    /// 公開用の絵 (<see cref="ArtRoot"/>) とは別に、実写など見せる相手を限定したい絵を
    /// 置く <see cref="PhotosRoot"/> がある。Photos は .gitignore 済みで GitHub には一切上がらない。
    /// WebGL ビルドの直前には必ず <see cref="ResetToPublicArtOnly"/> が走り、Photos の絵を
    /// 使っていても公開用の状態に戻してからビルドする (WebGLBuild.cs 側で強制)。
    /// Windows ビルドは <see cref="AssignPhotosOverlay"/> → ビルド → <see cref="ResetToPublicArtOnly"/>
    /// の順で、ビルドの瞬間だけ Photos を割り当てる (WindowsBuild.cs 側)。
    /// </summary>
    public static class RoundTableArtImporter
    {
        public const string ArtRoot = "Assets/RoundTable/Art";
        public const string CardArtFolder = ArtRoot + "/CardArt";   // {カードID}.png     440x330
        public const string CardFullFolder = ArtRoot + "/CardFull"; // {カードID}.png     500x700 (任意)
        public const string PortraitFolder = ArtRoot + "/Portrait"; // {デッキID}.png     512x768
                                                                    // {デッキID}_icon.png 256x256
        public const string CommonFolder = ArtRoot + "/Common";     // card_back / bg_battle / bg_title / dao_on / dao_off

        /// <summary>実写などを置く場所。.gitignore 済み (Windows ビルド限定)。</summary>
        public const string PhotosRoot = ArtRoot + "/Photos";
        public const string PhotosCardArtFolder = PhotosRoot + "/CardArt";
        public const string PhotosCardFullFolder = PhotosRoot + "/CardFull";
        public const string PhotosPortraitFolder = PhotosRoot + "/Portrait";
        public const string PhotosCommonFolder = PhotosRoot + "/Common";

        [MenuItem("Round Table/イラストを一括割り当て", priority = 20)]
        public static void AssignAll()
        {
            EnsureFolders();
            var db = LoadDatabase();
            if (db == null) return;

            var r = ApplyFolders(db, CardArtFolder, CardFullFolder, PortraitFolder, CommonFolder, clearFirst: false);
            SaveAll(db);

            Debug.Log($"[RoundTable] 割り当て完了 (公開用): カードイラスト {r.cards} / カード全体 {r.fulls} / " +
                      $"立ち絵 {r.portraits} / 顔アイコン {r.icons} / 共通 {r.commons}");
        }

        /// <summary>
        /// Photos フォルダの絵を、今の割り当てに上書きで重ねる。公開用の絵しか無いカードは
        /// そのまま残る。Windows ビルドの直前に自動で呼ばれるが、ビルドせず
        /// エディタでプレビューしたいときもこのメニューで単体実行できる。
        /// </summary>
        [MenuItem("Round Table/実写を割り当てる (Windows限定)", priority = 22)]
        public static void AssignPhotosOverlay()
        {
            EnsurePhotoFolders();
            var db = LoadDatabase();
            if (db == null) return;

            var r = ApplyFolders(db, PhotosCardArtFolder, PhotosCardFullFolder, PhotosPortraitFolder, PhotosCommonFolder, clearFirst: false);
            SaveAll(db);

            Debug.Log($"[RoundTable] 実写を割り当てました (Photos フォルダ): カードイラスト {r.cards} / カード全体 {r.fulls} / " +
                      $"立ち絵 {r.portraits} / 顔アイコン {r.icons} / 共通 {r.commons}\n" +
                      "⚠️ この状態で WebGL をビルドしないこと。WebGL は自動で公開用に戻ります。");
        }

        /// <summary>
        /// すべての絵をいったん外し、公開用フォルダ (<see cref="ArtRoot"/>) の絵だけを
        /// 割り当て直す。Photos の絵が何であれ、これを実行すれば必ず消える。
        /// WebGL ビルドの直前に無条件で呼ばれる安全策。
        /// </summary>
        [MenuItem("Round Table/実写を外す (公開用に戻す)", priority = 23)]
        public static void ResetToPublicArtOnly()
        {
            var db = LoadDatabase();
            if (db == null) return;

            ClearAll(db);
            var r = ApplyFolders(db, CardArtFolder, CardFullFolder, PortraitFolder, CommonFolder, clearFirst: false);
            SaveAll(db);

            Debug.Log($"[RoundTable] 公開用の状態に戻しました: カードイラスト {r.cards} / カード全体 {r.fulls} / " +
                      $"立ち絵 {r.portraits} / 顔アイコン {r.icons} / 共通 {r.commons}");
        }

        struct Counts
        {
            public int cards, fulls, portraits, icons, commons;
        }

        static Counts ApplyFolders(GameDatabase db, string cardArtFolder, string cardFullFolder,
            string portraitFolder, string commonFolder, bool clearFirst)
        {
            var c = new Counts();

            var cardArt = LoadSprites(cardArtFolder);
            var cardFull = LoadSprites(cardFullFolder);
            var portraitArt = LoadSprites(portraitFolder);
            var commonArt = LoadSprites(commonFolder);

            foreach (var deck in db.Decks)
            {
                if (deck == null) continue;

                if (portraitArt.TryGetValue(deck.DeckId, out var p) && deck.Portrait != p)
                {
                    deck.Portrait = p;
                    c.portraits++;
                }
                if (portraitArt.TryGetValue(deck.DeckId + "_icon", out var ic) && deck.PortraitIcon != ic)
                {
                    deck.PortraitIcon = ic;
                    c.icons++;
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
                        c.cards++;
                        dirty = true;
                    }
                    if (cardFull.TryGetValue(card.CardId, out var full) && card.FullCardImage != full)
                    {
                        card.FullCardImage = full;
                        c.fulls++;
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

            c.commons += Assign(commonArt, "card_back", s => db.CardBack = s, db.CardBack);
            c.commons += Assign(commonArt, "bg_battle", s => db.BattleBackground = s, db.BattleBackground);
            c.commons += Assign(commonArt, "bg_title", s => db.TitleBackground = s, db.TitleBackground);
            c.commons += Assign(commonArt, "dao_on", s => db.DaoOn = s, db.DaoOn);
            c.commons += Assign(commonArt, "dao_off", s => db.DaoOff = s, db.DaoOff);
            EditorUtility.SetDirty(db);

            return c;
        }

        /// <summary>すべてのカード/デッキ/共通の絵参照を null に戻す (公開用リセットの下準備)。</summary>
        static void ClearAll(GameDatabase db)
        {
            foreach (var deck in db.Decks)
            {
                if (deck == null) continue;
                deck.Portrait = null;
                deck.PortraitIcon = null;
                EditorUtility.SetDirty(deck);

                foreach (var entry in deck.Entries)
                {
                    var card = entry != null ? entry.Card : null;
                    if (card == null) continue;

                    card.Illustration = null;
                    card.FullCardImage = null;

                    var visual = card.GetComponent<CardVisual>();
                    if (visual != null) visual.Apply(card);
                    EditorUtility.SetDirty(card);
                    PrefabUtility.SavePrefabAsset(card.gameObject);
                }
            }

            db.CardBack = null;
            db.BattleBackground = null;
            db.TitleBackground = null;
            db.DaoOn = null;
            db.DaoOff = null;
            EditorUtility.SetDirty(db);
        }

        static GameDatabase LoadDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(RoundTableBuilder.DatabasePath);
            if (db == null)
                Debug.LogError("[RoundTable] RoundTableDatabase が見つかりません。先に「すべて再生成」を実行してください。");
            return db;
        }

        static void SaveAll(GameDatabase db)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
            var db = LoadDatabase();
            if (db == null) return;

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

            Debug.Log($"[RoundTable] 未設定のイラスト (公開用フォルダ基準)\n" +
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

        /// <summary>Photos フォルダ (.gitignore 済み) を必要なら作る。無くても動くように毎回呼ぶ。</summary>
        static void EnsurePhotoFolders()
        {
            foreach (var d in new[] { PhotosRoot, PhotosCardArtFolder, PhotosCardFullFolder, PhotosPortraitFolder, PhotosCommonFolder })
            {
                Directory.CreateDirectory(d);
                string readme = Path.Combine(d, "_ここに入れる.txt");
                if (!File.Exists(readme)) File.WriteAllText(readme, DescribePhotoFolder(d), System.Text.Encoding.UTF8);
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
                           "置いたあと メニュー「Round Table / イラストを一括割り当て」を実行。\n" +
                           "\n" +
                           "実写など公開したくない絵は、ここではなく Art/Photos/ に置くこと\n" +
                           "（.gitignore 済み・Windows ビルド限定で使われる）。\n";
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

        static string DescribePhotoFolder(string folder)
        {
            const string common =
                "ここに置いた絵は GitHub には絶対に上がりません（Art/Photos/ ごと .gitignore 済み）。\n" +
                "使われるのは Windows ビルドだけです。WebGL は自動で公開用フォルダ (Art/) の絵に戻ります。\n" +
                "\n" +
                "メニュー「Round Table / 実写を割り当てる (Windows限定)」でエディタにも反映できます\n" +
                "（プレビュー用。戻すときは「Round Table / 実写を外す (公開用に戻す)」）。\n" +
                "ファイル名の付け方は Art/ の同名フォルダと同じです。\n\n";

            switch (folder)
            {
                case PhotosCardArtFolder: return common + $"カードのイラスト。{ArtSizes.CardArtW} x {ArtSizes.CardArtH} px。";
                case PhotosCardFullFolder: return common + $"カード全体の差し替え（任意）。{ArtSizes.CardW} x {ArtSizes.CardH} px。";
                case PhotosPortraitFolder: return common + $"立ち絵 {ArtSizes.PortraitW}x{ArtSizes.PortraitH} / 顔 {ArtSizes.PortraitIconW}x{ArtSizes.PortraitIconH}。";
                case PhotosCommonFolder: return common + "card_back / bg_battle / bg_title / dao_on / dao_off。";
                default: return common;
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
