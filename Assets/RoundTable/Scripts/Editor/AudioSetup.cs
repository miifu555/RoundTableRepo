using System.Collections.Generic;
using System.IO;
using RoundTable.Data;
using UnityEditor;
using UnityEngine;

namespace RoundTable.EditorTools
{
    /// <summary>
    /// Assets/RoundTable/Audio の WAV にインポート設定を当てて、GameDatabase に割り当てる。
    /// 音そのものを作り直したいときは Tools/make_audio.py を実行してからこれを走らせる。
    /// </summary>
    public static class AudioSetup
    {
        public const string AudioFolder = "Assets/RoundTable/Audio";

        /// <summary>ファイル名 → GameDatabase のどのフィールドに入れるか。</summary>
        static readonly (string file, string field, bool isBgm)[] Mapping =
        {
            ("bgm_title",      nameof(GameDatabase.BgmTitle),     true),
            ("bgm_battle",     nameof(GameDatabase.BgmBattle),    true),
            ("se_button",      nameof(GameDatabase.SeButton),     false),
            ("se_card_attack", nameof(GameDatabase.SeAttackCard), false),
            ("se_card_field",  nameof(GameDatabase.SeFieldCard),  false),
            ("se_end_turn",    nameof(GameDatabase.SeEndTurn),    false),
            ("se_win",         nameof(GameDatabase.SeWin),        false),
            ("se_lose",        nameof(GameDatabase.SeLose),       false),
        };

        [MenuItem("Round Table/音声を取り込む", priority = 30)]
        public static void ImportAll()
        {
            if (!Directory.Exists(AudioFolder))
            {
                Debug.LogError($"[RoundTable] {AudioFolder} がありません。先に Tools/make_audio.py を実行してください。");
                return;
            }

            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(RoundTableBuilder.DatabasePath);
            if (db == null)
            {
                Debug.LogError("[RoundTable] RoundTableDatabase が見つかりません。");
                return;
            }

            var clips = new Dictionary<string, AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                bool isBgm = name.StartsWith("bgm_");

                ApplyImportSettings(path, isBgm);

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) clips[name] = clip;
            }

            var so = new SerializedObject(db);
            int assigned = 0;
            var missing = new List<string>();

            foreach (var (file, field, _) in Mapping)
            {
                var prop = so.FindProperty(field);
                if (prop == null) continue;

                if (clips.TryGetValue(file, out var clip))
                {
                    if (prop.objectReferenceValue != clip)
                    {
                        prop.objectReferenceValue = clip;
                        assigned++;
                    }
                }
                else
                {
                    missing.Add(file + ".wav");
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"[RoundTable] 音声の取り込み完了: {clips.Count} 件を検出 / {assigned} 件を割り当て"
                      + (missing.Count > 0 ? "\n  未検出: " + string.Join(", ", missing) : ""));
        }

        /// <summary>
        /// BGM は長いのでストリーミング寄り、SE は短いので読み込み時に展開する。
        /// どちらも Vorbis で圧縮してビルドサイズを抑える。
        /// </summary>
        static void ApplyImportSettings(string path, bool isBgm)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;

            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = isBgm ? 0.5f : 0.7f;
            settings.loadType = isBgm ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            // preloadAudioData は AudioImporter 直下から SampleSettings 側へ移った
            settings.preloadAudioData = !isBgm;

            importer.defaultSampleSettings = settings;
            importer.loadInBackground = isBgm;
            importer.forceToMono = false;

            importer.SaveAndReimport();
        }
    }
}
