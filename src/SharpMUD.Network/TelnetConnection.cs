using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpMUD.Core;

namespace SharpMUD.Network
{
    public class TelnetConnection : IConnection
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly CancellationTokenSource _cts;

        public string ConnectionId { get; }
        public bool IsConnected => _client.Connected;

        public event EventHandler<string>? OnMessageReceived;
        public event EventHandler? OnDisconnected;

        public TelnetConnection(TcpClient client)
        {
            _client = client;
            ConnectionId = Guid.NewGuid().ToString();
            _stream = client.GetStream();
            _reader = new StreamReader(_stream, Encoding.ASCII);
            _writer = new StreamWriter(_stream, Encoding.ASCII) { AutoFlush = true };
            _cts = new CancellationTokenSource();

            Task.Run(ReadLoopAsync);
        }

        private async Task ReadLoopAsync()
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested && _client.Connected)
                {
                    var line = await _reader.ReadLineAsync();
                    if (line == null) break; // Client disconnected

                    OnMessageReceived?.Invoke(this, line);
                }
            }
            catch (Exception)
            {
                // Handle disconnection or errors
            }
            finally
            {
                await DisconnectAsync();
            }
        }

        public async Task SendAsync(string message)
        {
            if (!IsConnected) return;
            try
            {
                await _writer.WriteLineAsync(message);
            }
            catch
            {
                await DisconnectAsync();
            }
        }

        public async Task DisconnectAsync()
        {
            if (_cts.IsCancellationRequested) return;
            
            _cts.Cancel();
            _client.Close();
            OnDisconnected?.Invoke(this, EventArgs.Empty);
            await Task.CompletedTask;
        }
    }
}
