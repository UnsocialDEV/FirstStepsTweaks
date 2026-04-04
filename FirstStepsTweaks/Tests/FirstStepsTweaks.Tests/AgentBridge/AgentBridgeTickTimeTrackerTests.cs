using System;
using System.Collections.Generic;
using System.Reflection;
using FirstStepsTweaks.AgentBridge;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests.AgentBridge;

public sealed class AgentBridgeTickTimeTrackerTests
{
    [Fact]
    public void Start_SchedulesFirstSampleAtOneSecond()
    {
        var (api, eventApi) = CreateApi();
        var tracker = new AgentBridgeTickTimeTracker(api);

        tracker.Start();

        Assert.Equal(new[] { 1000d }, eventApi.TimerDelays);
    }

    [Fact]
    public void Sample_ReschedulesUsingSameOneSecondInterval()
    {
        var (api, eventApi) = CreateApi();
        var tracker = new AgentBridgeTickTimeTracker(api);

        tracker.Start();
        eventApi.InvokeLastTimer();

        Assert.Equal(new[] { 1000d, 1000d }, eventApi.TimerDelays);
    }

    private static (ICoreServerAPI Api, TestServerEventApiProxy EventApi) CreateApi()
    {
        var eventApi = DispatchProxy.Create<IServerEventAPI, TestServerEventApiProxy>();
        var eventProxy = (TestServerEventApiProxy)(object)eventApi;
        var api = DispatchProxy.Create<ICoreServerAPI, TestCoreServerApiProxy>();
        var apiProxy = (TestCoreServerApiProxy)(object)api;
        apiProxy.Event = eventApi;
        return (api, eventProxy);
    }

    private class TestCoreServerApiProxy : DispatchProxy
    {
        public IServerEventAPI? Event { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_Event")
            {
                return Event;
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }

    private class TestServerEventApiProxy : DispatchProxy
    {
        private Action? lastTimerAction;

        public List<double> TimerDelays { get; } = new();

        public void InvokeLastTimer()
        {
            lastTimerAction?.Invoke();
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Timer")
            {
                lastTimerAction = (Action)args![0]!;
                TimerDelays.Add(Convert.ToDouble(args[1]));
                return null;
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
