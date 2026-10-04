using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;

public sealed class CookingFontAssetTests
{
    private const string FontAssetPath = "Assets/Fonts/NotoSansSC-CookingSubset SDF.asset";
    private const string RequiredCharacters =
        "切一下连续菜搅持续动翻炒完成失败料理练习✓> []ExcellentGreatGoodMiss";

    [Test]
    public void HudFontAsset_ContainsEveryAuthoredCharacter()
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        Assert.That(fontAsset, Is.Not.Null, $"Missing TMP font asset at {FontAssetPath}.");

        bool containsAll = fontAsset.HasCharacters(RequiredCharacters, out List<char> missingCharacters);

        Assert.That(containsAll, Is.True,
            $"TMP HUD font is missing: {new string(missingCharacters.ToArray())}");
    }
}
