using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CookingController : MonoBehaviour
{
    public InputController inputController;
    public CookingSession cookingSession;

    void Start()
    {
        inputController = GetComponent<InputController>();
        cookingSession = new CookingSession();
        inputController.Pressed += InputControllerOnPressed;
        inputController.Released += InputControllerOnReleased;
        inputController.ChangeStation += InputControllerOnChangeStation;
    }

    public void StartCooking()
    {
        cookingSession.CookingStart();
    }
    private void InputControllerOnChangeStation(int direction)
    {
        
        cookingSession.ChangeStand(direction);
    }

    private void InputControllerOnPressed()
    {
        cookingSession.PressON();
    }

    private void InputControllerOnReleased()
    {
        cookingSession.PressOFF();
    }
}
