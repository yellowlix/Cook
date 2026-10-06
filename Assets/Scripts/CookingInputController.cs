using Cook.Core;
using Cook.Managers;
using Cook.Presentation;
using UnityEngine;
using ConfigRecipeDefinition = Cook.Configuration.RecipeDefinition;
using CoreCookingSession = Cook.Core.CookingSession;

namespace Cook
{
    /// <summary>Unity 输入与生命周期适配器，玩法规则全部委托给 CookingSession。</summary>
    public sealed class CookingInputController : MonoBehaviour
    {
        [SerializeField] private ConfigRecipeDefinition recipe;
        [SerializeField] private CookingStation startingStation = CookingStation.SoupPot;
        [SerializeField] private CookingAnimationPresenter animationPresenter;
        [SerializeField] private CookingHud hud;
        [SerializeField, Min(0f)] private float roundResultDisplaySeconds = 0.75f;

        private GameManager managers;
        private GameInputManager inputs;
        private RecipeRuntimeData runtimeRecipe;
        private CoreCookingSession session;
        private float resultElapsed;

        public CoreCookingSession Session => session;

        private void Awake()
        {
            managers = GameManager.Instance;
            if (managers == null)
            {
                Debug.LogError("GameScene requires a GameManager entry", this);
                enabled = false;
                return;
            }
            inputs = managers.Input;
        }

        private void OnEnable()
        {
            if (inputs == null) return;
            inputs.StationChangeRequested += OnChangeStation;
            inputs.CookPressed += OnCookPressed;
            inputs.CookReleased += OnCookReleased;
        }

        private void Start()
        {
            if (recipe == null)
            {
                Debug.LogError("Cannot start cooking: recipe is not assigned", this);
                enabled = false;
                return;
            }

            if (!recipe.TryBuildRuntime(out runtimeRecipe, out string error))
            {
                Debug.LogError($"Cannot start cooking: {error}", this);
                enabled = false;
                return;
            }

            if (hud == null)
            {
                Debug.LogError("Cannot start cooking: HUD is not assigned", this);
                enabled = false;
                return;
            }

            hud.StartRequested += StartRun;
            hud.RestartRequested += StartRun;
            managers.UI.Register(hud);
            managers.UI.Show<CookingHud>();
            hud.ShowReady();
        }

        private void StartRun()
        {
            if (runtimeRecipe == null) return;
            inputs.ResetOperation();
            managers.Audio.PlayUIEffect(managers.ButtonSound);
            UnbindSessionAudio();
            animationPresenter?.Unbind();
            hud?.Unbind();
            session = new CoreCookingSession();
            session.OperationCompleted += OnOperationCompleted;
            session.RecipeSucceeded += OnRecipeSucceeded;
            animationPresenter?.Bind(session);
            hud?.Bind(session);
            resultElapsed = 0f;
            session.StartRecipe(runtimeRecipe, startingStation);
        }

        private void Update()
        {
            if (session == null) return;
            session.Tick(Time.deltaTime);
            if (session.State != CookingSessionState.RoundResult)
            {
                resultElapsed = 0f;
                return;
            }

            resultElapsed += Time.deltaTime;
            if (resultElapsed >= roundResultDisplaySeconds)
            {
                resultElapsed = 0f;
                session.ContinueAfterRoundResult();
            }
        }

        private void OnDisable()
        {
            if (inputs == null) return;
            inputs.StationChangeRequested -= OnChangeStation;
            inputs.CookPressed -= OnCookPressed;
            inputs.CookReleased -= OnCookReleased;
            inputs.ResetOperation();
        }

        private void OnDestroy()
        {
            if (hud != null)
            {
                hud.StartRequested -= StartRun;
                hud.RestartRequested -= StartRun;
            }
            animationPresenter?.Unbind();
            hud?.Unbind();
            UnbindSessionAudio();
            if (managers != null) managers.UI.Unregister(hud);
        }

        private void OnChangeStation(int direction)
        {
            session?.MoveStation(direction);
        }

        private void OnCookPressed() => session?.PressCook();
        private void OnCookReleased() => session?.ReleaseCook();

        private void OnOperationCompleted(OperationRuntimeData operation)
            => managers.Audio.PlaySceneEffect(managers.OperationSound);

        private void OnRecipeSucceeded()
            => managers.Audio.PlaySceneEffect(managers.CompletionSound);

        private void UnbindSessionAudio()
        {
            if (session == null) return;
            session.OperationCompleted -= OnOperationCompleted;
            session.RecipeSucceeded -= OnRecipeSucceeded;
        }
    }
}
