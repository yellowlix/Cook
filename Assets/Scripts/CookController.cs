using Cook.Configuration;
using Cook.Core;
using Cook.Managers;
using Cook.Presentation;
using System.Collections.Generic;
using UnityEngine;

namespace Cook
{
    /// <summary>协调一道菜的制作、场景输入和通知，不实现输入或制作规则。</summary>
    public sealed class CookController : MonoBehaviour
    {
        [SerializeField] private DishDefinition dish;
        [SerializeField] private ProductionStation startingStation = ProductionStation.SoupPot;
        [SerializeField] private CookHud hud;
        [SerializeField] private DishCatalogDefinition catalog;
        [SerializeField] private MenuPanel menu;
        [SerializeField] private SalePanel sale;
        private GameManager managers;
        private DishRuntimeData runtimeDish;
        private int observedRunIndex;
        private readonly List<DishDefinition> availableDishes = new List<DishDefinition>();
        private IReadOnlyList<StoredDish> saleDishes = new List<StoredDish>().AsReadOnly();

        public ProductionSession Session { get; private set; }
        public ProductionStation Station { get; private set; }
        public ShopPhase Phase { get; private set; } = ShopPhase.Production;
        public DishDefinition SelectedDish => dish;
        public IReadOnlyList<DishDefinition> AvailableDishes => availableDishes.AsReadOnly();
        public IReadOnlyList<StoredDish> SaleDishes => saleDishes;
        public ProductionResult PendingResult { get; private set; }

        private void Awake()
        {
            managers = GameManager.Instance;
            if (managers == null)
            {
                Debug.LogError("CookController requires a GameManager", this);
                enabled = false;
                return;
            }
            if (catalog != null)
            {
                var ids = new HashSet<string>();
                foreach (DishDefinition candidate in catalog.Dishes)
                {
                    if (candidate == null) continue;
                    if (!candidate.TryBuildRuntime(out DishRuntimeData data, out string error))
                    { Debug.LogWarning(error, candidate); continue; }
                    if (ids.Add(data.Id)) availableDishes.Add(candidate);
                }
            }
            if (!availableDishes.Contains(dish)) dish = availableDishes.Count > 0 ? availableDishes[0] : null;
            if (dish != null) dish.TryBuildRuntime(out runtimeDish, out _);
            Station = startingStation;
        }

        private void OnEnable()
        {
            if (managers == null) return;
            managers.Input.SetGameplayEnabled(false);
            managers.Input.StationChangeRequested += ChangeStation;
            managers.Input.CookPressed += OnPressed;
            managers.Input.CookReleased += OnReleased;
            managers.Input.OperationCompleted += RecordOperation;
            managers.Input.ProgressChanged += OnInputProgress;
            if (hud != null)
            {
                managers.UI.Register(hud);
                hud.StartRequested += OpenMenu;
                hud.RestartRequested += RestartProduction;
                hud.MenuRequested += OpenMenu;
                hud.SaleRequested += EnterSaleSelection;
            }
            if (menu != null)
            {
                managers.UI.Register(menu);
                menu.DishSelected += MakeSelectedDish;
            }
            if (sale != null)
            {
                managers.UI.Register(sale);
                sale.SelectionConfirmed += ConfirmSale;
                sale.BackRequested += ReturnToProduction;
            }
        }

        private void Start()
        {
            if (hud != null)
            {
                managers.UI.Show<CookHud>();
                hud.ShowReady();
            }
            managers.Events.Publish(new StationChanged(Station));
            if (menu != null) menu.Hide();
            if (sale != null) sale.Hide();
            OpenMenu();
        }

        public void StartProduction()
        {
            if (Session?.State == ProductionState.Active) return;
            BeginProduction();
        }

        public void RestartProduction()
        {
            if (Session?.State != ProductionState.Active) return;
            BeginProduction();
        }

        private void BeginProduction()
        {
            if (!isActiveAndEnabled || Phase != ShopPhase.Production) return;
            if (PendingResult != null || managers.Inventory.IsFull)
            { Notify("库存已满，请进入售卖"); return; }
            if (dish == null || !dish.TryBuildRuntime(out runtimeDish, out string error))
            { Notify("未选择有效菜品"); return; }
            CancelProduction();
            DetachSession();
            Session = new ProductionSession(runtimeDish);
            observedRunIndex = 0;
            Session.Changed += OnSessionChanged;
            Session.Completed += OnCompleted;
            managers.Input.ResetOperation();
            managers.Input.SetGameplayEnabled(true);
            menu?.Hide();
            managers.Events.Publish(new ProductionStarted(Session));
            managers.Events.Publish(new StationChanged(Station));
            OnSessionChanged();
        }

        public void OpenMenu()
        {
            if (Phase != ShopPhase.Production || menu == null) return;
            menu.Configure(AvailableDishes, dish, Session?.State == ProductionState.Active,
                managers.Inventory.IsFull || PendingResult != null);
            managers.UI.Show<MenuPanel>();
        }

        public void MakeSelectedDish(DishDefinition selected)
        {
            if (Phase != ShopPhase.Production || !availableDishes.Contains(selected)) return;
            if (managers.Inventory.IsFull || PendingResult != null)
            { Notify("库存已满，请进入售卖"); return; }
            if (selected == null || !selected.TryBuildRuntime(out _, out string error))
            { Notify("菜品配置无效"); return; }
            // 菜单确认按钮明确告知：此请求会取消正在制作的菜品。
            CancelProduction();
            dish = selected;
            BeginProduction();
        }

        public void EnterSaleSelection()
        {
            if (Phase != ShopPhase.Production || sale == null) return;
            if (PendingResult != null) { Notify("成品尚未入库，暂不能离开"); return; }
            if (managers.Inventory.Items.Count == 0) { Notify("库存为空，请先制作菜品"); return; }
            CancelProduction();
            menu?.Hide();
            SetPhase(ShopPhase.SaleSelection);
            sale.Configure(managers.Inventory.Items);
            managers.UI.Show<SalePanel>();
        }

        public void ConfirmSale(IReadOnlyList<string> ids)
        {
            if (Phase != ShopPhase.SaleSelection || ids == null || ids.Count < 1 || ids.Count > 3) return;
            var selection = new List<StoredDish>();
            var unique = new HashSet<string>();
            foreach (string id in ids)
            {
                StoredDish item = managers.Inventory.Find(id);
                if (item == null || !unique.Add(id)) return;
                selection.Add(item);
            }
            saleDishes = selection.AsReadOnly();
            SetPhase(ShopPhase.SaleReady);
            sale.ShowReady(saleDishes);
        }

        public void ReturnToProduction()
        {
            if (Phase == ShopPhase.Production) return;
            saleDishes = new List<StoredDish>().AsReadOnly();
            sale?.Hide();
            SetPhase(ShopPhase.Production);
            hud?.ShowReady();
            OpenMenu();
        }

        private void SetPhase(ShopPhase phase)
        {
            Phase = phase;
            managers.Events.Publish(new ShopPhaseChanged(phase));
        }

        private void Notify(string message) => hud?.ShowMessage(message);

        public void CancelProduction()
        {
            if (managers == null) return;
            managers.Input.SetGameplayEnabled(false);
            managers.Input.ResetOperation();
            Session?.Cancel();
        }

        private void Update()
        {
            Session?.Tick(Time.deltaTime);
        }

        public void ChangeStation(int direction)
        {
            if (Session?.State != ProductionState.Active || direction == 0) return;
            Station = (ProductionStation)(((int)Station + (direction < 0 ? 2 : 1)) % 3);
            managers.Input.ResetOperation();
            managers.Events.Publish(new StationChanged(Station));
        }

        private void RecordOperation(ProductionInteractionMode mode)
        {
            if (Session?.State != ProductionState.Active) return;
            var operation = new ProductionOperationData(Station, mode);
            if (Session.TryRecordOperation(operation))
                managers.Events.Publish(new OperationPerformed(operation));
        }

        private void OnSessionChanged()
        {
            if (Session.RunIndex != observedRunIndex || Session.State != ProductionState.Active)
            {
                observedRunIndex = Session.RunIndex;
                managers.Input.ResetOperation();
            }
            if (Session.State != ProductionState.Active) managers.Input.SetGameplayEnabled(false);
            managers.Events.Publish(new ProductionChanged(Session));
            if (menu != null && menu.IsShow)
                menu.SetAvailability(Session.State == ProductionState.Active, managers.Inventory.IsFull || PendingResult != null);
        }

        private void OnCompleted(ProductionResult result)
        {
            PendingResult = result;
            if (managers.Inventory.TryAdd(result, out StoredDish stored))
            {
                PendingResult = null;
                managers.Events.Publish(new InventoryChanged());
                Notify(managers.Inventory.IsFull ? "制作完成，库存已满，请进入售卖" : "制作完成，已入库");
            }
            else Notify("库存已满，成品保留待入库");
            managers.Events.Publish(new ProductionFinished(result));
            if (menu != null && menu.IsShow) menu.SetAvailability(false, managers.Inventory.IsFull || PendingResult != null);
        }

        private void OnPressed()
        {
            if (Session?.State == ProductionState.Active) managers.Events.Publish(new CookInputPressed());
        }

        private void OnReleased() => managers.Events.Publish(new CookInputReleased());

        private void OnInputProgress(ProductionInteractionMode mode, float value)
            => managers.Events.Publish(new OperationInputProgress(mode, value));

        private void OnDisable()
        {
            if (managers == null) return;
            CancelProduction();
            DetachSession();
            managers.Input.StationChangeRequested -= ChangeStation;
            managers.Input.CookPressed -= OnPressed;
            managers.Input.CookReleased -= OnReleased;
            managers.Input.OperationCompleted -= RecordOperation;
            managers.Input.ProgressChanged -= OnInputProgress;
            if (hud != null)
            {
                hud.StartRequested -= OpenMenu;
                hud.RestartRequested -= RestartProduction;
                hud.MenuRequested -= OpenMenu;
                hud.SaleRequested -= EnterSaleSelection;
                managers.UI.Unregister(hud);
            }
            if (menu != null) { menu.DishSelected -= MakeSelectedDish; managers.UI.Unregister(menu); }
            if (sale != null)
            {
                sale.SelectionConfirmed -= ConfirmSale;
                sale.BackRequested -= ReturnToProduction;
                managers.UI.Unregister(sale);
            }
        }

        private void DetachSession()
        {
            if (Session == null) return;
            Session.Changed -= OnSessionChanged;
            Session.Completed -= OnCompleted;
        }
    }
}
