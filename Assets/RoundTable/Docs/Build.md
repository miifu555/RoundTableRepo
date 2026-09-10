# WebGL ビルド（ブラウザで遊べるようにする）

## ビルドする

Unity メニュー **「Round Table / WebGL をビルド」**。出力先は `Build/WebGL`（`.gitignore` 済み）。

初回は IL2CPP と Emscripten のコンパイルが走るので **10〜20分**かかります。
2回目以降はキャッシュが効いて数分です。ビルド中 Unity は操作できません。

コマンドラインから叩く場合（Unity エディタを閉じてから）:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" -batchmode -quit -nographics -projectPath "D:\Unity\RoundTable" -executeMethod RoundTable.EditorTools.WebGLBuild.BuildFromCommandLine -buildOutput Build/WebGL
```

成否は `Build/webgl-build-result.txt` に残ります（`OK` / `NG` とエラー内容）。
ビルドは時間がかかって MCP 越しの応答がタイムアウトすることがあるため、このファイルで確認できるようにしてあります。

## 動作確認（ローカル）

**`index.html` をダブルクリックしても動きません。** `file://` では読み込みがブロックされるので、
簡易サーバーを立ててアクセスします。

```bash
cd Build/WebGL
python -m http.server 8080
```

ブラウザで <http://localhost:8080> を開きます。

## 設定（`Scripts/Editor/WebGLBuild.cs`）

| 項目 | 値 | 理由 |
|---|---|---|
| 圧縮 | Gzip + **展開フォールバック有効** | GitHub Pages などの静的ホスティングは `Content-Encoding` を付けられないため。JS 側で展開する |
| Data Caching | ON | 2回目以降の起動が速くなる（ブラウザの IndexedDB に `.data` を残す） |
| 例外 | 明示的な throw のみ | サイズと速度のため |
| Run In Background | ON | 別タブに移っても通信対戦のポーリングが止まらないように |
| テンプレート | `RoundTableMobile` | 画面いっぱいに広げる / ピンチズーム無効 / 縦持ちのとき回転を促す |

テンプレートの実体は `Assets/WebGLTemplates/RoundTableMobile/index.html` です。

## ⚠️ Unity MCP プラグインについて

`Assets/Plugins/NuGet` の DLL（SignalR / Roslyn / System.Text.Json など）は
Unity MCP プラグインが持ち込むもので、**ゲーム本体からは呼ばれません**。
ただし **プレイヤーのビルドから外すことはできません**。

MCP パッケージの Runtime アセンブリ `com.IvanMurzak.Unity.MCP.Runtime` が
それらを `precompiledReferences` として参照しているため、DLL をエディタ専用にすると
プレイヤー側のコンパイルが **CS0234 で 2,000 件以上落ちます**（実際に踏みました）。

本気で外したい場合は、Player Settings の **Scripting Define Symbols から
`UNITY_MCP_READY` を WebGL だけ削る**ことになります（両方の asmdef が
`defineConstraints: ["UNITY_MCP_READY"]` を持っているため）。
ただし**エディタ側の MCP も止まる**ので、その状態では Claude から Unity を操作できなくなります。
配布用に容量を削りたいときだけ、手作業で切り替えてください。

## スマホで遊ぶときの前提

- **横向き**推奨。UI が 1920×1080（16:9）前提なので、縦持ちだと文字が小さくなります。
  縦のときは「画面を横向きにしてください」と出ます
- 操作は uGUI のタップなので**指でそのまま動きます**。
  ただしカードの拡大表示はホバー（`IPointerEnterHandler`）に紐づいているので、
  スマホでは押している間だけ反応します。気になるならタップで拡大に変えるのが良いです
- 初回の読み込みは 30MB 前後。Wi-Fi 推奨。2回目からはキャッシュされます
- **CPU対戦は1人で遊べます。** 対人は同じ端末を回すホットシートか、通信対戦（`Online.md`）です

## GitHub Pages で公開する

`Build/WebGL` の中身を `gh-pages` ブランチとして push するだけです。CI も Unity ライセンスの登録も要りません。

```bash
bash Tools/deploy-pages.sh
```

このスクリプトは毎回 **1コミットだけの orphan ブランチ**として作り直して force push します。
WebGL の出力は差分の効かないバイナリが 28MB あり、履歴を積むとリポジトリが太るためです。

初回だけ GitHub 側で2つ設定します。

1. **Settings → General → Danger Zone → Change repository visibility → Public**
   （Private のまま Pages を使うには有料プランが必要）
2. **Settings → Pages → Source: Deploy from a branch → Branch: `gh-pages` / `/ (root)`**

数分待つと次の URL で遊べるようになります。

```
https://miifu555.github.io/RoundTableRepo/
```

更新したいときは、Unity で作り直して `bash Tools/deploy-pages.sh` を叩き直すだけです。

> ⚠️ **Public にすると URL を知っている人は誰でも遊べます。**
> また **通信対戦のトークンを焼き込んだまま公開しないでください。**
> ブラウザ版はビルドの中身を誰でも取り出せます（`Online.md` 参照）。
> 現在この構成にはトークンは含まれていません（各自がタイトル画面で入力する方式）。

## 他のホスティング

`Build/WebGL` の中身をそのまま置けば、Netlify / Cloudflare Pages / 自前のサーバーでも動きます。
gzip + 展開フォールバック有効なので、`Content-Encoding` を付けられないホスティングでも問題ありません。
