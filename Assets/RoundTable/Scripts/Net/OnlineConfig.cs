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

        [Tooltip("Personal Access Token (fine-grained / Contents: Read and write)。人ごとに違う。")]
        public string Token = "";
        [Tooltip("部屋名。対戦する2人で同じ文字列にする。")]
        public string RoomId = "room1";
        [Tooltip("ホスト = プレイヤー0。片方だけ ON にする。")]
        public bool IsHost = true;

        [Tooltip("相手のファイルを見に行く間隔(秒)。短くすると反応が早いがAPI消費が増える。")]
        public float PollIntervalSeconds = 2f;

        public int LocalRole => IsHost ? 0 : 1;
        public int RemoteRole => IsHost ? 1 : 0;

        public string LocalPath => $"rooms/{Sanitize(RoomId)}/p{LocalRole}.json";
        public string RemotePath => $"rooms/{Sanitize(RoomId)}/p{RemoteRole}.json";

        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(Token) &&
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
            PlayerPrefs.SetString(KeyToken, Token ?? "");
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
