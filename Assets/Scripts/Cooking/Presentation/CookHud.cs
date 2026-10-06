using System;
using System.Collections.Generic;
using System.Text;
using Cook.Core;
using Cook.Managers;
using Cook.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Cook.Presentation
{
    public sealed class CookHud : BasePanel
    {
        [SerializeField] private TMP_Text operationSequenceText;
        [SerializeField] private GameObject operationProgressBar;
        [SerializeField] private Image operationProgressFill;
        [SerializeField] private Image recipeProgressFill;
        [FormerlySerializedAs("gradeText"), SerializeField] private TMP_Text actualStepsText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private TMP_Text roundCountdownText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button saleButton;
        [SerializeField] private TMP_Text inventoryText;
        private EventManager events;
        private ProductionSession session;
        private ProductionStation station;
        private ShopPhase phase;

        public event Action StartRequested;
        public event Action RestartRequested;
        public event Action MenuRequested;
        public event Action SaleRequested;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(RequestStart);
            if (restartButton != null) restartButton.onClick.AddListener(RequestRestart);
            if (menuButton != null) menuButton.onClick.AddListener(RequestMenu);
            if (saleButton != null) saleButton.onClick.AddListener(RequestSale);
        }

        private void OnEnable()
        {
            events = GameManager.Instance?.Events;
            if (events == null) return;
            events.Subscribe<ProductionStarted>(OnStarted);
            events.Subscribe<ProductionChanged>(OnChanged);
            events.Subscribe<StationChanged>(OnStation);
            events.Subscribe<OperationInputProgress>(OnProgress);
            events.Subscribe<InventoryChanged>(OnInventory);
            events.Subscribe<ShopPhaseChanged>(OnPhase);
            RefreshInventory();
        }

        private void OnDisable()
        {
            if (events == null) return;
            events.Unsubscribe<ProductionStarted>(OnStarted);
            events.Unsubscribe<ProductionChanged>(OnChanged);
            events.Unsubscribe<StationChanged>(OnStation);
            events.Unsubscribe<OperationInputProgress>(OnProgress);
            events.Unsubscribe<InventoryChanged>(OnInventory);
            events.Unsubscribe<ShopPhaseChanged>(OnPhase);
            events = null;
        }

        private void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(RequestStart);
            if (restartButton != null) restartButton.onClick.RemoveListener(RequestRestart);
            if (menuButton != null) menuButton.onClick.RemoveListener(RequestMenu);
            if (saleButton != null) saleButton.onClick.RemoveListener(RequestSale);
        }

        public void ShowReady()
        {
            session = null;
            SetVisible(startButton, true);
            SetVisible(restartButton, false);
            SetText(operationSequenceText, "按开始制作菜品\nSpace：单击 / 三连击 / 长按 3 秒\nA、D 或方向键切换工位");
            SetText(actualStepsText, string.Empty);
            SetText(roundCountdownText, string.Empty);
            SetText(resultText, string.Empty);
            SetProgress(recipeProgressFill, 0f);
            SetProgress(operationProgressFill, 0f);
            if (operationProgressBar != null) operationProgressBar.SetActive(false);
            RefreshInventory();
        }

        public void ShowMessage(string message) => SetText(resultText, message);
        private void RequestMenu() => MenuRequested?.Invoke();
        private void RequestSale() => SaleRequested?.Invoke();
        private void OnInventory(InventoryChanged message) => RefreshInventory();
        private void OnPhase(ShopPhaseChanged message)
        {
            phase = message.Phase;
            Render();
            RefreshInventory();
        }
        private void RefreshInventory()
        {
            InventoryManager inventory = GameManager.Instance?.Inventory;
            if (inventory == null) return;
            SetText(inventoryText, $"成品库存 {inventory.Items.Count}/{InventoryManager.Capacity}");
            bool production = phase == ShopPhase.Production;
            if (startButton != null) startButton.interactable = production && !inventory.IsFull;
            if (restartButton != null) restartButton.interactable = production && !inventory.IsFull;
            if (menuButton != null) menuButton.interactable = production;
            if (saleButton != null) saleButton.interactable = production && inventory.Items.Count > 0;
            if (saleButton != null) saleButton.GetComponentInChildren<TMP_Text>().text = session?.State == ProductionState.Active
                ? "取消制作并售卖" : "进入售卖";
        }

        private void RequestStart() => StartRequested?.Invoke();
        private void RequestRestart() => RestartRequested?.Invoke();
        private void OnStarted(ProductionStarted message)
        {
            session = message.Session;
            SetText(resultText, string.Empty);
        }

        private void OnChanged(ProductionChanged message)
        {
            session = message.Session;
            Render();
        }

        private void OnStation(StationChanged message)
        {
            station = message.Station;
            Render();
        }

        private void OnProgress(OperationInputProgress message)
        {
            bool show = session?.State == ProductionState.Active && message.Progress > 0f;
            if (operationProgressBar != null) operationProgressBar.SetActive(show);
            SetProgress(operationProgressFill, message.Progress);
        }

        private void Render()
        {
            if (session == null) return;
            bool active = session.State == ProductionState.Active;
            SetVisible(startButton, !active && phase == ShopPhase.Production);
            SetVisible(restartButton, active && phase == ShopPhase.Production);
            RefreshInventory();
            SetText(roundCountdownText, active ? $"工序剩余 {session.RemainingTime:0.0}s" : string.Empty);
            if (active)
            {
                SetText(operationSequenceText, $"{session.Dish.DisplayName}  工序 {session.RunIndex + 1}/{session.Dish.Runs.Count}\n当前工位：{StationLabel(station)}\n指引：{Sequence(session.CurrentRun.Guide)}");
                SetText(actualStepsText, $"实际步骤 {session.ActualOperations.Count}/3：{Sequence(session.ActualOperations)}");
            }
            else
            {
                SetText(operationSequenceText, session.Dish.DisplayName);
                SetText(actualStepsText, $"已完成工序 {session.RunResults.Count}/{session.Dish.Runs.Count}");
                SetText(resultText, session.State == ProductionState.Completed ? "制作完成" : "已取消，未完成进度已丢弃");
                if (operationProgressBar != null) operationProgressBar.SetActive(false);
            }
            float completed = session.RunResults.Count;
            if (active) completed += session.ActualOperations.Count / 3f;
            SetProgress(recipeProgressFill, completed / session.Dish.Runs.Count);
        }

        private static string Sequence(IReadOnlyList<ProductionOperationData> operations)
        {
            if (operations.Count == 0) return "无";
            var text = new StringBuilder();
            for (int i = 0; i < operations.Count; i++)
            {
                if (i > 0) text.Append(" → ");
                text.Append(StationLabel(operations[i].Station)).Append('/').Append(ModeLabel(operations[i].InteractionMode));
            }
            return text.ToString();
        }

        public static string StationLabel(ProductionStation value)
            => value == ProductionStation.SoupPot ? "汤锅" : value == ProductionStation.CuttingBoard ? "砧板" : "平底锅";
        private static string ModeLabel(ProductionInteractionMode value)
            => value == ProductionInteractionMode.Click ? "单击" : value == ProductionInteractionMode.TripleClick ? "三连击" : "长按";
        private static void SetText(TMP_Text target, string value) { if (target != null) target.text = value; }
        private static void SetVisible(Component target, bool value) { if (target != null) target.gameObject.SetActive(value); }
        private static void SetProgress(Image target, float value) { if (target != null) target.fillAmount = Mathf.Clamp01(value); }
    }
}
