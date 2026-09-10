using System.IO;
using RoundTable.App;
using UnityEditor;
using UnityEngine;

namespace RoundTable.EditorTools
{
    /// <summary>
    /// デバッグメニューを開くための開発者コードを設定する窓。
    /// コードそのものではなく SHA-256 ハッシュを焼き込む。ファイルは .gitignore 済み。
    /// </summary>
    public sealed class DevCodeSetup : EditorWindow
    {
        const string Folder = "Assets/RoundTable/Resources";
        static string AssetPath => $"{Folder}/{DevAccess.CodeResourceName}.txt";

        string _code = "";
        bool _show;

        [MenuItem("Round Table/開発者コードを設定", priority = 24)]
        static void Open()
        {
            var w = GetWindow<DevCodeSetup>(true, "開発者コードの設定");
            w.minSize = new Vector2(560, 320);
            w._code = "";
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("デバッグメニューの開発者コード", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "タイトル画面の隅に出る小さなボタンから、このコードでデバッグメニューを開けます。\n" +
                "今のところ「GitHub に残ったルームを削除する」機能が入っています。\n\n" +
                "保存されるのはコードそのものではなく SHA-256 ハッシュです。\n" +
                "ファイルは .gitignore 済みなので GitHub には上がりません。",
                MessageType.Info);

            EditorGUILayout.HelpBox(
                "⚠️ これは他人を締め出す仕組みではありません。\n" +
                "・ブラウザ版はビルドの中身を誰でも取り出せるので、ハッシュも取り出せます\n" +
                "  短いコードだと総当たりで割られます。長め（12文字以上）にしてください\n" +
                "・そもそも遊ぶ人は全員トークンを持っているので、その気になれば\n" +
                "  このメニューを通さなくても GitHub 上のルームは消せます\n" +
                "・「普通に遊んでいる人が誤って踏まない」ための目隠しと考えてください",
                MessageType.Warning);

            EditorGUILayout.Space();

            bool exists = File.Exists(AssetPath);
            EditorGUILayout.LabelField("状態", exists ? "設定済み" : "未設定（デバッグメニューは表示されません）");
            EditorGUILayout.LabelField("保存先", AssetPath);

            EditorGUILayout.Space();
            _show = EditorGUILayout.ToggleLeft("入力中のコードを表示する", _show);
            _code = _show
                ? EditorGUILayout.TextField("コード", _code)
                : EditorGUILayout.PasswordField("コード", _code);

            if (!string.IsNullOrEmpty(_code) && _code.Trim().Length < 8)
                EditorGUILayout.HelpBox("8文字未満は短すぎます。12文字以上を推奨。", MessageType.Warning);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_code)))
                    if (GUILayout.Button("保存（ビルドに焼き込む）", GUILayout.Height(30)))
                        Save();

                using (new EditorGUI.DisabledScope(!exists))
                    if (GUILayout.Button("削除（メニューを無効にする）", GUILayout.Height(30)))
                        Delete();
            }
        }

        void Save()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(AssetPath, DevAccess.Hash(_code));
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);

            _code = "";
            _show = false;
            Debug.Log($"[RoundTable] 開発者コードを保存しました: {AssetPath}");
        }

        void Delete()
        {
            AssetDatabase.DeleteAsset(AssetPath);
            AssetDatabase.Refresh();
            Debug.Log("[RoundTable] 開発者コードを削除しました。デバッグメニューは表示されなくなります。");
        }
    }
}
