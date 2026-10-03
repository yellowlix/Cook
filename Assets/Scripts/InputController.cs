using System;
using System.Collections;
using System.Collections.Generic;
using Cook.Input;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputController : MonoBehaviour
{
    public CookInputActions inputs;
    public event Action Pressed;
    public event Action Released;
    public event Action<int> ChangeStation;

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

    private void OnCookReleased(InputAction.CallbackContext obj)
    {
        Released?.Invoke();
    }

    private void OnCookPressed(InputAction.CallbackContext obj)
    {
        Pressed?.Invoke();
    }

    private void OnChangeStation(InputAction.CallbackContext obj)
    { 
        float value = obj.ReadValue<float>();
        if (Mathf.Abs(value) < 0.5f)
        {
            return;
        }
        int direction = value < 0f ? -1 : 1;
        ChangeStation?.Invoke(direction);
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
        inputs.Dispose();
    }
}
