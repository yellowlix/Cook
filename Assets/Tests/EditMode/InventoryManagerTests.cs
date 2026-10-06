using System;
using Cook.Core;
using Cook.Managers;
using NUnit.Framework;

public sealed class InventoryManagerTests
{
    [Test]
    public void Capacity_RejectsFourthAndKeepsOriginalItems()
    {
        var inventory = new InventoryManager();
        for (int i = 0; i < 3; i++) Assert.That(inventory.TryAdd(Result(), out _), Is.True);
        Assert.That(inventory.IsFull, Is.True);
        Assert.That(inventory.TryAdd(Result(), out StoredDish rejected), Is.False);
        Assert.That(rejected, Is.Null);
        Assert.That(inventory.Items.Count, Is.EqualTo(3));
    }

    [Test]
    public void SameResult_IsIdempotentEvenWhenFull()
    {
        var inventory = new InventoryManager();
        ProductionResult result = Result();
        inventory.TryAdd(result, out StoredDish first);
        inventory.TryAdd(Result(), out _);
        inventory.TryAdd(Result(), out _);
        Assert.That(inventory.TryAdd(result, out StoredDish again), Is.True);
        Assert.That(again, Is.SameAs(first));
        Assert.That(inventory.Items.Count, Is.EqualTo(3));
    }

    [Test]
    public void SameRecipe_ProducesIndependentInstancesAndRecords()
    {
        var inventory = new InventoryManager();
        inventory.TryAdd(Result(), out StoredDish first);
        inventory.TryAdd(Result(), out StoredDish second);
        Assert.That(first.Id, Is.Not.EqualTo(second.Id));
        Assert.That(first.Result, Is.Not.SameAs(second.Result));
        Assert.That(inventory.Find(second.Id), Is.SameAs(second));
        Assert.That(inventory.Find("missing"), Is.Null);
        Assert.Throws<ArgumentNullException>(() => inventory.TryAdd(null, out _));
    }

    private static ProductionResult Result()
    {
        var run = new ProductionRunData(new ProductionOperationData[0]);
        var dish = new DishRuntimeData("test", "测试菜", 1, new[] { run, run, run });
        var session = new ProductionSession(dish);
        session.Tick(60f);
        return session.Result;
    }
}
