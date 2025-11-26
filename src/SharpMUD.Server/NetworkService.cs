using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using SharpMUD.Core;

using SharpMUD.Game;

namespace SharpMUD.Server
{
    public class NetworkService : IHostedService
    {
        private readonly INetworkServer _server;
        private readonly CommandQueue _commandQueue;
        private readonly SessionManager _sessionManager;

        public NetworkService(INetworkServer server, CommandQueue commandQueue, SessionManager sessionManager)
        {
            _server = server;
            _commandQueue = commandQueue;
            _sessionManager = sessionManager;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _server.OnClientConnected += Server_OnClientConnected;
            // Start the server in a background task so it doesn't block StartAsync
            _ = _server.StartAsync(cancellationToken);
            return Task.CompletedTask;
        }

        private void Server_OnClientConnected(object? sender, IConnection connection)
        {
            var session = _sessionManager.CreateSession(connection);
            _ = session.Connection.SendAsync("Welcome to SharpMUD! Type 'login <username>' to start.");

            connection.OnMessageReceived += (s, msg) =>
            {
                _commandQueue.Enqueue(connection, msg);
            };

            connection.OnDisconnected += (s, args) =>
            {
                _sessionManager.RemoveSession(connection.ConnectionId);
            };
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _server.StopAsync();
        }
    }
}
