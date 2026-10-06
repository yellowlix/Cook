#if UNITY_EDITOR
using Cook.Configuration;
using Cook.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Cook.Editor
{
    /// <summary>只生成菜单、库存显示和售卖入口，保留场景布景及动画。</summary>
    public static class ShopEntrySceneBuilder
    {
        private static TMP_FontAsset font;

        [MenuItem("Cook/Update Menu Inventory And Sale Entry")]
        public static void UpdateScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameScene.unity")
                throw new System.InvalidOperationException("Open GameScene first.");
            var controller = Object.FindFirstObjectByType<CookController>();
            var hud = Object.FindFirstObjectByType<CookHud>();
            if (controller == null || hud == null) throw new System.InvalidOperationException("Production scene components missing.");
            Canvas canvas = hud.GetComponentInParent<Canvas>();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansSC-CookingSubset SDF.asset");
            Transform parent = canvas.transform;
            Remove(parent, "MenuPanel"); Remove(parent, "SalePanel");
            Remove(parent, "InventoryText"); Remove(parent, "MenuButton"); Remove(parent, "SaleButton");

            TMP_Text inventory = Text("InventoryText", parent, new Vector2(-740, 470), new Vector2(360, 50), "成品库存 0/3", 28);
            Button menuButton = Button("MenuButton", parent, new Vector2(530, 470), new Vector2(230, 60), "选菜菜单");
            Button saleButton = Button("SaleButton", parent, new Vector2(800, 470), new Vector2(270, 60), "进入售卖");
            RectTransform menuRoot = Panel("MenuPanel", parent, new Vector2(640, 640));
            Text("Title", menuRoot, new Vector2(0, 260), new Vector2(540, 60), "制作菜单", 36);
            Transform menuContent = Scroll(menuRoot, new Vector2(0, 35), new Vector2(550, 320));
            Button menuTemplate = Row(menuContent, true);
            TMP_Text menuStatus = Text("Status", menuRoot, new Vector2(0, -175), new Vector2(560, 90), "选择菜品后开始制作", 23);
            Button make = Button("Confirm", menuRoot, new Vector2(145, -260), new Vector2(270, 65), "开始制作");
            Button close = Button("Close", menuRoot, new Vector2(-145, -260), new Vector2(240, 65), "关闭菜单");
            MenuPanel menu = menuRoot.gameObject.AddComponent<MenuPanel>();
            var menuData = new SerializedObject(menu);
            Assign(menuData, "content", menuContent); Assign(menuData, "rowTemplate", menuTemplate);
            Assign(menuData, "confirmButton", make); Assign(menuData, "closeButton", close);
            Assign(menuData, "statusText", menuStatus); menuData.ApplyModifiedPropertiesWithoutUndo();

            RectTransform saleRoot = Panel("SalePanel", parent, new Vector2(700, 720));
            TMP_Text saleTitle = Text("Title", saleRoot, new Vector2(0, 300), new Vector2(600, 60), "售卖选菜", 36);
            Transform saleContent = Scroll(saleRoot, new Vector2(0, 70), new Vector2(600, 310));
            Button saleTemplate = Row(saleContent, false);
            TMP_Text saleStatus = Text("Status", saleRoot, new Vector2(0, -150), new Vector2(620, 170), "选择 1–3 道成品", 24);
            Button confirm = Button("Confirm", saleRoot, new Vector2(155, -290), new Vector2(260, 65), "确认待售菜品");
            Button back = Button("Back", saleRoot, new Vector2(-155, -290), new Vector2(260, 65), "返回制作");
            SalePanel sale = saleRoot.gameObject.AddComponent<SalePanel>();
            var saleData = new SerializedObject(sale);
            Assign(saleData, "content", saleContent); Assign(saleData, "rowTemplate", saleTemplate);
            Assign(saleData, "confirmButton", confirm); Assign(saleData, "backButton", back);
            Assign(saleData, "titleText", saleTitle); Assign(saleData, "statusText", saleStatus);
            saleData.ApplyModifiedPropertiesWithoutUndo();

            var hudData = new SerializedObject(hud);
            Assign(hudData, "menuButton", menuButton); Assign(hudData, "saleButton", saleButton);
            Assign(hudData, "inventoryText", inventory);
            Place(hudData.FindProperty("startButton").objectReferenceValue as Component, new Vector2(0, -470), new Vector2(300, 65));
            Place(hudData.FindProperty("restartButton").objectReferenceValue as Component, new Vector2(0, -470), new Vector2(300, 65));
            Button start = hudData.FindProperty("startButton").objectReferenceValue as Button;
            start.GetComponentInChildren<TMP_Text>().text = "选择菜品";
            Component fill = hudData.FindProperty("recipeProgressFill").objectReferenceValue as Component;
            if (fill != null) Place(fill.transform.parent.GetComponent<RectTransform>(), new Vector2(0, -350), new Vector2(620, 28));
            Place(hudData.FindProperty("operationProgressBar").objectReferenceValue as GameObject, new Vector2(0, -395), new Vector2(620, 28));
            hudData.ApplyModifiedPropertiesWithoutUndo();

            var controllerData = new SerializedObject(controller);
            Assign(controllerData, "catalog", AssetDatabase.LoadAssetAtPath<DishCatalogDefinition>("Assets/Cooking/DishCatalog.asset"));
            Assign(controllerData, "menu", menu); Assign(controllerData, "sale", sale);
            controllerData.ApplyModifiedPropertiesWithoutUndo();
            menuRoot.gameObject.SetActive(false); saleRoot.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void Assign(SerializedObject data, string name, Object value)
            => data.FindProperty(name).objectReferenceValue = value;

        private static void Remove(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 size)
        {
            RectTransform root = Rect(name, parent, Vector2.zero, size);
            root.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.14f, 0.19f, 0.98f);
            return root;
        }

        private static TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize)
        {
            TMP_Text text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = fontSize;
            text.color = new Color(0.94f, 0.94f, 0.90f); text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static Button Button(string name, Transform parent, Vector2 position, Vector2 size, string label)
        {
            RectTransform rect = Rect(name, parent, position, size);
            Image image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.22f, 0.26f, 0.30f);
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            TMP_Text text = Text("Label", rect, Vector2.zero, size - new Vector2(16, 8), label, 25);
            text.enableAutoSizing = true; text.fontSizeMin = 18; text.fontSizeMax = 25;
            return button;
        }

        private static Transform Scroll(Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform root = Rect("List", parent, position, size);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            root.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.11f, 0.15f);
            root.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Content", root, Vector2.zero, new Vector2(0, size.y));
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10); layout.spacing = 12;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = root; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }

        private static Button Row(Transform content, bool icon)
        {
            Button row = Button("RowTemplate", content, Vector2.zero, new Vector2(530, 80), "菜品");
            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 80;
            TMP_Text label = row.GetComponentInChildren<TMP_Text>();
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(icon ? 85 : 10, 8);
            label.rectTransform.offsetMax = new Vector2(-10, -8);
            if (icon)
            {
                RectTransform picture = Rect("Icon", row.transform, new Vector2(45, 0), new Vector2(58, 58));
                picture.anchorMin = picture.anchorMax = new Vector2(0, 0.5f);
                picture.gameObject.AddComponent<Image>().raycastTarget = false;
            }
            row.gameObject.SetActive(false);
            return row;
        }

        private static void Place(Object value, Vector2 position, Vector2 size)
        {
            GameObject go = value as GameObject;
            Component component = value as Component;
            RectTransform rect = component != null ? component.GetComponent<RectTransform>() : go?.GetComponent<RectTransform>();
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
    }
}
#endif
