using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharpMUD.Core
{
    public interface INetworkServer
    {
        Task StartAsync(CancellationToken token);
        Task StopAsync();
        event EventHandler<IConnection> OnClientConnected;
    }
}
