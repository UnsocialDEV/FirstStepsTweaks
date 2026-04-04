using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeEndpointResolver
{
    public bool TryResolve(string host, int port, out IPEndPoint? endpoint, out string message)
    {
        endpoint = null;

        if (string.IsNullOrWhiteSpace(host))
        {
            message = "AgentBridge.Host must be configured.";
            return false;
        }

        if (port <= 0 || port > 65535)
        {
            message = "AgentBridge.Port must be between 1 and 65535.";
            return false;
        }

        string normalizedHost = NormalizeHost(host);
        if (IPAddress.TryParse(normalizedHost, out IPAddress? address))
        {
            endpoint = new IPEndPoint(address, port);
            message = string.Empty;
            return true;
        }

        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(normalizedHost);
            address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetworkV6);
        }
        catch (Exception exception)
        {
            message = $"AgentBridge.Host '{host}' could not be resolved: {exception.Message}";
            return false;
        }

        if (address == null)
        {
            message = $"AgentBridge.Host '{host}' did not resolve to a supported IP address.";
            return false;
        }

        endpoint = new IPEndPoint(address, port);
        message = string.Empty;
        return true;
    }

    private static string NormalizeHost(string host)
    {
        string trimmed = host.Trim();
        if (trimmed.Length > 1 && trimmed[0] == '[' && trimmed[^1] == ']')
        {
            return trimmed.Substring(1, trimmed.Length - 2);
        }

        return string.Equals(trimmed, "localhost", StringComparison.OrdinalIgnoreCase)
            ? IPAddress.Loopback.ToString()
            : trimmed;
    }
}
