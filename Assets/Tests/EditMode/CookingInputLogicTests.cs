using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public sealed class CookingInputLogicTests
{
    private static Type LogicType
    {
        get
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try { return assembly.GetTypes(); }
                    catch (ReflectionTypeLoadException exception) { return exception.Types.Where(type => type != null); }
                })
                .FirstOrDefault(type => type.FullName == "Cook.CookingInputLogic");
        }
    }

    private static object CreateLogic(float repeatTimeout = 0.45f, float singleDuration = 0.9f)
    {
        Assert.That(LogicType, Is.Not.Null, "Cook.CookingInputLogic has not been implemented.");
        return Activator.CreateInstance(LogicType, repeatTimeout, singleDuration);
    }

    private static object Mode(string name)
    {
        Type modeType = LogicType.Assembly.GetType("Cook.CookInputMode");
        Assert.That(modeType, Is.Not.Null);
        return Enum.Parse(modeType, name);
    }

    private static string InvokeSignal(object logic, string method, string mode, float time)
    {
        object value = LogicType.GetMethod(method).Invoke(logic, new[] { Mode(mode), (object)time });
        return value.ToString();
    }

    private static string InvokeSignal(object logic, string method, string mode)
    {
        object value = LogicType.GetMethod(method).Invoke(logic, new[] { Mode(mode) });
        return value.ToString();
    }

    [TestCase(1, -1, 3, 0)]
    [TestCase(1, 1, 3, 2)]
    [TestCase(0, -1, 3, 0)]
    [TestCase(2, 1, 3, 2)]
    public void StepStation_ChangesOneSlotAndClampsAtEdges(int current, int direction, int count, int expected)
    {
        object logic = CreateLogic();
        object result = LogicType.GetMethod("StepStation").Invoke(logic, new object[] { current, direction, count });
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void RepeatedPress_StartsOnceAndEachPressOnlyAddsAPulse()
    {
        object logic = CreateLogic();

        Assert.That(InvokeSignal(logic, "Press", "RepeatedPress", 0f), Is.EqualTo("Start, Pulse"));
        Assert.That(InvokeSignal(logic, "Press", "RepeatedPress", 0.2f), Is.EqualTo("Pulse"));
        Assert.That(InvokeSignal(logic, "Tick", "RepeatedPress", 0.5f), Is.EqualTo("None"));
        Assert.That(InvokeSignal(logic, "Tick", "RepeatedPress", 0.7f), Is.EqualTo("Stop"));
    }

    [Test]
    public void Hold_StartsOnPressAndStopsOnRelease()
    {
        object logic = CreateLogic();

        Assert.That(InvokeSignal(logic, "Press", "Hold", 0f), Is.EqualTo("Start"));
        Assert.That(InvokeSignal(logic, "Release", "Hold"), Is.EqualTo("Stop"));
    }

    [Test]
    public void SinglePress_AutomaticallyStopsAfterOneActionDuration()
    {
        object logic = CreateLogic();

        Assert.That(InvokeSignal(logic, "Press", "SinglePress", 0f), Is.EqualTo("Start, Pulse"));
        Assert.That(InvokeSignal(logic, "Tick", "SinglePress", 0.5f), Is.EqualTo("None"));
        Assert.That(InvokeSignal(logic, "Tick", "SinglePress", 1f), Is.EqualTo("Stop"));
    }

    [Test]
    public void Cancel_StopsAnActiveActionOnlyOnce()
    {
        object logic = CreateLogic();
        InvokeSignal(logic, "Press", "RepeatedPress", 0f);

        Assert.That(LogicType.GetMethod("Cancel").Invoke(logic, null).ToString(), Is.EqualTo("Stop"));
        Assert.That(LogicType.GetMethod("Cancel").Invoke(logic, null).ToString(), Is.EqualTo("None"));
    }
}
