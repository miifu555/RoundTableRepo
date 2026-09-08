using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RoundTable.Net
{
    /// <summary>ファイル1件の取得/書き込み結果。</summary>
    public struct FileResult
    {
        public bool Ok;
        public bool NotFound;
        public string Text;
        public string Sha;
        public string Error;
        public long Status;

        public static FileResult Fail(string error, long status = 0)
            => new FileResult { Ok = false, Error = error, Status = status };
    }

    /// <summary>
    /// GitHub Contents API で、リポジトリ内の1ファイルを読み書きするだけの薄いクライアント。
    /// 通信対戦では「自分は自分のファイルだけ書き、相手のファイルだけ読む」ので書き込み衝突が起きない。
    /// </summary>
    public sealed class GitHubFileClient
    {
        const string ApiRoot = "https://api.github.com";
        const int TimeoutSeconds = 20;

        readonly OnlineConfig _cfg;

        public GitHubFileClient(OnlineConfig config) => _cfg = config;

        string ContentsUrl(string path, bool cacheBust)
        {
            var url = $"{ApiRoot}/repos/{_cfg.Owner}/{_cfg.Repo}/contents/{Uri.EscapeDataString(path).Replace("%2F", "/")}";
            if (cacheBust)
                url += $"?ref={Uri.EscapeDataString(_cfg.Branch)}&_={DateTime.UtcNow.Ticks}";
            return url;
        }

        void AddHeaders(UnityWebRequest req)
        {
            req.SetRequestHeader("Authorization", "Bearer " + _cfg.Token);
            req.SetRequestHeader("Accept", "application/vnd.github+json");
            req.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
            req.SetRequestHeader("User-Agent", "RoundTable-Unity");
            req.SetRequestHeader("Cache-Control", "no-cache");
            req.timeout = TimeoutSeconds;
        }

        // =====================================================================
        // 読み込み
        // =====================================================================

        public IEnumerator Get(string path, Action<FileResult> done)
        {
            using (var req = UnityWebRequest.Get(ContentsUrl(path, true)))
            {
                AddHeaders(req);
                yield return req.SendWebRequest();

                if (req.responseCode == 404)
                {
                    done?.Invoke(new FileResult { Ok = true, NotFound = true, Status = 404 });
                    yield break;
                }
                if (req.result != UnityWebRequest.Result.Success)
                {
                    done?.Invoke(FileResult.Fail(Describe(req), req.responseCode));
                    yield break;
                }

                ContentsResponse parsed;
                try { parsed = JsonUtility.FromJson<ContentsResponse>(req.downloadHandler.text); }
                catch (Exception e) { done?.Invoke(FileResult.Fail("応答の解析に失敗: " + e.Message)); yield break; }

                string text;
                try { text = Encoding.UTF8.GetString(Convert.FromBase64String(parsed.content ?? "")); }
                catch (Exception e) { done?.Invoke(FileResult.Fail("Base64 の復号に失敗: " + e.Message)); yield break; }

                done?.Invoke(new FileResult { Ok = true, Text = text, Sha = parsed.sha });
            }
        }

        // =====================================================================
        // 書き込み
        // =====================================================================

        /// <summary>ファイルを丸ごと置き換える。sha が古いと 409 になるので1度だけ取り直して再試行する。</summary>
        public IEnumerator Put(string path, string text, string sha, string commitMessage, Action<FileResult> done)
        {
            FileResult result = default;
            yield return PutOnce(path, text, sha, commitMessage, r => result = r);

            if (!result.Ok && (result.Status == 409 || result.Status == 422))
            {
                FileResult latest = default;
                yield return Get(path, r => latest = r);
                if (latest.Ok)
                    yield return PutOnce(path, text, latest.NotFound ? null : latest.Sha, commitMessage, r => result = r);
            }

            done?.Invoke(result);
        }

        IEnumerator PutOnce(string path, string text, string sha, string commitMessage, Action<FileResult> done)
        {
            var body = new StringBuilder();
            body.Append('{');
            body.Append("\"message\":").Append(JsonString(commitMessage)).Append(',');
            body.Append("\"content\":").Append(JsonString(Convert.ToBase64String(Encoding.UTF8.GetBytes(text ?? "")))).Append(',');
            body.Append("\"branch\":").Append(JsonString(_cfg.Branch));
            if (!string.IsNullOrEmpty(sha))
                body.Append(",\"sha\":").Append(JsonString(sha));
            body.Append('}');

            using (var req = new UnityWebRequest(ContentsUrl(path, false), "PUT"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString()));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                AddHeaders(req);

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    done?.Invoke(FileResult.Fail(Describe(req), req.responseCode));
                    yield break;
                }

                string newSha = null;
                try
                {
                    var parsed = JsonUtility.FromJson<CommitResponse>(req.downloadHandler.text);
                    newSha = parsed?.content?.sha;
                }
                catch { /* sha が取れなくても次回 Get で拾い直せる */ }

                done?.Invoke(new FileResult { Ok = true, Sha = newSha });
            }
        }

        // =====================================================================

        /// <summary>設定が正しいか (トークン・リポジトリ・書き込み権限) をざっくり確認する。</summary>
        public IEnumerator TestConnection(Action<bool, string> done)
        {
            using (var req = UnityWebRequest.Get($"{ApiRoot}/repos/{_cfg.Owner}/{_cfg.Repo}"))
            {
                AddHeaders(req);
                yield return req.SendWebRequest();

                if (req.responseCode == 401) { done(false, "トークンが無効です (401)"); yield break; }
                if (req.responseCode == 403) { done(false, "権限がありません (403)。Contents: Read and write を許可してください"); yield break; }
                if (req.responseCode == 404) { done(false, "リポジトリが見つかりません (404)。所有者/リポジトリ名、またはトークンのリポジトリ許可を確認してください"); yield break; }
                if (req.result != UnityWebRequest.Result.Success) { done(false, Describe(req)); yield break; }

                done(true, "接続OK");
            }
        }

        static string Describe(UnityWebRequest req)
        {
            var body = req.downloadHandler != null ? req.downloadHandler.text : null;
            if (!string.IsNullOrEmpty(body) && body.Length > 300) body = body.Substring(0, 300);
            return $"HTTP {req.responseCode} {req.error} {body}";
        }

        static string JsonString(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        [Serializable]
        class ContentsResponse
        {
            public string content;
            public string sha;
        }

        [Serializable]
        class CommitResponse
        {
            public ContentEntry content;

            [Serializable]
            public class ContentEntry { public string sha; }
        }
    }
}
