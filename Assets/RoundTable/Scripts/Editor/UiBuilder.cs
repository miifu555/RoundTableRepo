using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RoundTable.EditorTools
{
    /// <summary>
    /// プレハブ / シーンをコードから組み立てるためのエディタ専用ヘルパ。
    /// 実行時には一切使われない (生成物であるプレハブとシーンだけが動く)。
    /// </summary>
    public static class UiBuilder
    {
        // ---- 配色 ----
        public static readonly Color BgDark = new Color(0.078f, 0.071f, 0.094f, 1f);
        public static readonly Color Panel = new Color(0.145f, 0.133f, 0.176f, 0.94f);
        public static readonly Color PanelSoft = new Color(0.196f, 0.180f, 0.235f, 0.94f);
        public static readonly Color CardInner = new Color(0.121f, 0.113f, 0.145f, 1f);
        public static readonly Color Gold = new Color(0.847f, 0.706f, 0.376f, 1f);
        public static readonly Color GoldDim = new Color(0.54f, 0.45f, 0.25f, 1f);
        public static readonly Color TextMain = new Color(0.945f, 0.933f, 0.905f, 1f);
        public static readonly Color TextDim = new Color(0.639f, 0.616f, 0.588f, 1f);
        public static readonly Color AttackColor = new Color(0.788f, 0.310f, 0.286f, 1f);
        public static readonly Color FieldColor = new Color(0.298f, 0.510f, 0.686f, 1f);
        public static readonly Color EnergyColor = new Color(0.373f, 0.749f, 0.639f, 1f);
        public static readonly Color DangerColor = new Color(0.855f, 0.353f, 0.290f, 1f);
        public static readonly Color Placeholder = new Color(0.20f, 0.19f, 0.24f, 1f);
        public static readonly Color PlaceholderText = new Color(0.45f, 0.43f, 0.50f, 1f);

        public static TMP_FontAsset Font;

        // =====================================================================
        // スプライト素材 (プレハブに保存できるよう実アセットとして生成する)
        // =====================================================================

        public const string SpriteFolder = "Assets/RoundTable/Art/Ui";

        public static Sprite White => LoadOrCreate("white", 8, 0, 0);
        public static Sprite RoundSmall => LoadOrCreate("panel_r10", 32, 10, 10);
        public static Sprite RoundLarge => LoadOrCreate("panel_r24", 64, 24, 24);
        public static Sprite Circle => LoadOrCreate("circle", 128, 64, 0);

        static Sprite LoadOrCreate(string name, int size, int radius, int border)
        {
            string path = $"{SpriteFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(SpriteFolder);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a;
                    if (radius <= 0)
                    {
                        a = 1f;
                    }
                    else
                    {
                        float cx = x < radius ? radius - 0.5f : (x >= size - radius ? size - radius - 0.5f : x);
                        float cy = y < radius ? radius - 0.5f : (y >= size - radius ? size - radius - 0.5f : y);
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx + 0.5f, cy + 0.5f));
                        a = Mathf.Clamp01(radius - d + 0.5f);
                        if (x >= radius && x < size - radius) a = 1f;
                        if (y >= radius && y < size - radius) a = 1f;
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (border > 0) importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // =====================================================================
        // 基本ノード
        // =====================================================================

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            return rt;
        }

        /// <summary>左上原点で配置する (デザイン座標をそのまま使える)。</summary>
        public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        // =====================================================================
        // 部品
        // =====================================================================

        public static Image Panel3(Transform parent, string name, float x, float y, float w, float h,
            Color color, bool largeRadius = false)
        {
            var rt = Node(name, parent);
            Place(rt, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = largeRadius ? RoundLarge : RoundSmall;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image Flat(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var rt = Node(name, parent);
            Place(rt, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = White;
            img.type = Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image CircleImage(Transform parent, string name, float x, float y, float d, Color color)
        {
            var rt = Node(name, parent);
            Place(rt, x, y, d, d);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Circle;
            img.type = Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>スプライト表示用 (絵を入れる枠)。</summary>
        public static Image Picture(Transform parent, string name, float x, float y, float w, float h,
            Sprite sprite = null, bool preserveAspect = false)
        {
            var rt = Node(name, parent);
            Place(rt, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = Color.white;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, float x, float y, float w, float h,
            string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool bold = false,
            bool truncate = false)
        {
            var rt = Node(name, parent);
            Place(rt, x, y, w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            // Truncate は「1行の高さが枠に収まらないと行ごと消える」ため、既定は Overflow。
            // 枠から溢れては困る箇所 (カードの効果テキスト等) だけ truncate:true を指定する。
            t.overflowMode = truncate ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        public static Button TextButton(Transform parent, string name, float x, float y, float w, float h,
            string label, float fontSize, Color bg, Color fg)
        {
            var img = Panel3(parent, name, x, y, w, h, bg);
            img.raycastTarget = true;

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.55f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;

            var t = Text(img.transform, "Label", 0, 0, w, h, label, fontSize, fg, TextAlignmentOptions.Center, true);
            Stretch(t.rectTransform);

            return btn;
        }

        /// <summary>透明でクリックだけ拾う当たり判定を追加する。</summary>
        public static Image HitArea(GameObject go)
        {
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = White;
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;
            return img;
        }

        public static TMP_InputField InputField(Transform parent, string name, float x, float y, float w, float h,
            string placeholder, float fontSize)
        {
            var bg = Panel3(parent, name, x, y, w, h, new Color(0.09f, 0.085f, 0.11f, 1f));
            bg.raycastTarget = true;

            var area = Node("TextArea", bg.transform);
            Stretch(area, 10f);
            area.gameObject.AddComponent<RectMask2D>();

            var ph = Text(area, "Placeholder", 0, 0, w, h, placeholder, fontSize, new Color(0.45f, 0.43f, 0.50f, 1f),
                TextAlignmentOptions.Left);
            Stretch(ph.rectTransform);

            var txt = Text(area, "Text", 0, 0, w, h, "", fontSize, TextMain, TextAlignmentOptions.Left);
            Stretch(txt.rectTransform);

            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = txt;
            field.placeholder = ph;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.fontAsset = Font;
            field.pointSize = fontSize;
            field.caretColor = TextMain;
            field.selectionColor = new Color(0.4f, 0.5f, 0.7f, 0.5f);

            return field;
        }

        public static Toggle CheckBox(Transform parent, string name, float x, float y, float w, float h,
            string label, float fontSize)
        {
            var root = Node(name, parent);
            Place(root, x, y, w, h);
            HitArea(root.gameObject);

            var box = Panel3(root, "Box", 0, (h - 32) * 0.5f, 32, 32, new Color(0.09f, 0.085f, 0.11f, 1f));
            var check = Panel3(box.transform, "Check", 6, 6, 20, 20, Gold);

            Text(root, "Label", 44, 0, w - 44, h, label, fontSize, TextMain, TextAlignmentOptions.Left);

            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = true;
            return toggle;
        }
    }
}
