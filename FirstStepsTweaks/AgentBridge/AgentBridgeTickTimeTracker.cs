using System;
using System.Diagnostics;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.AgentBridge;

public sealed class AgentBridgeTickTimeTracker
{
    private const double SampleIntervalMilliseconds = 1000;
    private readonly ICoreServerAPI api;
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private readonly object sync = new();
    private long lastSampleMilliseconds;
    private double averageTickMilliseconds = 50;
    private bool disposed;

    public AgentBridgeTickTimeTracker(ICoreServerAPI api)
    {
        this.api = api;
    }

    public void Start()
    {
        lastSampleMilliseconds = stopwatch.ElapsedMilliseconds;
        ScheduleNextSample();
    }

    public double GetAverageTickMilliseconds()
    {
        lock (sync)
        {
            return Math.Round(averageTickMilliseconds, 2);
        }
    }

    public void Stop()
    {
        disposed = true;
    }

    private void ScheduleNextSample()
    {
        if (disposed)
        {
            return;
        }

        api.Event.Timer(OnSample, SampleIntervalMilliseconds);
    }

    private void OnSample()
    {
        if (disposed)
        {
            return;
        }

        var currentMilliseconds = stopwatch.ElapsedMilliseconds;
        var deltaMilliseconds = currentMilliseconds - lastSampleMilliseconds;
        lastSampleMilliseconds = currentMilliseconds;

        if (deltaMilliseconds > 0)
        {
            lock (sync)
            {
                averageTickMilliseconds = (averageTickMilliseconds * 0.8) + (deltaMilliseconds * 0.2);
            }
        }

        ScheduleNextSample();
    }
}
