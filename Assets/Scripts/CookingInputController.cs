using Cook.Core;
using Cook.Input;
using Cook.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
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

        private CookInputActions inputs;
        private RecipeRuntimeData runtimeRecipe;
        private CoreCookingSession session;
        private float resultElapsed;

        public CoreCookingSession Session => session;

        private void Awake()
        {
            inputs = new CookInputActions();
        }

        private void OnEnable()
        {
            inputs.Player.ChangeStation.performed += OnChangeStation;
            inputs.Player.Cook.performed += OnCookPressed;
            inputs.Player.Cook.canceled += OnCookReleased;
            inputs.Player.Enable();
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
            hud.ShowReady();
        }

        private void StartRun()
        {
            if (runtimeRecipe == null) return;
            animationPresenter?.Unbind();
            hud?.Unbind();
            session = new CoreCookingSession();
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
            inputs.Player.ChangeStation.performed -= OnChangeStation;
            inputs.Player.Cook.performed -= OnCookPressed;
            inputs.Player.Cook.canceled -= OnCookReleased;
            inputs.Player.Disable();
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
            inputs?.Dispose();
        }

        private void OnChangeStation(InputAction.CallbackContext context)
        {
            float axis = context.ReadValue<float>();
            if (Mathf.Abs(axis) >= 0.5f) session?.MoveStation(axis < 0f ? -1 : 1);
        }

        private void OnCookPressed(InputAction.CallbackContext context) => session?.PressCook();
        private void OnCookReleased(InputAction.CallbackContext context) => session?.ReleaseCook();
    }
}
