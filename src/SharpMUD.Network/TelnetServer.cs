using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpMUD.Core;

namespace SharpMUD.Network
{
    public class TelnetServer : INetworkServer
    {
        private readonly TcpListener _listener;
        private readonly ILogger<TelnetServer> _logger;
        private CancellationTokenSource? _cts;

        public event EventHandler<IConnection>? OnClientConnected;

        public TelnetServer(ILogger<TelnetServer> logger, int port = 23)
        {
            _logger = logger;
            _listener = new TcpListener(IPAddress.Any, port);
        }

        public async Task StartAsync(CancellationToken token)
        {
            _listener.Start();
            _logger.LogInformation("Telnet Server started on port {Port}", ((IPEndPoint)_listener.LocalEndpoint).Port);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    _logger.LogInformation("New client connected: {RemoteEndPoint}", client.Client.RemoteEndPoint);
                    var connection = new TelnetConnection(client);
                    OnClientConnected?.Invoke(this, connection);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Telnet Server accept loop");
            }
        }

        public Task StopAsync()
        {
            _cts?.Cancel();
            _listener.Stop();
            return Task.CompletedTask;
        }
    }
}
