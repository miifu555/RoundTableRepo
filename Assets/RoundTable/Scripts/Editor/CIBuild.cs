using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RoundTable.EditorTools
{
    /// <summary>
    /// GitHub Actions から Unity を batchmode で叩いてビルドするための入口。
    /// エディタを開かずにビルドできるようにするためのもので、人間が手で使う必要はない。
    /// 使い方: Unity -batchmode -quit -executeMethod RoundTable.EditorTools.CIBuild.WebGL
    /// 出力先は -buildOutput <path>（省略時は build/WebGL）。
    /// </summary>
    public static class CIBuild
    {
        const string DefaultOutput = "build/WebGL";
        const string TemplateName = "PROJECT:RoundTableMobile";

        /// <summary>スマホのブラウザで遊べる WebGL ビルドを作る。</summary>
        public static void WebGL()
        {
            var output = ArgValue("-buildOutput") ?? ArgValue("-customBuildPath") ?? DefaultOutput;
            output = output.TrimEnd('/', '\\');

            MakeEditorOnlyPlugins();
            ApplyWebGLSettings();

            var scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                Fail("Build Settings に有効なシーンが1つもない。");
                return;
            }

            Log($"出力先: {output}");
            Log("シーン: " + string.Join(", ", scenes));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"ビルド失敗: result={summary.result} errors={summary.totalErrors}");
                return;
            }

            Log($"ビルド成功: {summary.totalSize / (1024 * 1024)} MB / {summary.totalTime}");
            EditorApplication.Exit(0);
        }

        /// <summary>WebGL 側の PlayerSettings を、スマホのブラウザで動く設定に揃える。</summary>
        static void ApplyWebGLSettings()
        {
            // 自作テンプレート。画面いっぱいに広げる・縦持ちのときは回転を促す。
            if (Directory.Exists("Assets/WebGLTemplates/RoundTableMobile"))
                PlayerSettings.WebGL.template = TemplateName;
            else
                Log("テンプレート RoundTableMobile が見つからないので既定のものを使う。");

            // GitHub Pages は Content-Encoding を付けられないので、
            // gzip + JS 側での展開フォールバックにしておく（Brotli より展開が速い）。
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            // 2回目以降の起動を速くする（ブラウザの IndexedDB に .data を残す）。
            PlayerSettings.WebGL.dataCaching = true;

            // 例外は明示的な throw のみ拾う（既定値。サイズと速度のため）。
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            // スマホでは別タブに移ると止まってしまうので、裏でも動かす。
            PlayerSettings.runInBackground = true;

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Assets/Plugins/NuGet 以下の DLL（Unity MCP プラグインが持ち込む SignalR / Roslyn など）は
        /// エディタ用の道具でありゲーム本体からは呼ばれない。プレイヤーに混ぜると WebGL では
        /// 太るだけか、IL2CPP で落ちるので、ビルド前にエディタ専用へ落とす。
        /// CI のチェックアウト内だけの変更で、リポジトリの .meta は書き換わらない。
        /// </summary>
        static void MakeEditorOnlyPlugins()
        {
            const string root = "Assets/Plugins/NuGet";
            if (!Directory.Exists(root)) return;

            var changed = 0;
            foreach (var path in Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories))
            {
                var assetPath = path.Replace('\\', '/');
                if (!(AssetImporter.GetAtPath(assetPath) is PluginImporter importer)) continue;
                if (!importer.GetCompatibleWithAnyPlatform() && importer.GetCompatibleWithEditor()) continue;

                importer.SetCompatibleWithAnyPlatform(false);
                importer.SetCompatibleWithEditor(true);
                importer.SetCompatibleWithPlatform(BuildTarget.WebGL, false);
                importer.SaveAndReimport();
                changed++;
            }

            if (changed > 0) Log($"エディタ専用にした DLL: {changed} 件");
        }

        static string[] EnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();
        }

        static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        static void Log(string message) => Debug.Log($"[CIBuild] {message}");

        static void Fail(string message)
        {
            Debug.LogError($"[CIBuild] {message}");
            EditorApplication.Exit(1);
        }
    }
}
