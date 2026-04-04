using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FirstStepsTweaks.AgentBridge;

#nullable enable
public sealed class AgentBridgeSubscriber
{
    private readonly StreamWriter writer;
    private readonly SemaphoreSlim gate = new(1, 1);

    public AgentBridgeSubscriber(Stream stream)
    {
        writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true)
        {
            AutoFlush = true
        };
    }

    public async Task SendAsync(string payload, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);

        try
        {
            await writer.WriteLineAsync(payload);
        }
        finally
        {
            gate.Release();
        }
    }
}
