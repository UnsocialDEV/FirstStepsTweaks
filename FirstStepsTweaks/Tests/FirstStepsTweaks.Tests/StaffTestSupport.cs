using System.Collections.Generic;
using System.Reflection;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Tests;

internal static class StaffTestCoreServerApiFactory
{
    public static ICoreServerAPI Create(params IServerPlayer[] onlinePlayers)
    {
        var saveGame = DispatchProxy.Create<ISaveGame, SaveGameProxy>();
        var worldManager = DispatchProxy.Create<IWorldManagerAPI, WorldManagerProxy>();
        var world = DispatchProxy.Create<IServerWorldAccessor, ServerWorldProxy>();
        var logger = DispatchProxy.Create<ILogger, LoggerProxy>();
        var api = DispatchProxy.Create<ICoreServerAPI, CoreServerApiProxy>();

        ((WorldManagerProxy)(object)worldManager).SaveGame = saveGame;
        ((ServerWorldProxy)(object)world).AllOnlinePlayers = onlinePlayers ?? [];
        ((CoreServerApiProxy)(object)api).WorldManager = worldManager;
        ((CoreServerApiProxy)(object)api).World = world;
        ((CoreServerApiProxy)(object)api).Logger = logger;

        return api;
    }
}

internal static class StaffTestServerPlayerFactory
{
    public static IServerPlayer Create(string playerUid, string playerName, IEnumerable<string>? privileges = null, double ping = 0.05)
    {
        var player = DispatchProxy.Create<IServerPlayer, TestServerPlayerProxy>();
        var proxy = (TestServerPlayerProxy)(object)player;
        proxy.PlayerUid = playerUid;
        proxy.PlayerName = playerName;
        proxy.Ping = ping;

        foreach (string privilege in privileges ?? [])
        {
            proxy.Privileges.Add(privilege);
        }

        return player;
    }
}

internal class TestServerPlayerProxy : DispatchProxy
{
    public string PlayerUid { get; set; } = string.Empty;

    public string PlayerName { get; set; } = string.Empty;

    public double Ping { get; set; }

    public HashSet<string> Privileges { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> SentMessages { get; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
        {
            return null;
        }

        return targetMethod.Name switch
        {
            "get_PlayerUID" => PlayerUid,
            "get_PlayerName" => PlayerName,
            "get_Ping" => Ping,
            "HasPrivilege" => args?.Length > 0 && args[0] is string privilege && Privileges.Contains(privilege),
            "SendMessage" => RecordMessage(args),
            _ => targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null
        };
    }

    private object? RecordMessage(object?[]? args)
    {
        if (args?.Length > 1)
        {
            SentMessages.Add(args[1]?.ToString() ?? string.Empty);
        }

        return null;
    }
}

internal sealed class RecordingPrivilegeMutator : IPlayerPrivilegeMutator
{
    public List<string> Granted { get; } = [];

    public List<string> Revoked { get; } = [];

    public void Grant(IServerPlayer player, string privilege)
    {
        Granted.Add(privilege);
        ((TestServerPlayerProxy)(object)player).Privileges.Add(privilege);
    }

    public void Revoke(IServerPlayer player, string privilege)
    {
        Revoked.Add(privilege);
        ((TestServerPlayerProxy)(object)player).Privileges.Remove(privilege);
    }
}

internal class SaveGameProxy : DispatchProxy
{
    private readonly Dictionary<string, object?> values = new(StringComparer.Ordinal);

    public void StoreDataDirect(string key, object? value)
    {
        values[key] = value;
    }

    public object? GetStoredValue(string key)
    {
        values.TryGetValue(key, out object? value);
        return value;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
        {
            return null;
        }

        return targetMethod.Name switch
        {
            "StoreData" => Store(args),
            "GetData" => Get(targetMethod, args),
            _ => targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null
        };
    }

    private object? Store(object?[]? args)
    {
        values[(string)args![0]!] = args[1];
        return null;
    }

    private object? Get(MethodInfo targetMethod, object?[]? args)
    {
        if (values.TryGetValue((string)args![0]!, out object? value))
        {
            return value;
        }

        return targetMethod.ReturnType.IsValueType
            ? Activator.CreateInstance(targetMethod.ReturnType)
            : null;
    }
}

internal class WorldManagerProxy : DispatchProxy
{
    public ISaveGame? SaveGame { get; set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == "get_SaveGame")
        {
            return SaveGame;
        }

        return targetMethod?.ReturnType.IsValueType == true
            ? Activator.CreateInstance(targetMethod.ReturnType)
            : null;
    }
}

internal class ServerWorldProxy : DispatchProxy
{
    public IServerPlayer[] AllOnlinePlayers { get; set; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == "get_AllOnlinePlayers")
        {
            return AllOnlinePlayers;
        }

        return targetMethod?.ReturnType.IsValueType == true
            ? Activator.CreateInstance(targetMethod.ReturnType)
            : null;
    }
}

internal class CoreServerApiProxy : DispatchProxy
{
    public IWorldManagerAPI? WorldManager { get; set; }

    public IServerWorldAccessor? World { get; set; }

    public ILogger? Logger { get; set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        return targetMethod?.Name switch
        {
            "get_WorldManager" => WorldManager,
            "get_World" => World,
            "get_Logger" => Logger,
            _ => targetMethod?.ReturnType.IsValueType == true ? Activator.CreateInstance(targetMethod.ReturnType) : null
        };
    }
}

internal class LoggerProxy : DispatchProxy
{
    public List<string> Notifications { get; } = [];

    public List<string> Warnings { get; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.ReturnType == typeof(void))
        {
            if (targetMethod.Name.StartsWith("Notification", StringComparison.Ordinal) && args?.Length > 0)
            {
                Notifications.Add(args[0]?.ToString() ?? string.Empty);
            }

            if (targetMethod.Name.StartsWith("Warning", StringComparison.Ordinal) && args?.Length > 0)
            {
                Warnings.Add(args[0]?.ToString() ?? string.Empty);
            }

            return null;
        }

        if (targetMethod?.Name.StartsWith("Notification", StringComparison.Ordinal) == true && args?.Length > 0)
        {
            Notifications.Add(args[0]?.ToString() ?? string.Empty);
        }

        if (targetMethod?.Name.StartsWith("Warning", StringComparison.Ordinal) == true && args?.Length > 0)
        {
            Warnings.Add(args[0]?.ToString() ?? string.Empty);
        }

        return targetMethod?.ReturnType.IsValueType == true
            ? Activator.CreateInstance(targetMethod.ReturnType)
            : null;
    }
}
