using System;
using System.Collections.Generic;
using Cook.Managers;
using NUnit.Framework;

public sealed class EventManagerTests
{
    private readonly struct SampleEvent
    {
        public SampleEvent(int value) { Value = value; }
        public int Value { get; }
    }

    [Test]
    public void Publish_DeliversPayloadInSubscriptionOrderAndIsolatesTypes()
    {
        var events = new EventManager();
        var values = new List<int>();
        events.Subscribe<SampleEvent>(message => values.Add(message.Value));
        events.Subscribe<SampleEvent>(message => values.Add(message.Value + 1));
        events.Subscribe<string>(_ => Assert.Fail("Other event type received the message"));

        events.Publish(new SampleEvent(10));
        Assert.That(values, Is.EqualTo(new[] { 10, 11 }));
    }

    [Test]
    public void Unsubscribe_RemovesOnlyTheSpecifiedHandler()
    {
        var events = new EventManager();
        var values = new List<int>();
        Action<SampleEvent> removed = message => values.Add(1);
        events.Subscribe(removed);
        events.Subscribe<SampleEvent>(message => values.Add(2));
        events.Unsubscribe(removed);
        events.Unsubscribe(removed);

        events.Publish(new SampleEvent(0));
        Assert.That(values, Is.EqualTo(new[] { 2 }));
    }

    [Test]
    public void NoSubscribers_AndUnsubscribingLastHandler_AreSafe()
    {
        var events = new EventManager();
        Action<SampleEvent> handler = _ => Assert.Fail("Removed handler was called");
        events.Publish(new SampleEvent(0));
        events.Unsubscribe(handler);
        events.Subscribe(handler);
        events.Unsubscribe(handler);
        Assert.DoesNotThrow(() => events.Publish(new SampleEvent(0)));
    }

    [Test]
    public void SubscriptionChangesDuringPublish_ApplyToNextPublish()
    {
        var events = new EventManager();
        var values = new List<int>();
        Action<SampleEvent> first = null;
        Action<SampleEvent> second = _ => values.Add(2);
        Action<SampleEvent> added = _ => values.Add(3);
        first = _ =>
        {
            values.Add(1);
            events.Unsubscribe(first);
            events.Unsubscribe(second);
            events.Subscribe(added);
        };
        events.Subscribe(first);
        events.Subscribe(second);

        events.Publish(new SampleEvent(0));
        Assert.That(values, Is.EqualTo(new[] { 1, 2 }));
        values.Clear();
        events.Publish(new SampleEvent(0));
        Assert.That(values, Is.EqualTo(new[] { 3 }));
    }

    [Test]
    public void Clear_RemovesAllTypesAndAllowsNewSubscriptions()
    {
        var events = new EventManager();
        events.Subscribe<int>(_ => Assert.Fail("Cleared int handler was called"));
        events.Subscribe<string>(_ => Assert.Fail("Cleared string handler was called"));
        events.Clear();
        events.Publish(1);
        events.Publish("test");
        int calls = 0;
        events.Subscribe<int>(_ => calls++);
        events.Publish(1);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void NullHandlers_AreRejected()
    {
        var events = new EventManager();
        Assert.Throws<ArgumentNullException>(() => events.Subscribe<int>(null));
        Assert.Throws<ArgumentNullException>(() => events.Unsubscribe<int>(null));
    }
}
