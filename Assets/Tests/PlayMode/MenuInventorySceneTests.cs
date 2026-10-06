using System.Collections;
using Cook.Managers;
using Cook;
using Cook.Core;
using Cook.Presentation;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine;

public sealed class MenuInventorySceneTests
{
    [UnitySetUp]
    public IEnumerator FreshScene()
    {
        if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("GameScene");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Menu_StartsSelectedDishAndCompletionStoresOnce()
    {
        var controller = Object.FindFirstObjectByType<CookController>();
        var menu = Object.FindFirstObjectByType<MenuPanel>();
        Assert.That(menu.IsShow, Is.True);
        Assert.That(controller.AvailableDishes.Count, Is.GreaterThan(0));
        controller.MakeSelectedDish(controller.AvailableDishes[0]);
        ProductionSession session = controller.Session;
        controller.StartProduction();
        Assert.That(controller.Session, Is.SameAs(session), "重复开始不能重置制作");
        Assert.That(menu.IsShow, Is.False);
        session.Tick(60f);
        session.Tick(60f);
        Assert.That(GameManager.Instance.Inventory.Items.Count, Is.EqualTo(1));
        Assert.That(GameManager.Instance.Inventory.Items[0].Result, Is.SameAs(session.Result));
        yield return null;
    }

    [UnityTest]
    public IEnumerator FullInventory_BlocksProductionAndSaleSelectionKeepsStock()
    {
        var controller = Object.FindFirstObjectByType<CookController>();
        for (int i = 0; i < 3; i++) { controller.StartProduction(); controller.Session.Tick(60f); }
        ProductionSession last = controller.Session;
        controller.StartProduction();
        Assert.That(controller.Session, Is.SameAs(last));
        controller.EnterSaleSelection();
        Assert.That(controller.Phase, Is.EqualTo(ShopPhase.SaleSelection));
        controller.ConfirmSale(new string[0]);
        Assert.That(controller.Phase, Is.EqualTo(ShopPhase.SaleSelection));
        string id = GameManager.Instance.Inventory.Items[0].Id;
        controller.ConfirmSale(new[] { id, id });
        Assert.That(controller.Phase, Is.EqualTo(ShopPhase.SaleSelection));
        controller.ConfirmSale(new[] { "missing" });
        Assert.That(controller.Phase, Is.EqualTo(ShopPhase.SaleSelection));
        controller.ConfirmSale(new[] { id });
        Assert.That(controller.Phase, Is.EqualTo(ShopPhase.SaleReady));
        Assert.That(controller.SaleDishes.Count, Is.EqualTo(1));
        Assert.That(GameManager.Instance.Inventory.Items.Count, Is.EqualTo(3));
        controller.ReturnToProduction();
        Assert.That(controller.SaleDishes, Is.Empty);
        Assert.That(controller.Phase, Is.EqualTo(ShopPhase.Production));
        Assert.That(GameManager.Instance.Inventory.Items.Count, Is.EqualTo(3));
        yield return null;
    }

    [UnityTest]
    public IEnumerator RestartAndLeavingProduction_KeepStoredDishAndCancelOnlyUnfinished()
    {
        var controller = Object.FindFirstObjectByType<CookController>();
        controller.StartProduction(); controller.Session.Tick(60f);
        controller.StartProduction();
        ProductionSession unfinished = controller.Session;
        controller.OpenMenu();
        Assert.That(unfinished.State, Is.EqualTo(ProductionState.Active));
        controller.RestartProduction();
        Assert.That(unfinished.State, Is.EqualTo(ProductionState.Cancelled));
        Assert.That(controller.Session.Dish.Id, Is.EqualTo(unfinished.Dish.Id));
        Assert.That(GameManager.Instance.Inventory.Items.Count, Is.EqualTo(1));
        ProductionSession restarted = controller.Session;
        controller.EnterSaleSelection();
        Assert.That(restarted.State, Is.EqualTo(ProductionState.Cancelled));
        Assert.That(GameManager.Instance.Inventory.Items.Count, Is.EqualTo(1));
        yield return null;
    }
}
