"""Round Table の BGM と効果音を合成して Assets/RoundTable/Audio に書き出す。

    python Tools/make_audio.py

外部の音源を使わず numpy だけで作っている。作り直したくなったらこのファイルの
数値をいじって実行し直せばよい。Notion の「効果音」の項に対応:

  BGM  : タイトル/デッキ選択, ゲームプレイ
  SE   : ボタン, 攻撃カード使用, フィールドカード使用, ターン終了, 勝利, 敗北

カード関係の音は「実際に紙のカードを扱っている」感じを狙って、
帯域を絞ったノイズの短いバーストと低い胴鳴りを重ねて作っている。
"""

import os
import wave

import numpy as np

SR = 44100
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "RoundTable", "Audio")

rng = np.random.default_rng(20260910)


# ---------------------------------------------------------------- 基本の道具

def silence(seconds):
    return np.zeros(int(SR * seconds))


def noise(seconds):
    return rng.uniform(-1.0, 1.0, int(SR * seconds))


def fft_filter(x, low=None, high=None, slope=0.25):
    """FFT でざっくり帯域を絞る。slope は遷移の緩さ(オクターブ相当)。"""
    n = len(x)
    if n == 0:
        return x
    spec = np.fft.rfft(x)
    freq = np.fft.rfftfreq(n, 1.0 / SR)
    gain = np.ones_like(freq)

    if low is not None:
        # low 以下をなだらかに落とす
        with np.errstate(divide="ignore"):
            g = 1.0 / (1.0 + (low / np.maximum(freq, 1e-6)) ** (2.0 / slope))
        gain *= g
    if high is not None:
        gain *= 1.0 / (1.0 + (freq / high) ** (2.0 / slope))

    return np.fft.irfft(spec * gain, n)


def env_ad(seconds, attack, decay, curve=2.5):
    """アタックとディケイだけの単純な包絡。"""
    n = int(SR * seconds)
    t = np.arange(n) / SR
    a = int(SR * attack)
    e = np.zeros(n)
    if a > 0:
        e[:a] = np.linspace(0.0, 1.0, a) ** 0.6
    e[a:] = np.exp(-curve * (t[a:] - t[a] if a < n else 0) / max(decay, 1e-4))
    return e


def place(dst, src, at_seconds, gain=1.0):
    """dst の指定位置に src を足し込む。はみ出したぶんは切る。"""
    i = int(SR * at_seconds)
    n = min(len(src), len(dst) - i)
    if n > 0:
        dst[i:i + n] += src[:n] * gain
    return dst


def tone(seconds, freq, kind="sine", detune=0.0):
    t = np.arange(int(SR * seconds)) / SR
    f = freq * (1.0 + detune)
    if kind == "sine":
        return np.sin(2 * np.pi * f * t)
    if kind == "tri":
        return 2.0 / np.pi * np.arcsin(np.sin(2 * np.pi * f * t))
    if kind == "saw":
        return 2.0 * ((f * t) % 1.0) - 1.0
    if kind == "square":
        return np.sign(np.sin(2 * np.pi * f * t))
    raise ValueError(kind)


def note(midi):
    return 440.0 * 2 ** ((midi - 69) / 12.0)


def soft_clip(x, drive=1.0):
    return np.tanh(x * drive)


def normalize(x, peak=0.9):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 1e-9 else x


def write_wav(name, data, stereo=False):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.normpath(os.path.join(OUT_DIR, name))

    if stereo:
        left, right = data
        frames = np.stack([left, right], axis=1)
    else:
        frames = data.reshape(-1, 1)

    frames = np.clip(frames, -1.0, 1.0)
    pcm = (frames * 32767.0).astype("<i2")

    with wave.open(path, "wb") as w:
        w.setnchannels(frames.shape[1])
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())

    size_kb = os.path.getsize(path) / 1024.0
    print(f"  {name:24} {frames.shape[0] / SR:5.2f}s  {size_kb:7.1f} KB")


# ---------------------------------------------------------------- カードの音

def card_slide(seconds=0.075, low=1800, high=7000, roughness=0.0):
    """カードが指や別のカードを擦る音。帯域を絞ったノイズ。"""
    n = noise(seconds)
    if roughness > 0:
        # ざらつき: 細かい振幅ゆらぎ
        wob = 1.0 + roughness * rng.uniform(-1, 1, len(n))
        n = n * wob
    n = fft_filter(n, low=low, high=high)
    return n * env_ad(seconds, 0.004, seconds * 0.42)


def card_snap(seconds=0.055):
    """指で弾いた「パシッ」。高域寄りで立ち上がりが速い。"""
    n = fft_filter(noise(seconds), low=2600, high=11000)
    return n * env_ad(seconds, 0.0012, seconds * 0.22, curve=4.0)


def table_thud(seconds=0.16, freq=110.0, amount=1.0):
    """机に当たる低い胴鳴り。"""
    t = np.arange(int(SR * seconds)) / SR
    body = np.sin(2 * np.pi * freq * t * (1.0 - 0.25 * t / seconds))
    body *= np.exp(-26.0 * t)
    knock = fft_filter(noise(seconds), low=200, high=1600) * env_ad(seconds, 0.001, 0.03, curve=5.0)
    return (body * 0.85 + knock * 0.5) * amount


def riffle(seconds=0.34, count=16, low=1500, high=9000):
    """カードを繰るパラパラ音。細かいクリックを散らす。"""
    out = silence(seconds)
    for i in range(count):
        at = (i / count) * seconds * 0.92 + rng.uniform(0, 0.006)
        click = fft_filter(noise(0.02), low=low, high=high)
        click *= env_ad(0.02, 0.0008, 0.006, curve=5.0)
        place(out, click, at, gain=rng.uniform(0.45, 1.0))
    return out


# ---------------------------------------------------------------- SE

def se_button():
    """タイトル/デッキ選択のボタン。小さくて硬いクリック。"""
    dur = 0.10
    out = silence(dur)
    place(out, card_slide(0.045, low=1600, high=5200), 0.0, 0.55)
    blip = tone(0.05, 1180, "sine") * env_ad(0.05, 0.001, 0.014, curve=4.0)
    place(out, blip, 0.002, 0.35)
    place(out, table_thud(0.06, 220, 0.25), 0.004)
    return normalize(out, 0.55)


def se_card_attack():
    """攻撃カード使用。手札から抜いて弾き、机に叩きつける。"""
    dur = 0.46
    out = silence(dur)
    # 1) 手札から抜く
    place(out, card_slide(0.085, low=1900, high=7200, roughness=0.25), 0.0, 0.75)
    # 2) 指で弾く
    place(out, card_snap(0.05), 0.085, 0.95)
    # 3) 机に叩きつける
    place(out, card_slide(0.05, low=1200, high=6000), 0.15, 0.7)
    place(out, table_thud(0.20, 104, 1.0), 0.155)
    # 4) 余韻 (机の鳴り)
    tail = fft_filter(noise(0.20), low=90, high=700) * env_ad(0.20, 0.004, 0.05, curve=3.0)
    place(out, tail, 0.165, 0.22)
    return normalize(out, 0.88)


def se_card_field():
    """フィールドカード使用。滑らせて静かに置く。"""
    dur = 0.52
    out = silence(dur)
    # 1) 抜く
    place(out, card_slide(0.07, low=1700, high=6200, roughness=0.2), 0.0, 0.6)
    # 2) 机の上を滑らせる (長めのこすれ)
    slide = fft_filter(noise(0.22), low=900, high=4200)
    slide *= np.linspace(0.25, 1.0, len(slide)) ** 1.5 * np.exp(-2.2 * np.arange(len(slide)) / SR)
    place(out, slide, 0.075, 0.5)
    # 3) そっと置く
    place(out, card_slide(0.045, low=800, high=4000), 0.245, 0.5)
    place(out, table_thud(0.18, 88, 0.6), 0.25)
    return normalize(out, 0.72)


def se_end_turn():
    """ターン終了。カードの束を机で揃えて、確認の低い音。"""
    dur = 0.70
    out = silence(dur)
    for i, at in enumerate((0.0, 0.075)):
        place(out, card_slide(0.05, low=1400, high=6000), at, 0.55)
        place(out, table_thud(0.12, 130 - i * 12, 0.55), at + 0.004)
    for i, (m, at) in enumerate(((69, 0.20), (64, 0.30))):  # A4 -> E4
        v = (tone(0.42, note(m), "sine") * 0.7 + tone(0.42, note(m) * 2, "sine") * 0.16)
        v *= env_ad(0.42, 0.012, 0.16, curve=2.6)
        place(out, v, at, 0.5)
    return normalize(out, 0.7)


def se_win():
    """勝利。カードを掻き集めてから明るいアルペジオ。"""
    dur = 1.9
    out = silence(dur)
    place(out, riffle(0.28, 14), 0.0, 0.45)
    for i, m in enumerate((72, 76, 79, 84)):  # C5 E5 G5 C6
        at = 0.22 + i * 0.105
        v = (tone(1.1, note(m), "tri") * 0.6 + tone(1.1, note(m) * 2.005, "sine") * 0.25)
        v *= env_ad(1.1, 0.004, 0.30 + i * 0.09, curve=2.2)
        place(out, v, at, 0.55)
    # 最後に和音を重ねる
    chord = sum(tone(1.3, note(m), "tri") for m in (72, 76, 79, 84)) / 4.0
    chord *= env_ad(1.3, 0.02, 0.55, curve=2.0)
    place(out, chord, 0.60, 0.42)
    return normalize(out, 0.82)


def se_lose():
    """敗北。カードが散って、暗く下がる。"""
    dur = 2.0
    out = silence(dur)
    place(out, riffle(0.30, 9, low=900, high=5200), 0.0, 0.4)
    place(out, table_thud(0.28, 76, 0.8), 0.12)
    for i, m in enumerate((69, 65, 62, 57)):  # A4 F4 D4 A3
        at = 0.20 + i * 0.155
        v = (tone(1.4, note(m), "tri") * 0.5 + tone(1.4, note(m) * 0.5, "sine") * 0.35)
        v = fft_filter(v, high=2600)
        v *= env_ad(1.4, 0.012, 0.34 + i * 0.10, curve=2.0)
        place(out, v, at, 0.55)
    return normalize(out, 0.72)


# ---------------------------------------------------------------- BGM

def loop_wrap(buf, loop_len):
    """loop_len より後ろに伸びた余韻を先頭に折り返して、継ぎ目のないループにする。"""
    head = buf[:loop_len].copy()
    tail = buf[loop_len:]
    n = min(len(tail), loop_len)
    if n > 0:
        head[:n] += tail[:n]
    return head


def pluck(seconds, freq, decay, kind="tri", bright=3000):
    v = tone(seconds, freq, kind) * 0.6 + tone(seconds, freq * 2.001, "sine") * 0.25
    v = fft_filter(v, high=bright)
    return v * env_ad(seconds, 0.005, decay, curve=2.2)


def pad(seconds, freqs, cutoff=1500):
    v = np.zeros(int(SR * seconds))
    for f in freqs:
        v += tone(seconds, f, "saw", detune=+0.0016)
        v += tone(seconds, f, "saw", detune=-0.0019)
    v /= max(len(freqs) * 2, 1)
    v = fft_filter(v, high=cutoff)
    n = len(v)
    e = np.ones(n)
    a = int(SR * 0.35)
    e[:a] = np.linspace(0, 1, a) ** 1.5
    r = int(SR * 0.45)
    e[-r:] = np.linspace(1, 0, r) ** 1.2
    return v * e


def bgm_title():
    """タイトル / デッキ選択。落ち着いていて少し思わせぶりに。"""
    bpm = 84.0
    beat = 60.0 / bpm
    bar = beat * 4
    bars = 8
    loop_len = int(SR * bar * bars)
    buf = np.zeros(loop_len + int(SR * 3.0))

    # Am7 - Fmaj7 - Cmaj7 - E7  を2周
    prog = [
        (57, (57, 60, 64, 67)),   # Am7
        (53, (53, 57, 60, 64)),   # Fmaj7
        (48, (52, 55, 59, 64)),   # Cmaj7
        (52, (52, 56, 59, 62)),   # E7
    ]

    for b in range(bars):
        root, chord = prog[b % 4]
        at = b * bar

        # パッド
        place(buf, pad(bar * 1.06, [note(m) for m in chord], cutoff=1250), at, 0.30)
        # ベース
        place(buf, pluck(bar * 0.9, note(root - 12), decay=0.55, kind="sine", bright=600), at, 0.55)
        place(buf, pluck(beat * 1.4, note(root - 12), decay=0.30, kind="sine", bright=600), at + beat * 2.5, 0.32)

        # アルペジオ (8分)
        seq = [chord[0], chord[2], chord[1], chord[3], chord[2], chord[1], chord[3], chord[2]]
        for i, m in enumerate(seq):
            gain = 0.30 if i % 2 == 0 else 0.20
            place(buf, pluck(beat * 1.6, note(m + 12), decay=0.28), at + i * beat * 0.5, gain)

        # 拍の頭に小さなリムショット風
        tick = fft_filter(noise(0.03), low=2200, high=8000) * env_ad(0.03, 0.001, 0.008, curve=5.0)
        place(buf, tick, at + beat * 2, 0.10)

    buf = loop_wrap(buf, loop_len)
    buf = soft_clip(buf * 1.15, 1.0) * 0.72

    # 軽いステレオ (左右で少しだけ遅らせて広げる)
    d = int(SR * 0.011)
    left = buf.copy()
    right = np.concatenate([np.zeros(d), buf[:-d]]) * 0.92 + buf * 0.35
    return normalize(left, 0.72), normalize(right, 0.72)


def bgm_battle():
    """ゲームプレイ中。前に進む感じで、少しだけ落ち着かない。"""
    bpm = 132.0
    beat = 60.0 / bpm
    bar = beat * 4
    bars = 16
    loop_len = int(SR * bar * bars)
    buf = np.zeros(loop_len + int(SR * 2.5))

    prog = [
        (45, (57, 60, 64)),   # Am
        (41, (57, 60, 65)),   # F
        (43, (55, 59, 62)),   # G
        (40, (55, 59, 64)),   # Em
    ]

    for b in range(bars):
        root, chord = prog[b % 4]
        at = b * bar

        # キック (1拍目と3拍目の裏)
        for k in (0.0, beat * 1.5, beat * 2.0, beat * 3.5):
            t = np.arange(int(SR * 0.14)) / SR
            kick = np.sin(2 * np.pi * (110 * np.exp(-24 * t) + 44) * t) * np.exp(-19 * t)
            place(buf, kick, at + k, 0.55)

        # ハイハット (8分)
        for i in range(8):
            hh = fft_filter(noise(0.035), low=5500, high=13000)
            hh *= env_ad(0.035, 0.0008, 0.010 if i % 2 else 0.016, curve=5.0)
            place(buf, hh, at + i * beat * 0.5, 0.13 if i % 2 else 0.19)

        # スネア風 (2, 4拍)
        for s in (beat, beat * 3):
            sn = fft_filter(noise(0.14), low=900, high=6500) * env_ad(0.14, 0.001, 0.05, curve=3.5)
            sn += tone(0.14, 190, "tri") * env_ad(0.14, 0.001, 0.03, curve=4.0) * 0.4
            place(buf, sn, at + s, 0.30)

        # ベース (8分の刻み)
        for i in range(8):
            f = note(root - 12) * (1.0 if i % 4 != 3 else 1.5)
            bs = tone(beat * 0.55, f, "saw")
            bs = fft_filter(bs, high=900)
            bs *= env_ad(beat * 0.55, 0.004, beat * 0.20, curve=3.0)
            place(buf, bs, at + i * beat * 0.5, 0.34)

        # コードの刻み (裏拍)
        for i in (1, 3, 5, 7):
            st = sum(tone(beat * 0.4, note(m), "saw") for m in chord) / len(chord)
            st = fft_filter(st, low=250, high=3200)
            st *= env_ad(beat * 0.4, 0.004, beat * 0.12, curve=3.5)
            place(buf, st, at + i * beat * 0.5, 0.20)

        # 8小節目以降はメロディを足して単調さを避ける
        if b >= 8:
            mel = [(chord[2] + 12, 0.0), (chord[1] + 12, beat * 1.0),
                   (chord[2] + 12, beat * 1.5), (chord[0] + 12, beat * 2.5)]
            for m, off in mel:
                place(buf, pluck(beat * 1.2, note(m), decay=beat * 0.5, bright=4200), at + off, 0.26)

    buf = loop_wrap(buf, loop_len)
    buf = soft_clip(buf * 1.25, 1.0) * 0.78

    d = int(SR * 0.008)
    left = buf.copy()
    right = np.concatenate([np.zeros(d), buf[:-d]]) * 0.9 + buf * 0.4
    return normalize(left, 0.78), normalize(right, 0.78)


# ---------------------------------------------------------------- 実行

def main():
    print("SE:")
    write_wav("se_button.wav", se_button())
    write_wav("se_card_attack.wav", se_card_attack())
    write_wav("se_card_field.wav", se_card_field())
    write_wav("se_end_turn.wav", se_end_turn())
    write_wav("se_win.wav", se_win())
    write_wav("se_lose.wav", se_lose())

    print("BGM:")
    write_wav("bgm_title.wav", bgm_title(), stereo=True)
    write_wav("bgm_battle.wav", bgm_battle(), stereo=True)

    print(f"\n出力先: {os.path.normpath(OUT_DIR)}")


if __name__ == "__main__":
    main()
