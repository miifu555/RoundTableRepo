using System;
using UnityEngine;

namespace RoundTable.Net
{
    /// <summary>
    /// GitHub 経由の通信対戦の設定。トークン以外は PlayerPrefs に保存される。
    /// </summary>
    [Serializable]
    public sealed class OnlineConfig
    {
        const string KeyOwner = "RT.gh.owner";
        const string KeyRepo = "RT.gh.repo";
        const string KeyBranch = "RT.gh.branch";
        const string KeyToken = "RT.gh.token";
        const string KeyRoom = "RT.gh.room";
        const string KeyHost = "RT.gh.isHost";

        [Tooltip("リポジトリの所有者 (ユーザー名 or organization)。")]
        public string Owner = "";
        [Tooltip("リポジトリ名。プライベートで構わない。")]
        public string Repo = "";
        [Tooltip("ブランチ名。")]
        public string Branch = "main";
        [Tooltip("Personal Access Token (fine-grained / Contents: Read and write)。")]
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
            !string.IsNullOrWhiteSpace(Owner) &&
            !string.IsNullOrWhiteSpace(Repo) &&
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
            PlayerPrefs.SetString(KeyOwner, Owner ?? "");
            PlayerPrefs.SetString(KeyRepo, Repo ?? "");
            PlayerPrefs.SetString(KeyBranch, Branch ?? "main");
            PlayerPrefs.SetString(KeyToken, Token ?? "");
            PlayerPrefs.SetString(KeyRoom, RoomId ?? "");
            PlayerPrefs.SetInt(KeyHost, IsHost ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Load()
        {
            Owner = PlayerPrefs.GetString(KeyOwner, Owner);
            Repo = PlayerPrefs.GetString(KeyRepo, Repo);
            Branch = PlayerPrefs.GetString(KeyBranch, string.IsNullOrEmpty(Branch) ? "main" : Branch);
            Token = PlayerPrefs.GetString(KeyToken, Token);
            RoomId = PlayerPrefs.GetString(KeyRoom, RoomId);
            IsHost = PlayerPrefs.GetInt(KeyHost, IsHost ? 1 : 0) != 0;
        }
    }
}
