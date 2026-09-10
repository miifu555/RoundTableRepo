using RoundTable.Core;
using RoundTable.Data;
using UnityEngine;

namespace RoundTable.App
{
    /// <summary>
    /// BGM と効果音の再生。シーンをまたいで1つだけ生きる。
    /// 音源は GameDatabase が持っていて、ここは鳴らすだけ。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        const string KeyBgmVolume = "RT.audio.bgm";
        const string KeySeVolume = "RT.audio.se";

        static AudioManager _instance;

        public static AudioManager Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var go = new GameObject("RoundTableAudio");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<AudioManager>();
                return _instance;
            }
        }

        AudioSource _bgmSource;
        AudioSource _seSource;
        AudioClip _wantedBgm;

        public float BgmVolume { get; private set; } = 0.45f;
        public float SeVolume { get; private set; } = 0.85f;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            BgmVolume = PlayerPrefs.GetFloat(KeyBgmVolume, BgmVolume);
            SeVolume = PlayerPrefs.GetFloat(KeySeVolume, SeVolume);

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = BgmVolume;

            _seSource = gameObject.AddComponent<AudioSource>();
            _seSource.loop = false;
            _seSource.playOnAwake = false;

            EnsureListener();
        }

        /// <summary>
        /// AudioListener が1つも無いと音が鳴らない。
        /// シーンのカメラはコードから作っていて Listener が付いていないので、
        /// シーンをまたいで生き残るこのオブジェクトに1つだけ持たせる。
        /// </summary>
        void EnsureListener()
        {
            if (FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length > 0) return;
            gameObject.AddComponent<AudioListener>();
        }

        void Update()
        {
            // ブラウザは最初のクリックまで音を止めるので、鳴るべき BGM が
            // 止まっていたら鳴らし直す。操作した瞬間から自然に流れ始める。
            if (_wantedBgm != null && !_bgmSource.isPlaying && BgmVolume > 0f)
            {
                _bgmSource.clip = _wantedBgm;
                _bgmSource.Play();
            }
        }

        // =====================================================================
        // BGM
        // =====================================================================

        public void PlayBgm(AudioClip clip)
        {
            if (clip == null) { StopBgm(); return; }
            if (_wantedBgm == clip && _bgmSource.isPlaying) return; // 同じ曲なら流しっぱなし

            _wantedBgm = clip;
            _bgmSource.clip = clip;
            _bgmSource.volume = BgmVolume;
            _bgmSource.Play();
        }

        public void StopBgm()
        {
            _wantedBgm = null;
            _bgmSource.Stop();
        }

        /// <summary>タイトルとデッキ選択で流す曲。同じ曲なので画面が変わっても途切れない。</summary>
        public void PlayTitleBgm()
        {
            var db = GameDatabase.Instance;
            if (db != null) PlayBgm(db.BgmTitle);
        }

        public void PlayBattleBgm()
        {
            var db = GameDatabase.Instance;
            if (db != null) PlayBgm(db.BgmBattle);
        }

        // =====================================================================
        // 効果音
        // =====================================================================

        public void PlaySe(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || SeVolume <= 0f) return;
            _seSource.PlayOneShot(clip, SeVolume * volumeScale);
        }

        public void PlayButton()
        {
            var db = GameDatabase.Instance;
            if (db != null) PlaySe(db.SeButton);
        }

        /// <summary>カードを出したときの音。攻撃は叩きつけ、フィールドは滑らせて置く。</summary>
        public void PlayCard(CardKind kind)
        {
            var db = GameDatabase.Instance;
            if (db == null) return;
            PlaySe(kind == CardKind.Attack ? db.SeAttackCard : db.SeFieldCard);
        }

        public void PlayEndTurn()
        {
            var db = GameDatabase.Instance;
            if (db != null) PlaySe(db.SeEndTurn);
        }

        public void PlayResult(bool win)
        {
            var db = GameDatabase.Instance;
            if (db != null) PlaySe(win ? db.SeWin : db.SeLose);
        }

        // =====================================================================
        // 音量
        // =====================================================================

        public void SetBgmVolume(float v)
        {
            BgmVolume = Mathf.Clamp01(v);
            _bgmSource.volume = BgmVolume;
            PlayerPrefs.SetFloat(KeyBgmVolume, BgmVolume);
            PlayerPrefs.Save();
        }

        public void SetSeVolume(float v)
        {
            SeVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(KeySeVolume, SeVolume);
            PlayerPrefs.Save();
        }

        /// <summary>BGM の ON/OFF を切り替えて、切り替え後の状態 (ON なら true) を返す。</summary>
        public bool ToggleBgm()
        {
            SetBgmVolume(BgmVolume > 0f ? 0f : 0.45f);
            if (BgmVolume <= 0f) _bgmSource.Pause();
            return BgmVolume > 0f;
        }
    }
}
