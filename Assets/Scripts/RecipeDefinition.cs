using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Recipe Definition", menuName = "ScriptableObjects/Recipe Definition",order = 2)]
public class RecipeDefinition : ScriptableObject
{
    public int recipeID;
    public string recipeName;
    public string recipeDescription;
    public List<RunDefinition> runList;
    public Sprite recipeSprite;
    public string recipeReword;
}

[System.Serializable]
public class RunDefinition
{
    public List<OperationDefinition> operationList = new List<OperationDefinition>();
}