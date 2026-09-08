# スマホで遊べるようにする（WebGL → GitHub Pages）

Unity エディタを開かずに、**main に push するだけで** GitHub の中でビルドし、URL として公開します。
公開先は次の URL です（Pages を有効にした後に生きます）。

```
https://miifu555.github.io/RoundTableRepo/
```

この URL をスマホのブラウザで開けば、インストールなしで遊べます（iPhone / Android どちらも可）。

> 注意：**この URL は誰でも開けます**（リポジトリが public のため）。
> 身内だけに配りたい場合は、下の「公開したくない場合」を読んでください。

---

## 準備（最初の1回だけ）

### 1. Unity のライセンスを Secrets に登録する

CI のコンテナから Unity を動かすのに、ライセンスファイル（`.ulf`）が要ります。Personal（無償）で構いません。

1. GitHub のリポジトリ → **Actions** タブ → 左の **「Unity ライセンス申請ファイル(.alf)を作る」** を選び、
   **Run workflow** を押す
2. 終わったら実行結果のページ下部にある成果物 **`unity-activation-file`** をダウンロードして解凍する
   （中に `Unity_v6000.x.alf` が入っています）
3. <https://license.unity3d.com/manual> を開き、その `.alf` をアップロード
   → Unity Personal を選ぶと `Unity_v6000.x.ulf` がダウンロードされる
4. リポジトリ → **Settings → Secrets and variables → Actions → New repository secret** で以下を登録

   | 名前 | 中身 |
   |---|---|
   | `UNITY_LICENSE` | `.ulf` ファイルを**テキストエディタで開いて中身を全部コピー**したもの |
   | `UNITY_EMAIL` | Unity アカウントのメールアドレス |
   | `UNITY_PASSWORD` | Unity アカウントのパスワード |

### 2. GitHub Pages を有効にする

リポジトリ → **Settings → Pages** → **Source** を **「GitHub Actions」** にする。
（ブランチを選ぶ方式ではありません。ここを間違えると公開されません）

### 3. 走らせる

**Actions → 「WebGL ビルド → GitHub Pages」→ Run workflow**。
以降は `Assets/` `Packages/` `ProjectSettings/` のどれかを main に push するたびに自動で再ビルド・再公開されます。

初回は Library の生成から始まるので **30〜60分**かかります。2回目以降はキャッシュが効いて数分〜十数分です。

---

## スマホで遊ぶときの前提

- **横向き**で遊んでください。UI が 1920×1080（16:9）前提で組まれているため、縦持ちだと文字が小さくなりすぎます。
  縦のときは「画面を横向きにしてください」と出るようにしてあります。
- 操作はすべて uGUI のタップ（`IPointerClickHandler`）なので、**タッチでそのまま動きます**。
  ただしカードの拡大表示はマウスホバー（`IPointerEnterHandler`）に紐づいているので、
  スマホでは指で押している間だけ反応します。気になるならタップで拡大に変えるのが良いです。
- **CPU対戦は無い**ので、1台で遊ぶ場合は**ホットシート**（2人で1台を回す）になります。
  離れて遊ぶなら通信対戦（`Online.md`）を使ってください。
- 初回の読み込みは 30〜60MB 程度あります。Wi-Fi 推奨。2回目からはブラウザにキャッシュされます。

### 通信対戦をスマホでやる場合

ブラウザ版でも GitHub API は叩けるので通信対戦は動きますが、**Personal Access Token を
公開ページの入力欄に打ち込む**ことになります。トークンはその端末のブラウザ内（PlayerPrefs）にだけ残り、
送信先も `api.github.com` だけですが、

- 対戦用に **contents だけ許可した fine-grained token** を、**対戦部屋用の別リポジトリ**に対して発行する
- 遊び終わったら失効させる

くらいはしておいた方が安全です。共用の端末では特に。

---

## 仕組み（触るファイル）

| ファイル | 役割 |
|---|---|
| `.github/workflows/webgl-pages.yml` | ビルドして Pages に公開する本体 |
| `.github/workflows/unity-activation.yml` | 上の準備1で使う、ライセンス申請ファイル生成 |
| `Assets/RoundTable/Scripts/Editor/CIBuild.cs` | batchmode から呼ばれるビルド入口。WebGL 向けの設定もここ |
| `Assets/WebGLTemplates/RoundTableMobile/index.html` | スマホ向けの HTML。全画面表示・拡大禁止・縦持ち警告 |

`CIBuild.cs` がビルド前にやっていること：

- WebGL テンプレートを `RoundTableMobile` に切り替え
- 圧縮を **gzip + JS 側で展開**に設定
  （GitHub Pages は `Content-Encoding` ヘッダを付けられないため、これでないと読み込めない）
- `Assets/Plugins/NuGet` の DLL を**エディタ専用**に落とす
  （Unity MCP プラグインが持ち込む SignalR / Roslyn 一式。ゲーム本体からは呼ばれず、
  プレイヤーに混ぜても太るだけなので外す。CI の中だけの変更で、リポジトリの `.meta` は変わりません）

Unity のバージョンは `ProjectSettings/ProjectVersion.txt` と揃える必要があります。
エディタを上げたら `webgl-pages.yml` の `UNITY_VERSION` も直してください。

---

## うまくいかないとき

| 症状 | 見るところ |
|---|---|
| `UNITY_LICENSE が未設定です` で即失敗 | 準備1をやり直す。`.ulf` は中身のテキストを貼る（ファイル名ではない） |
| ライセンス認証で落ちる | `.ulf` は Unity のバージョンごとに紐づく。エディタを上げたら取り直す |
| 公開はされたが 404 | Settings → Pages の Source が「GitHub Actions」になっているか |
| 画面が真っ黒のまま | ブラウザの開発者コンソールを見る。`.data` の読み込み失敗なら圧縮設定（上記）を疑う |
| ビルドが容量不足で落ちる | ワークフロー冒頭の「空き容量を確保」で消す対象を増やす |
| MCP プラグイン由来のコンパイルエラー | `Packages/manifest.json` から `com.ivanmurzak.unity.mcp` を外すと確実（エディタ側の道具なのでゲームには影響しない） |

## 公開したくない場合

GitHub Pages は public リポジトリだと必ず全世界に公開されます。身内限定にしたいなら、

- リポジトリを private にして GitHub Pro 以上にする（private Pages が使える）、または
- Pages をやめて **Android の APK** を Actions の成果物として配る（iPhone は不可）

のどちらかになります。APK 版が必要なら言ってください、ワークフローを足します。
