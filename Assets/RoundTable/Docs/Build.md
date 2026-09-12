# ビルドして配る（WebGL / Windows）

配り方は2通りあります。

- **WebGL**: ブラウザで URL を開くだけで遊べる。GitHub Pages で公開するため、
  リポジトリを Public にする必要がある
- **Windows**: `.exe` を直接渡す。公開ホスティングを経由しないので、
  実写イラストなど見せる相手を限定したい絵を使うならこちら（後述の「Windows ビルドで配る」）

## WebGL をビルドする

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

## Windows ビルドで配る（実写イラストを使うとき向け）

WebGL 版は `gh-pages` を経由するため、リポジトリを Public にする必要があり、
イラストの実データが誰でも見られる URL に置かれます。**カードの絵に実写（本人の顔写真など）を
使うつもりなら、公開ホスティングを経由しない Windows ビルドで配るほうが安全です。**

### ビルドする

Unity メニュー **「Round Table / Windows をビルド」**。出力先は `Build/Windows`（`.gitignore` 済み）。
WebGL と違って IL2CPP/Emscripten のコンパイルが無いので数分で終わります。

コマンドラインから叩く場合（Unity エディタを閉じてから）:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" -batchmode -quit -nographics -projectPath "D:\Unity\RoundTable" -executeMethod RoundTable.EditorTools.WindowsBuild.BuildFromCommandLine -buildOutput Build/Windows
```

成否は `Build/windows-build-result.txt` に残ります。

### 実写は自動で Windows だけに入る

実写（本人の顔写真など）は `Art/` ではなく **`Art/Photos/`** に置いてください。中のフォルダ構成は
`Art/` と同じです（`Art/Photos/CardArt/toshi_a1.png` のように）。`Art/Photos/` は丸ごと
`.gitignore` 済みで、GitHub には一切上がりません。

- **Windows ビルドを実行した瞬間だけ** `Art/Photos/` の絵が割り当てられ、ビルドが終わると
  （成功でも失敗でも）自動で公開用の絵に戻ります。プロジェクトを保存したまま放置しても、
  実写が割り当たった状態は残りません
- **WebGL ビルドには絶対に混ざりません。** `Round Table / WebGL をビルド` は実行するたびに、
  何が割り当たっていても強制的に公開用へリセットしてから焼きます
- ビルドせずエディタでプレビューしたいときはメニュー
  **「Round Table / 実写を割り当てる (Windows限定)」**。手動で戻すなら
  **「Round Table / 実写を外す (公開用に戻す)」**

`Art/` （公開用）にしか絵が無いカードは、Windows ビルドでもそのまま公開用の絵が使われます。
実写を用意した分だけ差し替わる仕組みです。

### 配り方

`Build/Windows` フォルダごと zip にして、Google ドライブや Discord など**GitHub を経由しない方法**で
友人に直接渡してください。`RoundTable.exe` をダブルクリックすれば遊べます。インストール不要です。

- **このリポジトリを Public にする必要はありません。** WebGL 版と違い、GitHub Pages を使わないためです
  （通信対戦の郵便受けである `RoundTableLobby` は別リポジトリなので、そちらの公開設定には影響しません）
- 初回起動時に **Windows SmartScreen が「WindowsによってPCが保護されました」と警告を出すことがあります。**
  署名していない個人開発のビルドなら普通に起きることです。「詳細情報」→「実行」で進めます

### ⚠️ これは「絶対に流出しない」という意味ではありません

zip を渡した相手は `RoundTable_Data` フォルダの中身を、AssetStudio のような専用ツールで
開けば画像を取り出せます。WebGL 版のように**検索エンジンや偶然踏んだ誰かに見られる**リスクは
無くなりますが、**渡す相手は信頼できる人に限定してください**。実写を人に見せるときの
一般的な注意と同じです。

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
