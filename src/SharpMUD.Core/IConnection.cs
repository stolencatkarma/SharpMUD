using System;
using System.Threading.Tasks;

namespace SharpMUD.Core
{
    public interface IConnection
    {
        string ConnectionId { get; }
        bool IsConnected { get; }
        Task SendAsync(string message);
        Task DisconnectAsync();
        event EventHandler<string> OnMessageReceived;
        event EventHandler OnDisconnected;
    }
}
