using System;
using System.Collections;
using System.Collections.Generic;
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
            // トークンが無いときは付けない。空の Bearer は必ず 401 になるが、
            // 公開リポジトリなら未認証の GET は通る (読むだけのデバッグ用途で効く)。
            var token = _cfg.EffectiveToken;
            if (!string.IsNullOrWhiteSpace(token))
                req.SetRequestHeader("Authorization", "Bearer " + token);

            req.SetRequestHeader("Accept", "application/vnd.github+json");
            req.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

            // WebGL ではブラウザの CORS 制約を受ける。
            // Cache-Control は GitHub の Access-Control-Allow-Headers に無いため、
            // 付けるとプリフライトで全リクエストが弾かれる
            // (キャッシュ回避は ContentsUrl のクエリ文字列で行っている)。
            // User-Agent はブラウザが自動で付けるので、設定しても警告が出るだけ。
#if !UNITY_WEBGL || UNITY_EDITOR
            req.SetRequestHeader("User-Agent", "RoundTable-Unity");
            req.SetRequestHeader("Cache-Control", "no-cache");
#endif
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
                if (req.responseCode == 404) { done(false, $"リポジトリ {OnlineConfig.RepoDisplay} が見つかりません (404)。トークンの Repository access にこのリポジトリが入っているか、Private ならコラボレーター招待を受けているか確認してください"); yield break; }
                if (req.result != UnityWebRequest.Result.Success) { done(false, Describe(req)); yield break; }

                // GET が通っても書き込み権限が無いと、対戦開始の PUT で 403 になる。
                // リポジトリ情報の permissions.push でその場で判別する。
                RepoResponse repo = null;
                try { repo = JsonUtility.FromJson<RepoResponse>(req.downloadHandler.text); }
                catch { /* 解析できなければ権限チェックは諦めて続行する */ }

                if (repo != null && repo.permissions != null && !repo.permissions.push)
                {
                    done(false, $"{OnlineConfig.RepoDisplay} への書き込み権限がありません。トークンの Repository permissions で Contents を Read and write にしてください");
                    yield break;
                }

                done(true, "接続OK");
            }
        }

        // =====================================================================
        // 一覧と削除 (デバッグメニューのルーム掃除で使う)
        // =====================================================================

        /// <summary>ディレクトリの中身を1階層ぶん取る。無ければ空リストで Ok=true。</summary>
        public IEnumerator ListDirectory(string path, Action<bool, List<DirEntry>, string> done)
        {
            using (var req = UnityWebRequest.Get(ContentsUrl(path, true)))
            {
                AddHeaders(req);
                yield return req.SendWebRequest();

                if (req.responseCode == 404)
                {
                    done?.Invoke(true, new List<DirEntry>(), null);
                    yield break;
                }
                if (req.result != UnityWebRequest.Result.Success)
                {
                    done?.Invoke(false, null, Describe(req));
                    yield break;
                }

                List<DirEntry> entries;
                try
                {
                    // ディレクトリは JSON 配列で返る。JsonUtility は配列を直接読めないので包む。
                    var wrapped = "{\"items\":" + req.downloadHandler.text + "}";
                    var parsed = JsonUtility.FromJson<DirListing>(wrapped);
                    entries = parsed?.items != null ? new List<DirEntry>(parsed.items) : new List<DirEntry>();
                }
                catch (Exception e)
                {
                    done?.Invoke(false, null, "一覧の解析に失敗: " + e.Message);
                    yield break;
                }

                done?.Invoke(true, entries, null);
            }
        }

        /// <summary>ファイルを1つ消す。sha は ListDirectory / Get で取れるもの。</summary>
        public IEnumerator Delete(string path, string sha, string commitMessage, Action<FileResult> done)
        {
            var body = new StringBuilder();
            body.Append('{');
            body.Append("\"message\":").Append(JsonString(commitMessage)).Append(',');
            body.Append("\"sha\":").Append(JsonString(sha)).Append(',');
            body.Append("\"branch\":").Append(JsonString(_cfg.Branch));
            body.Append('}');

            using (var req = new UnityWebRequest(ContentsUrl(path, false), "DELETE"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString()));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                AddHeaders(req);

                yield return req.SendWebRequest();

                done?.Invoke(req.result == UnityWebRequest.Result.Success
                    ? new FileResult { Ok = true }
                    : FileResult.Fail(Describe(req), req.responseCode));
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
        class RepoResponse
        {
            public PermissionSet permissions;

            [Serializable]
            public class PermissionSet
            {
                public bool admin;
                public bool push;
                public bool pull;
            }
        }

        /// <summary>ディレクトリ一覧の1件。</summary>
        [Serializable]
        public struct DirEntry
        {
            public string name;
            public string path;
            /// <summary>"file" か "dir"。</summary>
            public string type;
            public string sha;

            public bool IsDirectory => type == "dir";
        }

        [Serializable]
        class DirListing
        {
            public DirEntry[] items;
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
