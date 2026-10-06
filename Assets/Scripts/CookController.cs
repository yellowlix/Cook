using Cook.Configuration;
using Cook.Core;
using Cook.Managers;
using Cook.Presentation;
using UnityEngine;

namespace Cook
{
    /// <summary>协调一道菜的制作、场景输入和通知，不实现输入或制作规则。</summary>
    public sealed class CookController : MonoBehaviour
    {
        [SerializeField] private DishDefinition dish;
        [SerializeField] private ProductionStation startingStation = ProductionStation.SoupPot;
        [SerializeField] private CookHud hud;
        private GameManager managers;
        private DishRuntimeData runtimeDish;
        private int observedRunIndex;

        public ProductionSession Session { get; private set; }
        public ProductionStation Station { get; private set; }

        private void Awake()
        {
            managers = GameManager.Instance;
            if (managers == null || dish == null)
            {
                Debug.LogError("CookController requires a GameManager and DishDefinition", this);
                enabled = false;
                return;
            }
            if (!dish.TryBuildRuntime(out runtimeDish, out string error))
            {
                Debug.LogError(error, this);
                enabled = false;
                return;
            }
            Station = startingStation;
        }

        private void OnEnable()
        {
            if (managers == null || runtimeDish == null) return;
            managers.Input.SetGameplayEnabled(false);
            managers.Input.StationChangeRequested += ChangeStation;
            managers.Input.CookPressed += OnPressed;
            managers.Input.CookReleased += OnReleased;
            managers.Input.OperationCompleted += RecordOperation;
            managers.Input.ProgressChanged += OnInputProgress;
            if (hud != null)
            {
                managers.UI.Register(hud);
                hud.StartRequested += StartProduction;
                hud.RestartRequested += StartProduction;
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
        }

        public void StartProduction()
        {
            if (!isActiveAndEnabled || runtimeDish == null) return;
            CancelProduction();
            DetachSession();
            Session = new ProductionSession(runtimeDish);
            observedRunIndex = 0;
            Session.Changed += OnSessionChanged;
            Session.Completed += OnCompleted;
            managers.Input.ResetOperation();
            managers.Input.SetGameplayEnabled(true);
            managers.Events.Publish(new ProductionStarted(Session));
            managers.Events.Publish(new StationChanged(Station));
            OnSessionChanged();
        }

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
        }

        private void OnCompleted(ProductionResult result)
            => managers.Events.Publish(new ProductionFinished(result));

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
                hud.StartRequested -= StartProduction;
                hud.RestartRequested -= StartProduction;
                managers.UI.Unregister(hud);
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
