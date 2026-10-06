using System;
using System.Collections.Generic;
using Cook.Configuration;
using Cook.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cook.Presentation
{
    /// <summary>读取只读展示配置，选择保留在面板内，确认后才请求制作。</summary>
    public sealed class MenuPanel : BasePanel
    {
        [SerializeField] private Transform content;
        [SerializeField] private Button rowTemplate;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text statusText;
        private readonly List<Button> rows = new List<Button>();
        private DishDefinition selected;
        private bool blocked;

        public event Action<DishDefinition> DishSelected;

        private void Awake()
        {
            confirmButton.onClick.AddListener(Confirm);
            closeButton.onClick.AddListener(Hide);
        }

        public void Configure(IReadOnlyList<DishDefinition> dishes, DishDefinition current, bool active, bool full)
        {
            foreach (Button row in rows) Destroy(row.gameObject);
            rows.Clear();
            selected = current;
            foreach (DishDefinition dish in dishes)
            {
                if (dish == null) continue;
                Button row = Instantiate(rowTemplate, content);
                row.gameObject.SetActive(true);
                row.GetComponentInChildren<TMP_Text>().text = dish.DisplayName;
                Image icon = row.transform.Find("Icon").GetComponent<Image>();
                icon.sprite = dish.Icon;
                icon.color = dish.Icon != null ? Color.white : new Color(0.95f, 0.76f, 0.42f);
                DishDefinition captured = dish;
                row.onClick.AddListener(() => { selected = captured; RenderSelection(dishes); });
                rows.Add(row);
            }
            SetAvailability(active, full);
            RenderSelection(dishes);
        }

        private void RenderSelection(IReadOnlyList<DishDefinition> dishes)
        {
            int index = 0;
            foreach (DishDefinition dish in dishes)
            {
                if (dish == null) continue;
                rows[index++].GetComponent<Image>().color = dish == selected
                    ? new Color(0.34f, 0.56f, 0.49f) : new Color(0.22f, 0.26f, 0.30f);
            }
            confirmButton.interactable = !blocked && selected != null;
        }

        public void SetAvailability(bool active, bool full)
        {
            blocked = full;
            confirmButton.interactable = !blocked && selected != null;
            confirmButton.GetComponentInChildren<TMP_Text>().text = active ? "取消当前并制作" : "开始制作";
            statusText.text = selected == null ? "当前菜单没有可制作菜品" : full ? "库存已满，请关闭菜单进入售卖"
                : active ? "浏览菜单时制作计时继续；确认会取消当前菜品" : "选择菜品后开始制作";
        }

        private void Confirm()
        {
            if (!blocked && selected != null) DishSelected?.Invoke(selected);
        }

        private void OnDestroy()
        {
            confirmButton.onClick.RemoveListener(Confirm);
            closeButton.onClick.RemoveListener(Hide);
        }
    }
}
