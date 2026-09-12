using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RoundTable.EditorTools
{
    /// <summary>
    /// Windows で単体で遊べる .exe を作る。
    ///
    /// WebGL 版と違って GitHub Pages のような公開ホスティングを経由しないので、
    /// 実写イラストを使うときはこちらで配る方が安全 (zip にして直接友人に渡す想定。
    /// リポジトリには絶対に入れないこと。.gitignore 済み)。
    ///
    /// - メニュー「Round Table / Windows をビルド」から実行（出力先は Build/Windows）
    /// - CI から叩く場合は batchmode で
    ///   Unity -batchmode -quit -executeMethod RoundTable.EditorTools.WindowsBuild.BuildFromCommandLine
    ///   出力先は -buildOutput &lt;path&gt; で指定できる。
    /// </summary>
    public static class WindowsBuild
    {
        public const string DefaultOutput = "Build/Windows";
        const string ExeName = "RoundTable.exe";

        [MenuItem("Round Table/Windows をビルド", priority = 41)]
        public static void BuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Round Table",
                    $"Windows ビルドを作ります。出力先は {DefaultOutput}。\n" +
                    "初回は数分かかり、その間 Unity は操作できません。",
                    "ビルドする", "やめる"))
                return;

            Build(DefaultOutput);
        }

        /// <summary>batchmode 用の入口。終了コードで成否を返す。</summary>
        public static void BuildFromCommandLine()
        {
            var output = ArgValue("-buildOutput") ?? ArgValue("-customBuildPath") ?? DefaultOutput;
            bool ok = Build(output.TrimEnd('/', '\\'));
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>ビルド本体。成功したら true。</summary>
        public static bool Build(string output)
        {
            if (string.IsNullOrEmpty(output)) output = DefaultOutput;

            var scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[WindowsBuild] Build Settings に有効なシーンが1つもありません。");
                return false;
            }

            ApplyWindowsSettings();

            Directory.CreateDirectory(output);
            var exePath = Path.Combine(output, ExeName);
            Debug.Log($"[WindowsBuild] 出力先: {Path.GetFullPath(exePath)}\n  シーン: {string.Join(", ", scenes)}");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            bool ok = summary.result == BuildResult.Succeeded;

            string text = ok
                ? $"OK  {summary.totalSize / (1024f * 1024f):0.0} MB  {summary.totalTime}  {DateTime.Now:HH:mm:ss}"
                : $"NG  result={summary.result} errors={summary.totalErrors}  {DateTime.Now:HH:mm:ss}";

            WriteResult(output, text, report);

            if (ok) Debug.Log($"[WindowsBuild] 成功: {text}\n  {Path.GetFullPath(exePath)}");
            else Debug.LogError($"[WindowsBuild] 失敗: {text}");
            return ok;
        }

        /// <summary>
        /// ビルドの成否を出力先の隣にテキストで残す。
        /// ビルドは数分かかり MCP 越しの応答がタイムアウトすることがあるので、
        /// ファイルを見れば成否が分かるようにしておく。
        /// </summary>
        static void WriteResult(string output, string text, BuildReport report)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(text);
                foreach (var step in report.steps)
                    foreach (var msg in step.messages)
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                            sb.AppendLine($"  [{step.name}] {msg.content}");

                var dir = Path.GetDirectoryName(Path.GetFullPath(output));
                if (!string.IsNullOrEmpty(dir))
                    File.WriteAllText(Path.Combine(dir, "windows-build-result.txt"), sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WindowsBuild] 結果の書き出しに失敗: " + e.Message);
            }
        }

        static void ApplyWindowsSettings()
        {
            // 別ウィンドウに移っても通信対戦のポーリングが止まらないように。
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
        }

        static string[] EnabledScenes() => EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => s.path)
            .ToArray();

        static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }
}
