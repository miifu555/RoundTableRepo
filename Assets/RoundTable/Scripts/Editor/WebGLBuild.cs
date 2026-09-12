using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RoundTable.EditorTools
{
    /// <summary>
    /// ブラウザで遊べる WebGL ビルドを作る。
    ///
    /// - メニュー「Round Table / WebGL をビルド」から実行（出力先は Build/WebGL）
    /// - CI から叩く場合は batchmode で
    ///   Unity -batchmode -quit -executeMethod RoundTable.EditorTools.WebGLBuild.BuildFromCommandLine
    ///   出力先は -buildOutput &lt;path&gt; で指定できる。
    /// </summary>
    public static class WebGLBuild
    {
        public const string DefaultOutput = "Build/WebGL";
        const string TemplateName = "PROJECT:RoundTableMobile";
        const string TemplateFolder = "Assets/WebGLTemplates/RoundTableMobile";

        [MenuItem("Round Table/WebGL をビルド", priority = 40)]
        public static void BuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Round Table",
                    $"WebGL ビルドを作ります。出力先は {DefaultOutput}。\n" +
                    "初回は10〜20分ほどかかり、その間 Unity は操作できません。",
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
                Debug.LogError("[WebGLBuild] Build Settings に有効なシーンが1つもありません。");
                return false;
            }

            // 実写など Photos フォルダの絵が割り当てられていても、WebGL は必ず公開用に戻してから焼く。
            // (Windows ビルドの直後などに Photos が残ったままここへ来ても安全なようにする、無条件の安全策)
            RoundTableArtImporter.ResetToPublicArtOnly();

            ApplyWebGLSettings();

            Directory.CreateDirectory(output);
            Debug.Log($"[WebGLBuild] 出力先: {Path.GetFullPath(output)}\n  シーン: {string.Join(", ", scenes)}");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            bool ok = summary.result == BuildResult.Succeeded;

            string text = ok
                ? $"OK  {summary.totalSize / (1024f * 1024f):0.0} MB  {summary.totalTime}  {DateTime.Now:HH:mm:ss}"
                : $"NG  result={summary.result} errors={summary.totalErrors}  {DateTime.Now:HH:mm:ss}";

            WriteResult(output, text, report);

            if (ok) Debug.Log($"[WebGLBuild] 成功: {text}\n  {Path.GetFullPath(output)}");
            else Debug.LogError($"[WebGLBuild] 失敗: {text}");
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
                    File.WriteAllText(Path.Combine(dir, "webgl-build-result.txt"), sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WebGLBuild] 結果の書き出しに失敗: " + e.Message);
            }
        }

        /// <summary>ブラウザ（特にスマホ）で動くように PlayerSettings を揃える。</summary>
        static void ApplyWebGLSettings()
        {
            // 自作テンプレート。画面いっぱいに広げ、縦持ちのときは回転を促す。
            if (Directory.Exists(TemplateFolder))
                PlayerSettings.WebGL.template = TemplateName;
            else
                Debug.LogWarning($"[WebGLBuild] {TemplateFolder} が無いので既定テンプレートを使います。");

            // 静的ホスティング（GitHub Pages など）は Content-Encoding を付けられないので、
            // gzip + JS 側での展開フォールバックにしておく（Brotli より展開が速い）。
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            // 2回目以降の起動を速くする（ブラウザの IndexedDB に .data を残す）。
            PlayerSettings.WebGL.dataCaching = true;

            // 例外は明示的な throw のみ拾う（サイズと速度のため）。
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            // 別タブに移っても止まらないように（通信対戦のポーリングが死なないため）。
            PlayerSettings.runInBackground = true;

            AssetDatabase.SaveAssets();
        }

        // NOTE: Assets/Plugins/NuGet の DLL（SignalR / Roslyn など）をエディタ専用に落として
        // プレイヤーから外す、という手は使えない。Unity MCP パッケージの Runtime アセンブリ
        // (com.IvanMurzak.Unity.MCP.Runtime) がそれらを precompiledReferences で参照しているため、
        // プレイヤー側のコンパイルが 2000 件以上の CS0234 で落ちる。
        // 本当に player から外したいなら UNITY_MCP_READY を WebGL の Scripting Define から
        // 外すことになるが、それをするとエディタ側の MCP も止まる点に注意。

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
