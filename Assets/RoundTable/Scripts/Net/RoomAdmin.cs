using System;
using System.Collections;
using System.Collections.Generic;

namespace RoundTable.Net
{
    /// <summary>1つの部屋の中身。</summary>
    public sealed class RoomInfo
    {
        public string Id;
        public readonly List<GitHubFileClient.DirEntry> Files = new List<GitHubFileClient.DirEntry>();

        public int FileCount => Files.Count;

        public string Describe()
        {
            if (Files.Count == 0) return $"{Id}  (空)";

            var names = new List<string>();
            foreach (var f in Files) names.Add(f.name);
            names.Sort(StringComparer.Ordinal);
            return $"{Id}  ({string.Join(", ", names)})";
        }
    }

    /// <summary>
    /// 対戦部屋 (rooms/{部屋名}/p0.json, p1.json) の掃除。
    ///
    /// 途中でタブを閉じるなどして残った部屋のファイルは、次の対戦の邪魔になる。
    /// とくに「ゲストが先に開始して、前回のホストのファイルを読んでしまう」と、
    /// 古い試合ID・古いシードで始まってしまい、あとから来た本物のホストと噛み合わなくなる。
    /// </summary>
    public sealed class RoomAdmin
    {
        public const string RoomsRoot = "rooms";

        readonly GitHubFileClient _client;

        public RoomAdmin(OnlineConfig config) => _client = new GitHubFileClient(config);

        /// <summary>部屋の一覧と、それぞれの中のファイルを取ってくる。</summary>
        public IEnumerator ListRooms(Action<bool, List<RoomInfo>, string> done)
        {
            bool ok = false;
            string error = null;
            List<GitHubFileClient.DirEntry> dirs = null;

            yield return _client.ListDirectory(RoomsRoot, (o, entries, err) =>
            {
                ok = o;
                dirs = entries;
                error = err;
            });

            if (!ok)
            {
                done?.Invoke(false, null, error);
                yield break;
            }

            var rooms = new List<RoomInfo>();
            foreach (var dir in dirs)
            {
                if (!dir.IsDirectory) continue;

                var room = new RoomInfo { Id = dir.name };
                List<GitHubFileClient.DirEntry> files = null;
                yield return _client.ListDirectory(dir.path, (o, entries, _) =>
                {
                    if (o && entries != null) files = entries;
                });

                if (files != null)
                    foreach (var f in files)
                        if (!f.IsDirectory) room.Files.Add(f);

                rooms.Add(room);
            }

            rooms.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            done?.Invoke(true, rooms, null);
        }

        /// <summary>
        /// 部屋の中のファイルを全部消す。
        /// GitHub にディレクトリを消す API は無いので、中身が空になれば部屋ごと消えたことになる。
        /// </summary>
        public IEnumerator DeleteRoom(string roomId, Action<bool, int, string> done)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                done?.Invoke(false, 0, "部屋名が空です");
                yield break;
            }

            string path = $"{RoomsRoot}/{OnlineConfig.Sanitize(roomId)}";

            bool ok = false;
            string error = null;
            List<GitHubFileClient.DirEntry> files = null;

            yield return _client.ListDirectory(path, (o, entries, err) =>
            {
                ok = o;
                files = entries;
                error = err;
            });

            if (!ok)
            {
                done?.Invoke(false, 0, error);
                yield break;
            }
            if (files == null || files.Count == 0)
            {
                done?.Invoke(true, 0, null); // もう無い
                yield break;
            }

            int deleted = 0;
            foreach (var f in files)
            {
                if (f.IsDirectory) continue;

                FileResult res = default;
                yield return _client.Delete(f.path, f.sha, $"Remove stale room file {f.path}", r => res = r);

                if (!res.Ok)
                {
                    done?.Invoke(false, deleted, $"{f.name}: {res.Error}");
                    yield break;
                }
                deleted++;
            }

            done?.Invoke(true, deleted, null);
        }
    }
}
