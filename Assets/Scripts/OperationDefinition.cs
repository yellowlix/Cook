using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "OperationDefinition", menuName = "ScriptableObjects/OperationDefinition", order = 1)]
public class OperationDefinition : ScriptableObject
{
    public string operationName;
    public OperationType operationType;
    public double time;
    public string animatorTrigger;
    public StandPosition standPosition;
    public Sprite sprite;
}

public enum StandPosition
{
    Left = 0,
    Middle = 1,
    Right = 2,
}

public enum OperationType
{
    Once = 0,
    Loop = 1,
}