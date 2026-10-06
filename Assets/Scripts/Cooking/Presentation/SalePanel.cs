using System;
using System.Collections.Generic;
using System.Text;
using Cook.Core;
using Cook.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cook.Presentation
{
    /// <summary>售卖入口：选择成品及显示准备结果，不运行顾客或售卖判定。</summary>
    public sealed class SalePanel : BasePanel
    {
        [SerializeField] private Transform content;
        [SerializeField] private Button rowTemplate;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        private readonly List<string> selected = new List<string>();
        private readonly List<Button> rows = new List<Button>();
        private bool ready;

        public event Action<IReadOnlyList<string>> SelectionConfirmed;
        public event Action BackRequested;

        private void Awake()
        {
            confirmButton.onClick.AddListener(Confirm);
            backButton.onClick.AddListener(Back);
        }

        public void Configure(IReadOnlyList<StoredDish> items)
        {
            foreach (Button row in rows) Destroy(row.gameObject);
            rows.Clear(); selected.Clear(); ready = false;
            titleText.text = "售卖选菜";
            statusText.rectTransform.anchoredPosition = new Vector2(0, -150);
            statusText.rectTransform.sizeDelta = new Vector2(620, 170);
            content.gameObject.SetActive(true);
            confirmButton.gameObject.SetActive(true);
            for (int i = 0; i < items.Count; i++)
            {
                StoredDish item = items[i];
                Button row = Instantiate(rowTemplate, content);
                row.gameObject.SetActive(true);
                row.GetComponentInChildren<TMP_Text>().text = $"成品 {i + 1}  {item.Result.Dish.DisplayName}";
                string id = item.Id;
                row.onClick.AddListener(() => Toggle(id, row));
                rows.Add(row);
            }
            RenderCount();
        }

        private void Toggle(string id, Button row)
        {
            if (ready) return;
            if (selected.Contains(id)) selected.Remove(id);
            else if (selected.Count < 3) selected.Add(id);
            row.GetComponent<Image>().color = selected.Contains(id)
                ? new Color(0.34f, 0.56f, 0.49f) : new Color(0.22f, 0.26f, 0.30f);
            RenderCount();
        }

        private void RenderCount()
        {
            statusText.text = $"选择 1–3 道成品，已选 {selected.Count}/3\n返回制作保留全部库存";
            confirmButton.interactable = selected.Count > 0;
        }

        public void ShowReady(IReadOnlyList<StoredDish> items)
        {
            ready = true;
            titleText.text = "售卖准备";
            statusText.rectTransform.anchoredPosition = Vector2.zero;
            statusText.rectTransform.sizeDelta = new Vector2(620, 400);
            content.gameObject.SetActive(false);
            confirmButton.gameObject.SetActive(false);
            var text = new StringBuilder("待售菜品：\n");
            foreach (StoredDish item in items) text.Append("• ").Append(item.Result.Dish.DisplayName).Append('\n');
            text.Append("已进入售卖入口\n顾客与售卖判定尚未接入，库存未扣除");
            statusText.text = text.ToString();
        }

        private void Confirm()
        {
            if (!ready && selected.Count > 0) SelectionConfirmed?.Invoke(selected.AsReadOnly());
        }
        private void Back() => BackRequested?.Invoke();
        private void OnDestroy()
        {
            confirmButton.onClick.RemoveListener(Confirm);
            backButton.onClick.RemoveListener(Back);
        }
    }
}
