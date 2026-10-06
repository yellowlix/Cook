using System;
using Cook.Core;
using Cook.Input;
using UnityEngine.InputSystem;

namespace Cook.Managers
{
    /// <summary>适配 Input System，提供原始按键事件与已完成交互事件。</summary>
    public sealed class GameInputManager : IDisposable
    {
        private readonly CookInputActions actions;
        private readonly OperationInput recognition;
        private bool enabled;
        private bool disposed;

        public GameInputManager(float clickInterval)
        {
            recognition = new OperationInput(clickInterval);
            actions = new CookInputActions();
            actions.Player.Cook.AddBinding("<Keyboard>/space");
            actions.Player.ChangeStation.performed += OnStation;
            actions.Player.Cook.performed += OnPressed;
            actions.Player.Cook.canceled += OnReleased;
            recognition.Completed += OnCompleted;
            recognition.ProgressChanged += OnProgress;
        }

        public event Action<int> StationChangeRequested;
        public event Action CookPressed;
        public event Action CookReleased;
        public event Action<ProductionInteractionMode> OperationCompleted;
        public event Action<ProductionInteractionMode, float> ProgressChanged;

        public void SetGameplayEnabled(bool value)
        {
            if (disposed || enabled == value) return;
            enabled = value;
            if (value) actions.Player.Enable();
            else actions.Player.Disable();
            recognition.Reset();
        }

        public void Tick(float deltaTime)
        {
            if (enabled && !disposed) recognition.Tick(deltaTime);
        }

        public void ResetOperation() => recognition.Reset();

        public void Dispose()
        {
            if (disposed) return;
            SetGameplayEnabled(false);
            disposed = true;
            actions.Player.ChangeStation.performed -= OnStation;
            actions.Player.Cook.performed -= OnPressed;
            actions.Player.Cook.canceled -= OnReleased;
            recognition.Completed -= OnCompleted;
            recognition.ProgressChanged -= OnProgress;
            actions.Dispose();
            StationChangeRequested = null;
            CookPressed = CookReleased = null;
            OperationCompleted = null;
            ProgressChanged = null;
        }

        private void OnStation(InputAction.CallbackContext context)
        {
            float axis = context.ReadValue<float>();
            if (Math.Abs(axis) < 0.5f) return;
            recognition.Reset();
            StationChangeRequested?.Invoke(axis < 0f ? -1 : 1);
        }

        private void OnPressed(InputAction.CallbackContext context)
        {
            recognition.Press();
            CookPressed?.Invoke();
        }

        private void OnReleased(InputAction.CallbackContext context)
        {
            recognition.Release();
            CookReleased?.Invoke();
        }

        private void OnCompleted(ProductionInteractionMode mode) => OperationCompleted?.Invoke(mode);
        private void OnProgress(ProductionInteractionMode mode, float value) => ProgressChanged?.Invoke(mode, value);
    }
}
