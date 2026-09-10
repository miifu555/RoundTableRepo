using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace RoundTable.App
{
    /// <summary>
    /// デバッグメニューの鍵。開発者コードが合っていれば開く。
    ///
    /// ⚠️ これは「他人が触れないようにする」ためのものではなく、
    /// 「普通に遊んでいる人が誤って踏まないようにする」ための目隠しでしかない。
    /// ブラウザ版はビルドの中身を誰でも取り出せるので、ここに焼き込まれた
    /// ハッシュも取り出せるし、短いコードなら総当たりで割れる。
    /// 実際の権限は GitHub のトークンが握っていて、遊ぶ人は全員そのトークンを
    /// 持っている（持っていないと対戦できない）ので、その気になれば
    /// このメニューを通さなくてもルームは消せる。
    /// </summary>
    public static class DevAccess
    {
        /// <summary>
        /// コードのハッシュを置く場所。
        /// Assets/RoundTable/Resources/RoundTableDevCode.txt に 64桁の16進を1行。
        /// エディタメニュー「Round Table / 開発者コードを設定」で作る。.gitignore 済み。
        /// </summary>
        public const string CodeResourceName = "RoundTableDevCode";

        const string Salt = "RoundTable-dev-v1:";

        static string _hash;
        static bool _loaded;

        /// <summary>ハッシュが焼き込まれているか。無ければデバッグメニューは表示しない。</summary>
        public static bool IsConfigured
        {
            get
            {
                Load();
                return !string.IsNullOrEmpty(_hash);
            }
        }

        /// <summary>解除済みか。ゲームを起動し直すと閉じる。</summary>
        public static bool Unlocked { get; private set; }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            var asset = Resources.Load<TextAsset>(CodeResourceName);
            _hash = asset != null ? asset.text.Trim().ToLowerInvariant() : "";
            if (asset != null) Resources.UnloadAsset(asset);
        }

        /// <summary>コードを照合して、合っていれば解除する。</summary>
        public static bool TryUnlock(string code)
        {
            Load();
            if (string.IsNullOrEmpty(_hash) || string.IsNullOrEmpty(code)) return false;

            Unlocked = FixedTimeEquals(Hash(code), _hash);
            return Unlocked;
        }

        public static void Lock() => Unlocked = false;

        /// <summary>コード → 保存するハッシュ。エディタ側の設定窓からも使う。</summary>
        public static string Hash(string code)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Salt + (code ?? "").Trim()));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>長さと中身の一致を、途中で抜けずに比べる。</summary>
        static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
