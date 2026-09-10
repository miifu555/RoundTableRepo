# BGM と効果音

Notion の「効果音」の項に対応する 2 BGM + 6 SE。すべて `Tools/make_audio.py` で合成しています。
外部の音源は使っていないので、権利表記は不要です。

| ファイル | 用途 | 長さ | 内容 |
|---|---|---|---|
| `bgm_title.wav` | タイトル / デッキ選択 | 22.9s ループ | 84 BPM。Am7 - Fmaj7 - Cmaj7 - E7 をパッド + アルペジオ + ベースで |
| `bgm_battle.wav` | 対戦中 | 29.1s ループ | 132 BPM。Am - F - G - Em。キック/ハイハット/スネアと刻みベース、後半はメロディ入り |
| `se_button.wav` | タイトル / デッキ選択のボタン | 0.10s | 硬いクリック |
| `se_card_attack.wav` | 攻撃カード使用 | 0.46s | 手札から抜く → 指で弾く → 机に叩きつける |
| `se_card_field.wav` | フィールドカード使用 | 0.52s | 手札から抜く → 机の上を滑らせる → そっと置く |
| `se_end_turn.wav` | ターン終了 | 0.70s | カードの束を机で2回揃えて、A4 → E4 の確認音 |
| `se_win.wav` | 勝利 | 1.90s | カードを掻き集める音 → C-E-G-C の明るいアルペジオ |
| `se_lose.wav` | 敗北 | 2.00s | カードが散る音 → A-F-D-A の暗い下降 |

## カードの音の作り方

「実際にカードを扱っている風に」という要望なので、楽器音ではなく**紙と机の物理音を積み上げて**います。
`Tools/make_audio.py` の以下の部品の組み合わせです。

| 部品 | 中身 |
|---|---|
| `card_slide()` | 1.8〜7kHz に絞ったノイズ + 速い減衰。紙が擦れる「シュッ」 |
| `card_snap()` | 2.6〜11kHz の、より高くて立ち上がりの速いノイズ。指で弾く「パシッ」 |
| `table_thud()` | 110Hz 前後の減衰する正弦波 + 低域ノイズ。机に当たる胴鳴り |
| `riffle()` | 細かいクリックを 16 個ばらまく。カードを繰る「パラパラ」 |

攻撃とフィールドの差は、**強さと速さ**で付けています。

- 攻撃: 弾く音を入れて、机への当たりを強く速く（重心 4.1kHz と明るい）
- フィールド: 弾く音を入れず、長い擦れを挟んで低く柔らかく置く（重心 2.6kHz）

## 作り直す

```bash
python Tools/make_audio.py          # WAV を作り直す
```

そのあと Unity メニュー **「Round Table / 音声を取り込む」** を実行すると、
インポート設定を当てて `RoundTableDatabase` に割り当て直します。

音を変えたいときは `Tools/make_audio.py` の各関数の数値をいじってください。
たとえば攻撃カードをもっと重くしたいなら `se_card_attack()` の `table_thud(0.20, 104, 1.0)` の
周波数を下げる、繰る音を長くしたいなら `riffle()` の `count` を増やす、といった具合です。

## 実装

| ファイル | 役割 |
|---|---|
| `Scripts/App/AudioManager.cs` | 再生。シーンをまたいで1つだけ生きる。`AudioListener` もここが持つ |
| `Scripts/Data/GameDatabase.cs` | 音源の参照（Inspector で差し替え可能） |
| `Scripts/Editor/AudioSetup.cs` | インポート設定 + データベースへの割り当て |

鳴らしている場所:

- BGM: `TitleScreen` / `DeckSelectScreen` が `PlayTitleBgm()`、`BattleScreen` が `PlayBattleBgm()`。
  タイトルとデッキ選択は同じ曲なので、画面が変わっても途切れません
- カード / ターン終了: `MatchController.ApplyAction`。
  **自分・CPU・通信相手のどれが操作しても同じ経路を通る**ので、1箇所で済んでいます
- 勝敗: `MatchController.UpdateResultSound`。決着した瞬間に1回だけ

## 音量

既定は BGM 0.45 / SE 0.85 で、`PlayerPrefs` に保存されます。
`AudioManager.SetBgmVolume()` / `SetSeVolume()` / `ToggleBgm()` が用意してあるので、
画面にスライダーやミュートボタンを付けたい場合はそれを呼んでください（現状 UI はありません）。

## ブラウザでの注意

ブラウザは**最初のクリックまで音を鳴らしません**（自動再生のブロック）。
`AudioManager.Update` が「鳴るべき BGM が止まっていたら鳴らし直す」ようにしてあるので、
タイトル画面で最初にボタンを押した瞬間から自然に流れ始めます。

## ビルドサイズ

プロジェクト内の WAV は合計 9.4MB ですが、Vorbis 圧縮でインポートしているので
ビルドに入るのはずっと小さくなります（BGM は Streaming、SE は読み込み時に展開）。
