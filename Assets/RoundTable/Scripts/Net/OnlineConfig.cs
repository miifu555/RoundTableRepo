using System;
using UnityEngine;

namespace RoundTable.Net
{
    /// <summary>
    /// GitHub 経由の通信対戦の設定。
    /// 少人数の身内でしか使わないので、**使うリポジトリは固定**にしてある
    /// （下の Fixed～ を書き換えれば移せる）。プレイヤーごとに違うのはトークン・部屋名・
    /// ホストかどうかの3つだけで、トークン以外は PlayerPrefs に保存される。
    /// </summary>
    [Serializable]
    public sealed class OnlineConfig
    {
        // ---- 固定値（対戦に使う郵便受け） ----
        public const string FixedOwner = "miifu555";
        public const string FixedRepo = "RoundTableLobby";
        public const string FixedBranch = "main";

        /// <summary>
        /// ビルドに焼き込む共有トークンの置き場所。
        /// Assets/RoundTable/Resources/RoundTableToken.txt に github_pat_... を1行書いておくと、
        /// プレイヤーはトークンを入力しなくてよくなる（ファイルは .gitignore 済み。リポジトリには入れないこと）。
        /// </summary>
        public const string TokenResourceName = "RoundTableToken";

        const string KeyToken = "RT.gh.token";
        const string KeyRoom = "RT.gh.room";
        const string KeyHost = "RT.gh.isHost";

        // 旧バージョンが保存していたキー。もう読まないので起動時に掃除する。
        static readonly string[] ObsoleteKeys = { "RT.gh.owner", "RT.gh.repo", "RT.gh.branch" };

        /// <summary>リポジトリの所有者。固定。</summary>
        public string Owner => FixedOwner;
        /// <summary>リポジトリ名。固定。</summary>
        public string Repo => FixedRepo;
        /// <summary>ブランチ名。固定。</summary>
        public string Branch => FixedBranch;
        /// <summary>画面に出すための "所有者/リポジトリ名"。</summary>
        public static string RepoDisplay => $"{FixedOwner}/{FixedRepo}";

        [Tooltip("Personal Access Token (fine-grained / Contents: Read and write)。焼き込みトークンが無いときだけ使う。")]
        public string Token = "";
        [Tooltip("部屋名。対戦する2人で同じ文字列にする。")]
        public string RoomId = "room1";
        [Tooltip("ホスト = プレイヤー0。片方だけ ON にする。")]
        public bool IsHost = true;

        [Tooltip("相手のファイルを見に行く間隔(秒)。短くすると反応が早いがAPI消費が増える。")]
        public float PollIntervalSeconds = 4f;

        static string _builtInToken;
        static bool _builtInLoaded;

        /// <summary>ビルドに焼き込まれた共有トークン。無ければ空。</summary>
        public static string BuiltInToken
        {
            get
            {
                if (_builtInLoaded) return _builtInToken;
                _builtInLoaded = true;
                var asset = Resources.Load<TextAsset>(TokenResourceName);
                _builtInToken = asset == null ? "" : asset.text.Trim();
                return _builtInToken;
            }
        }

        /// <summary>共有トークンが焼き込まれているか。true なら画面での入力は不要。</summary>
        public static bool HasBuiltInToken => !string.IsNullOrWhiteSpace(BuiltInToken);

        /// <summary>実際に通信で使うトークン。焼き込みがあればそちらを優先する。</summary>
        public string EffectiveToken => HasBuiltInToken ? BuiltInToken : Token;

        public int LocalRole => IsHost ? 0 : 1;
        public int RemoteRole => IsHost ? 1 : 0;

        public string LocalPath => $"rooms/{Sanitize(RoomId)}/p{LocalRole}.json";
        public string RemotePath => $"rooms/{Sanitize(RoomId)}/p{RemoteRole}.json";

        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(EffectiveToken) &&
            !string.IsNullOrWhiteSpace(RoomId);

        public static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "room";
            var sb = new System.Text.StringBuilder();
            foreach (var c in s)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
                else sb.Append('_');
            }
            return sb.Length == 0 ? "room" : sb.ToString();
        }

        public void Save()
        {
            // 焼き込みトークンを使っているときは、端末側に控えを残さない
            PlayerPrefs.SetString(KeyToken, HasBuiltInToken ? "" : (Token ?? ""));
            PlayerPrefs.SetString(KeyRoom, RoomId ?? "");
            PlayerPrefs.SetInt(KeyHost, IsHost ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Load()
        {
            Token = PlayerPrefs.GetString(KeyToken, Token);
            RoomId = PlayerPrefs.GetString(KeyRoom, RoomId);
            IsHost = PlayerPrefs.GetInt(KeyHost, IsHost ? 1 : 0) != 0;

            foreach (var key in ObsoleteKeys)
                if (PlayerPrefs.HasKey(key)) PlayerPrefs.DeleteKey(key);
        }
    }
}
