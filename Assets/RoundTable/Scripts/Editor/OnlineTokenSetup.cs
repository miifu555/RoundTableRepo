using System.IO;
using RoundTable.Net;
using UnityEditor;
using UnityEngine;

namespace RoundTable.EditorTools
{
    /// <summary>
    /// 通信対戦の共有トークンをビルドに焼き込むための設定窓。
    /// ここで保存したファイルは .gitignore 済みなので、リポジトリには入らない。
    /// 焼き込んでおくと、遊ぶ人はタイトル画面でトークンを入力しなくてよくなる。
    /// </summary>
    public sealed class OnlineTokenSetup : EditorWindow
    {
        const string Folder = "Assets/RoundTable/Resources";
        static string AssetPath => $"{Folder}/{OnlineConfig.TokenResourceName}.txt";

        string _token = "";
        bool _show;

        [MenuItem("Round Table/通信トークンを設定", priority = 23)]
        static void Open()
        {
            var w = GetWindow<OnlineTokenSetup>(true, "通信トークンの設定");
            w.minSize = new Vector2(560, 300);
            w._token = File.Exists(AssetPath) ? File.ReadAllText(AssetPath).Trim() : "";
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField($"対戦に使うリポジトリ: {OnlineConfig.RepoDisplay}", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "ここに入れたトークンはビルドに焼き込まれ、遊ぶ人は入力不要になります。\n" +
                "・ファイルは .gitignore 済みなので GitHub には上がりません\n" +
                "・ただしビルドの中身を覗けば取り出せます。fine-grained トークンで\n" +
                "  " + OnlineConfig.RepoDisplay + " の Contents だけに限定し、配る相手を絞ってください\n" +
                "・ブラウザ(WebGL)で公開する場合は、誰でも取り出せるので焼き込まないこと",
                MessageType.Warning);

            EditorGUILayout.Space();
            _show = EditorGUILayout.ToggleLeft("入力中のトークンを表示する", _show);
            _token = _show
                ? EditorGUILayout.TextField("トークン", _token)
                : EditorGUILayout.PasswordField("トークン", _token);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_token)))
                    if (GUILayout.Button("保存（ビルドに焼き込む）", GUILayout.Height(30))) Save();

                using (new EditorGUI.DisabledScope(!File.Exists(AssetPath)))
                    if (GUILayout.Button("削除（各自入力に戻す）", GUILayout.Height(30))) Delete();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("現在の状態", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(File.Exists(AssetPath)
                ? $"焼き込み済み: {AssetPath}"
                : "未設定（プレイヤーがタイトル画面で入力する）");
        }

        void Save()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(AssetPath, _token.Trim());
            AssetDatabase.ImportAsset(AssetPath);
            AssetDatabase.Refresh();
            Debug.Log($"[RoundTable] 通信トークンを焼き込みました: {AssetPath}（gitignore 済み）");
        }

        void Delete()
        {
            AssetDatabase.DeleteAsset(AssetPath);
            AssetDatabase.Refresh();
            _token = "";
            Debug.Log("[RoundTable] 通信トークンの焼き込みを解除しました。");
        }
    }
}
