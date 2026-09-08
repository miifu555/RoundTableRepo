using System.Collections.Generic;
using RoundTable.App;
using RoundTable.Data;
using RoundTable.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoundTable.EditorTools
{
    public static partial class RoundTableBuilder
    {
        const float RefW = ArtSizes.ReferenceWidth;
        const float RefH = ArtSizes.ReferenceHeight;

        static void BuildScenes(GameDatabase db)
        {
            BuildTitleScene(db);
            BuildDeckSelectScene(db);
            BuildBattleScene(db);
            RegisterBuildSettings();
        }

        static void RegisterBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene($"{ScenesRoot}/{SceneFlow.Title}.unity", true),
                new EditorBuildSettingsScene($"{ScenesRoot}/{SceneFlow.DeckSelect}.unity", true),
                new EditorBuildSettingsScene($"{ScenesRoot}/{SceneFlow.Battle}.unity", true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // =====================================================================
        // 共通の骨組み
        // =====================================================================

        static RectTransform NewScene(out UnityEngine.SceneManagement.Scene scene)
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            // Expand にすると拡大率が min(w/1920, h/1080) になり、16:9 を維持できる
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;

            canvasGo.AddComponent<GraphicRaycaster>();

            var canvasRt = (RectTransform)canvasGo.transform;

            // 黒帯
            var bars = UiBuilder.Flat(canvasRt, "Letterbox", 0, 0, RefW, RefH, Color.black);
            UiBuilder.Stretch(bars.rectTransform);

            // ゲーム画面はすべてこの中 (常に 1920x1080)
            var safe = UiBuilder.Node("SafeArea", canvasRt);
            safe.anchorMin = safe.anchorMax = new Vector2(0.5f, 0.5f);
            safe.pivot = new Vector2(0.5f, 0.5f);
            safe.anchoredPosition = Vector2.zero;
            safe.sizeDelta = new Vector2(RefW, RefH);
            safe.gameObject.AddComponent<RectMask2D>();

            return safe;
        }

        static Image AddBackground(RectTransform safe, Sprite sprite, string note, out GameObject placeholder)
        {
            var flat = UiBuilder.Flat(safe, "BackgroundColor", 0, 0, RefW, RefH, UiBuilder.BgDark);
            UiBuilder.Stretch(flat.rectTransform);

            var img = UiBuilder.Picture(safe, "Background", 0, 0, RefW, RefH, sprite);
            UiBuilder.Stretch(img.rectTransform);
            img.gameObject.SetActive(sprite != null);

            var ph = UiBuilder.Text(safe, "BackgroundNote", 0, RefH - 26, RefW, 20,
                note, 14, new Color(1f, 1f, 1f, 0.16f), TextAlignmentOptions.Center);
            ph.gameObject.SetActive(sprite == null);
            placeholder = ph.gameObject;
            return img;
        }

        static void SaveScene(UnityEngine.SceneManagement.Scene scene, string sceneName)
        {
            EditorSceneManager.SaveScene(scene, $"{ScenesRoot}/{sceneName}.unity");
        }

        // =====================================================================
        // 01_Title
        // =====================================================================

        static void BuildTitleScene(GameDatabase db)
        {
            var safe = NewScene(out var scene);
            AddBackground(safe, db.TitleBackground,
                $"タイトル背景 {ArtSizes.BackgroundW}×{ArtSizes.BackgroundH} 未設定 (GameDatabase の Title Background に割り当て)", out _);

            var rootRt = UiBuilder.Node("TitleScreen", safe);
            UiBuilder.Place(rootRt, 0, 0, RefW, RefH);
            var screen = rootRt.gameObject.AddComponent<TitleScreen>();

            UiBuilder.Text(rootRt, "Title", 0, 40, RefW, 88, "Round Table", 58, UiBuilder.Gold, TextAlignmentOptions.Center, true);
            UiBuilder.Text(rootRt, "Lead", 0, 126, RefW, 40, "始めよう、変人たちの狂宴を。", 24, UiBuilder.TextDim, TextAlignmentOptions.Center);

            // ---- モード選択 ----
            var mode = UiBuilder.Node("ModePanel", rootRt);
            UiBuilder.Place(mode, 560, 260, 800, 440);

            var hotSeat = UiBuilder.TextButton(mode, "HotSeatButton", 0, 20, 800, 110,
                "同じPCで2人対戦（ホットシート）", 30, UiBuilder.PanelSoft, UiBuilder.TextMain);
            UiBuilder.Text(mode, "HotSeatHint", 0, 138, 800, 44,
                "1台の画面を交代しながら遊びます。手番でない側の手札は隠れます。",
                18, UiBuilder.TextDim, TextAlignmentOptions.Center);

            var online = UiBuilder.TextButton(mode, "OnlineButton", 0, 210, 800, 110,
                "通信対戦（GitHub経由）", 30, UiBuilder.GoldDim, Color.white);
            UiBuilder.Text(mode, "OnlineHint", 0, 328, 800, 66,
                "GitHub のリポジトリを郵便受けにして離れた相手と対戦します。\n先にリポジトリとアクセストークンの用意が必要です。",
                18, UiBuilder.TextDim, TextAlignmentOptions.Center);

            // ---- 通信設定 ----
            var onlinePanel = UiBuilder.Panel3(rootRt, "OnlinePanel", 440, 190, 1040, 760, UiBuilder.Panel, true);
            UiBuilder.Text(onlinePanel.transform, "Header", 0, 16, 1040, 44, "通信対戦の設定", 30,
                UiBuilder.Gold, TextAlignmentOptions.Center, true);

            TMP_InputField owner = null, repo = null, branch = null, token = null, room = null;
            string[] labels = { "所有者 (ユーザー名)", "リポジトリ名", "ブランチ", "アクセストークン", "部屋名" };
            string[] hints =
            {
                "例: your-github-name", "例: roundtable-matches", "main",
                "github_pat_... (Contents: Read and write)", "対戦相手と同じ文字列にする"
            };
            for (int i = 0; i < 5; i++)
            {
                float y = 76 + i * 68;
                UiBuilder.Text(onlinePanel.transform, "Label" + i, 28, y, 250, 56, labels[i], 21,
                    UiBuilder.TextMain, TextAlignmentOptions.Left);
                var f = UiBuilder.InputField(onlinePanel.transform, "Field" + i, 288, y, 724, 56, hints[i], 21);
                switch (i)
                {
                    case 0: owner = f; break;
                    case 1: repo = f; break;
                    case 2: branch = f; break;
                    case 3: token = f; break;
                    case 4: room = f; break;
                }
            }

            var hostToggle = UiBuilder.CheckBox(onlinePanel.transform, "HostToggle", 288, 424, 724, 48,
                "自分がホスト（プレイヤー1）として部屋を作る", 21);
            var roleHint = UiBuilder.Text(onlinePanel.transform, "RoleHint", 28, 480, 984, 60, "", 19,
                UiBuilder.TextDim, TextAlignmentOptions.TopLeft);

            var test = UiBuilder.TextButton(onlinePanel.transform, "TestButton", 28, 552, 260, 62,
                "接続テスト", 22, UiBuilder.PanelSoft, UiBuilder.TextMain);
            var status = UiBuilder.Text(onlinePanel.transform, "StatusText", 306, 552, 706, 62, "", 19,
                UiBuilder.TextDim, TextAlignmentOptions.Left);

            var back = UiBuilder.TextButton(onlinePanel.transform, "BackButton", 28, 640, 260, 72,
                "戻る", 24, UiBuilder.PanelSoft, UiBuilder.TextMain);
            var start = UiBuilder.TextButton(onlinePanel.transform, "StartButton", 690, 640, 322, 72,
                "デッキ選択へ", 26, UiBuilder.GoldDim, Color.white);

            onlinePanel.gameObject.SetActive(false);

            screen.ModePanel = mode.gameObject;
            screen.HotSeatButton = hotSeat;
            screen.OnlineButton = online;
            screen.OnlinePanel = onlinePanel.gameObject;
            screen.OwnerField = owner;
            screen.RepoField = repo;
            screen.BranchField = branch;
            screen.TokenField = token;
            screen.RoomField = room;
            screen.HostToggle = hostToggle;
            screen.RoleHintText = roleHint;
            screen.TestButton = test;
            screen.OnlineStartButton = start;
            screen.OnlineBackButton = back;
            screen.StatusText = status;

            SaveScene(scene, SceneFlow.Title);
        }

        // =====================================================================
        // 02_DeckSelect
        // =====================================================================

        static void BuildDeckSelectScene(GameDatabase db)
        {
            var safe = NewScene(out var scene);
            AddBackground(safe, db.TitleBackground,
                $"背景 {ArtSizes.BackgroundW}×{ArtSizes.BackgroundH} 未設定 (GameDatabase の Title Background に割り当て)", out _);

            var rootRt = UiBuilder.Node("DeckSelectScreen", safe);
            UiBuilder.Place(rootRt, 0, 0, RefW, RefH);
            var screen = rootRt.gameObject.AddComponent<DeckSelectScreen>();

            UiBuilder.Text(rootRt, "Title", 0, 28, RefW, 56, "Round Table", 46, UiBuilder.Gold, TextAlignmentOptions.Center, true);
            UiBuilder.Text(rootRt, "Lead", 0, 86, RefW, 34, "デッキを選んでください", 22, UiBuilder.TextDim, TextAlignmentOptions.Center);

            var grid = UiBuilder.Node("Grid", rootRt);
            UiBuilder.Place(grid, 0, 0, RefW, RefH);

            var bar = UiBuilder.Flat(rootRt, "BottomBar", 0, 986, RefW, 94, UiBuilder.Panel);
            var statusText = UiBuilder.Text(bar.transform, "StatusText", 60, 0, 1000, 94, "", 24,
                UiBuilder.TextMain, TextAlignmentOptions.Left);
            var backBtn = UiBuilder.TextButton(bar.transform, "BackButton", 1080, 17, 180, 60, "戻る", 22, UiBuilder.PanelSoft, UiBuilder.TextMain);
            var viewBtn = UiBuilder.TextButton(bar.transform, "ViewDeckButton", 1280, 17, 240, 60, "デッキを見る", 22, UiBuilder.PanelSoft, UiBuilder.TextMain);
            var startBtn = UiBuilder.TextButton(bar.transform, "StartButton", 1540, 17, 340, 60, "対戦開始", 28, UiBuilder.GoldDim, Color.white);

            // ---- デッキ一覧オーバーレイ ----
            var overlay = UiBuilder.Node("DeckListOverlay", rootRt);
            UiBuilder.Place(overlay, 0, 0, RefW, RefH);

            var shade = UiBuilder.Flat(overlay, "Shade", 0, 0, RefW, RefH, new Color(0.02f, 0.02f, 0.03f, 0.97f));
            UiBuilder.Stretch(shade.rectTransform);
            shade.raycastTarget = true;
            var closeBtn = shade.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = shade;
            closeBtn.transition = Selectable.Transition.None;

            var listTitle = UiBuilder.Text(overlay, "Title", 0, 24, RefW, 46, "", 30, UiBuilder.Gold, TextAlignmentOptions.Center, true);
            var listContainer = UiBuilder.Node("Cards", overlay);
            UiBuilder.Place(listContainer, 0, 0, RefW, RefH);
            UiBuilder.Text(overlay, "Hint", 0, 980, RefW, 40, "クリックで閉じる", 22, UiBuilder.TextDim, TextAlignmentOptions.Center);

            overlay.gameObject.SetActive(false);

            screen.Grid = grid;
            screen.CellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabRoot + "/DeckCell.prefab")?.GetComponent<DeckCell>();
            screen.StatusText = statusText;
            screen.ViewDeckButton = viewBtn;
            screen.StartButton = startBtn;
            screen.BackButton = backBtn;
            screen.DeckListOverlay = overlay.gameObject;
            screen.DeckListContainer = listContainer;
            screen.DeckListTitle = listTitle;
            screen.DeckListCloseButton = closeBtn;

            SaveScene(scene, SceneFlow.DeckSelect);
        }

        // =====================================================================
        // 03_Battle
        // =====================================================================

        const float BoardX = 16f, BoardW = 1426f;
        const float RightX = 1458f, RightW = 446f;

        static void BuildBattleScene(GameDatabase db)
        {
            var safe = NewScene(out var scene);
            var bgImage = AddBackground(safe, db.BattleBackground,
                $"対戦背景 {ArtSizes.BackgroundW}×{ArtSizes.BackgroundH} 未設定 (GameDatabase の Battle Background に割り当て)",
                out var bgPlaceholder);

            var matchGo = new GameObject("Match");
            var match = matchGo.AddComponent<MatchController>();

            var rootRt = UiBuilder.Node("BattleScreen", safe);
            UiBuilder.Place(rootRt, 0, 0, RefW, RefH);
            var screen = rootRt.gameObject.AddComponent<BattleScreen>();

            // ---- 上下の情報バー ----
            var topInfo = BuildInfoPanel(rootRt, "TopInfo", BoardX, 16);
            var bottomInfo = BuildInfoPanel(rootRt, "BottomInfo", BoardX, 684);

            // ---- 相手の手札 (裏向き) ----
            var topHand = UiBuilder.Node("TopHand", rootRt);
            UiBuilder.Place(topHand, BoardX, 126, BoardW, 90);

            // ---- フィールド ----
            var topFieldPanel = UiBuilder.Panel3(rootRt, "TopField", BoardX, 222, BoardW, 190, UiBuilder.Panel, true);
            var topFieldLabel = UiBuilder.Text(topFieldPanel.transform, "Label", 12, 4, 300, 24, "相手のフィールド", 17,
                UiBuilder.TextDim, TextAlignmentOptions.TopLeft);
            var topFieldCards = UiBuilder.Node("Cards", topFieldPanel.transform);
            UiBuilder.Place(topFieldCards, 0, 0, BoardW, 190);

            var bottomFieldPanel = UiBuilder.Panel3(rootRt, "BottomField", BoardX, 488, BoardW, 190, UiBuilder.Panel, true);
            var bottomFieldLabel = UiBuilder.Text(bottomFieldPanel.transform, "Label", 12, 4, 300, 24, "自分のフィールド", 17,
                UiBuilder.TextDim, TextAlignmentOptions.TopLeft);
            var bottomFieldCards = UiBuilder.Node("Cards", bottomFieldPanel.transform);
            UiBuilder.Place(bottomFieldCards, 0, 0, BoardW, 190);

            // ---- 中央バー ----
            var center = UiBuilder.Panel3(rootRt, "CenterBar", BoardX, 418, BoardW, 64, UiBuilder.Panel, true);
            var centerText = UiBuilder.Text(center.transform, "CenterText", 20, 0, 880, 64, "", 24,
                UiBuilder.TextMain, TextAlignmentOptions.Left);
            var surrenderBtn = UiBuilder.TextButton(center.transform, "SurrenderButton", 920, 8, 140, 48,
                "投了", 20, new Color(0.30f, 0.20f, 0.22f, 1f), UiBuilder.TextDim);
            var confirmBtn = UiBuilder.TextButton(center.transform, "ConfirmButton", 1074, 8, 176, 48,
                "選択を確定", 22, new Color(0.35f, 0.55f, 0.42f, 1f), Color.white);
            var endTurnBtn = UiBuilder.TextButton(center.transform, "EndTurnButton", 1262, 8, 148, 48,
                "ターン終了", 22, UiBuilder.GoldDim, Color.white);

            // ---- 手札 ----
            var hand = UiBuilder.Node("Hand", rootRt);
            UiBuilder.Place(hand, BoardX, 796, BoardW, 280);
            var handEmpty = UiBuilder.Text(hand, "EmptyLabel", 0, 0, BoardW, 280, "手札なし", 22,
                new Color(1f, 1f, 1f, 0.2f), TextAlignmentOptions.Center);
            var handCards = UiBuilder.Node("Cards", hand);
            UiBuilder.Place(handCards, 0, 0, BoardW, 280);

            // ---- 右カラム ----
            var zoomPanel = UiBuilder.Panel3(rootRt, "ZoomPanel", RightX, 16, RightW, 612, UiBuilder.Panel, true);
            var zoomHint = UiBuilder.Text(zoomPanel.transform, "Hint", 20, 0, RightW - 40, 612,
                "カードにカーソルを合わせると\nここに拡大表示されます", 20, UiBuilder.TextDim, TextAlignmentOptions.Center);
            var zoomContainer = UiBuilder.Node("Card", zoomPanel.transform);
            UiBuilder.Place(zoomContainer, 8, 0, RightW - 16, 612);

            var logPanel = UiBuilder.Panel3(rootRt, "LogPanel", RightX, 634, RightW, 430, UiBuilder.Panel, true);
            logPanel.gameObject.AddComponent<RectMask2D>();
            UiBuilder.Text(logPanel.transform, "Header", 14, 8, RightW - 28, 26, "ログ", 20, UiBuilder.Gold, TextAlignmentOptions.TopLeft, true);
            var logText = UiBuilder.Text(logPanel.transform, "LogText", 14, 38, RightW - 28, 380, "", 16,
                UiBuilder.TextDim, TextAlignmentOptions.BottomLeft);

            // ---- 結果オーバーレイ ----
            var result = UiBuilder.Node("ResultOverlay", rootRt);
            UiBuilder.Place(result, 0, 0, RefW, RefH);
            var rShade = UiBuilder.Flat(result, "Shade", 0, 0, RefW, RefH, new Color(0f, 0f, 0f, 0.72f));
            UiBuilder.Stretch(rShade.rectTransform);
            rShade.raycastTarget = true;

            var rPanel = UiBuilder.Panel3(result, "Panel", (RefW - 760) * 0.5f, (RefH - 400) * 0.5f, 760, 400, UiBuilder.PanelSoft, true);
            var rTitle = UiBuilder.Text(rPanel.transform, "Title", 0, 44, 760, 80, "", 56, UiBuilder.Gold, TextAlignmentOptions.Center, true);
            var rSub = UiBuilder.Text(rPanel.transform, "Sub", 0, 138, 760, 56, "", 26, UiBuilder.TextMain, TextAlignmentOptions.Center);
            var nextBtn = UiBuilder.TextButton(rPanel.transform, "NextRoundButton", 60, 250, 300, 72, "次のラウンドへ", 24, UiBuilder.GoldDim, Color.white);
            var exitBtn = UiBuilder.TextButton(rPanel.transform, "ExitButton", 400, 250, 300, 72, "タイトルへ", 24, UiBuilder.PanelSoft, UiBuilder.TextMain);
            result.gameObject.SetActive(false);

            // ---- 接続オーバーレイ ----
            var statusOverlay = UiBuilder.Node("StatusOverlay", rootRt);
            UiBuilder.Place(statusOverlay, 0, 0, RefW, RefH);
            var sShade = UiBuilder.Flat(statusOverlay, "Shade", 0, 0, RefW, RefH, new Color(0.02f, 0.02f, 0.03f, 0.92f));
            UiBuilder.Stretch(sShade.rectTransform);
            sShade.raycastTarget = true;
            var sText = UiBuilder.Text(statusOverlay, "Text", 260, 460, 1400, 120, "接続中…", 30,
                UiBuilder.TextMain, TextAlignmentOptions.Center);
            var sCancel = UiBuilder.TextButton(statusOverlay, "CancelButton", (RefW - 300) * 0.5f, 600, 300, 72,
                "タイトルへ戻る", 24, UiBuilder.PanelSoft, UiBuilder.TextMain);
            statusOverlay.gameObject.SetActive(false);

            // ---- 結線 ----
            screen.Match = match;
            screen.Background = bgImage;
            screen.BackgroundPlaceholder = bgPlaceholder;
            screen.TopInfo = topInfo;
            screen.BottomInfo = bottomInfo;
            screen.TopHandContainer = topHand;
            screen.TopFieldContainer = topFieldCards;
            screen.BottomFieldContainer = bottomFieldCards;
            screen.HandContainer = handCards;
            screen.TopFieldLabel = topFieldLabel;
            screen.BottomFieldLabel = bottomFieldLabel;
            screen.HandEmptyLabel = handEmpty;
            screen.CenterText = centerText;
            screen.EndTurnButton = endTurnBtn;
            screen.ConfirmButton = confirmBtn;
            screen.SurrenderButton = surrenderBtn;
            screen.ZoomContainer = zoomContainer;
            screen.ZoomHint = zoomHint;
            screen.LogText = logText;
            screen.ResultOverlay = result.gameObject;
            screen.ResultTitle = rTitle;
            screen.ResultSub = rSub;
            screen.NextRoundButton = nextBtn;
            screen.ExitButton = exitBtn;
            screen.StatusOverlay = statusOverlay.gameObject;
            screen.StatusOverlayText = sText;
            screen.StatusCancelButton = sCancel;
            screen.CardBackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabRoot + "/CardBack.prefab");

            SaveScene(scene, SceneFlow.Battle);
        }

        static PlayerInfoPanel BuildInfoPanel(Transform parent, string name, float x, float y)
        {
            const float W = BoardW, H = 104f;

            var panel = UiBuilder.Panel3(parent, name, x, y, W, H, UiBuilder.Panel, true);
            var t = panel.transform;

            var portrait = UiBuilder.Picture(t, "Portrait", 8, 6, 92, 92, null, true);
            portrait.gameObject.SetActive(false);
            var ph = UiBuilder.Panel3(t, "PortraitPlaceholder", 8, 6, 92, 92, UiBuilder.Placeholder);
            var phText = UiBuilder.Text(ph.transform, "Text", 0, 0, 92, 92,
                $"顔\n{ArtSizes.PortraitIconW}\n×{ArtSizes.PortraitIconH}", 14, UiBuilder.PlaceholderText, TextAlignmentOptions.Center);
            UiBuilder.Stretch(phText.rectTransform);

            var nameText = UiBuilder.Text(t, "Name", 112, 6, 420, 38, "", 28, UiBuilder.TextMain, TextAlignmentOptions.Left, true);
            var archText = UiBuilder.Text(t, "Archetype", 112, 46, 420, 48, "", 19, UiBuilder.TextDim, TextAlignmentOptions.TopLeft);

            var energyText = UiBuilder.Text(t, "EnergyText", 548, 2, 380, 44, "", 28, UiBuilder.EnergyColor, TextAlignmentOptions.Left, true);
            UiBuilder.Panel3(t, "EnergyBarBg", 548, 54, 380, 24, new Color(0f, 0f, 0f, 0.45f));
            // Filled は 9-slice と併用できない (枠が伸びて歪む) ので、塗りは単色スプライトで作る
            var energyBar = UiBuilder.Flat(t, "EnergyBar", 548, 54, 380, 24, UiBuilder.EnergyColor);
            energyBar.type = Image.Type.Filled;
            energyBar.fillMethod = Image.FillMethod.Horizontal;
            energyBar.fillOrigin = 0;
            energyBar.fillAmount = 1f;

            UiBuilder.Text(t, "DaoLabel", 950, 4, 60, 32, "^^", 24, UiBuilder.Gold, TextAlignmentOptions.Left, true);
            var daoIcons = new Image[3];
            for (int i = 0; i < 3; i++)
                daoIcons[i] = UiBuilder.CircleImage(t, "Dao" + i, 1000 + i * 52, 44, 44, new Color(0.28f, 0.26f, 0.32f, 1f));

            var counts = UiBuilder.Text(t, "Counts", 1180, 4, 236, 96, "", 17, UiBuilder.TextDim, TextAlignmentOptions.Left);

            var info = panel.gameObject.AddComponent<PlayerInfoPanel>();
            info.Portrait = portrait;
            info.PortraitPlaceholder = ph.gameObject;
            info.NameText = nameText;
            info.ArchetypeText = archText;
            info.EnergyText = energyText;
            info.EnergyBar = energyBar;
            info.DaoIcons = daoIcons;
            info.CountsText = counts;
            return info;
        }
    }
}
