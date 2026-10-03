using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CookingSession
{
    public CookingState currentState;
    public event Action AchieveOperation;
    public event Action<RunResult> RunEnd;
    public event Action EndCooking;
    public RecipeDefinition recipe;
    public RunDefinition run;
    public OperationDefinition operation;
    public StandPosition currentStation;
    public bool isPressing;
    public double lastTime;
    

    public void SetRecipe(RecipeDefinition recipe)
    {
        this.recipe = recipe;
    }
    public void CookingStart()
    {
        run = recipe.runList[0];
        operation = run.operationList[0];
    }

    public void ChangeStand(int direction)
    {
        if (direction == 0)
        {
            //向左切换
        }
        else if (direction == 1)
        {
            //向右切换
        }
    }

    public void PressON()
    {
        isPressing = true;
        lastTime = Time.realtimeSinceStartup;
    }

    public void PressOFF()
    {
        isPressing = false;
    }

    void Tick()
    {
        var deltaTime = Time.realtimeSinceStartup - lastTime;
        if (isPressing)
        {
            if (operation.operationType == OperationType.Once)
            {
                //播放动画
                AchieveOperation?.Invoke();
            }

            if (operation.operationType == OperationType.Loop)
            {
                //
            }
        }
    }
}

public class RunResult
{
    
}
public enum CookingState
{
    CookingStart,
    RunStart,
    OperationStart,
    OperationEnd,
    RunEnd,
    CookingEnd,
}