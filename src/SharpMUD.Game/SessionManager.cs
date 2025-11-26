using System.Collections.Concurrent;
using SharpMUD.Core;

namespace SharpMUD.Game
{
    public class SessionManager
    {
        private readonly ConcurrentDictionary<string, PlayerSession> _sessions = new();

        public PlayerSession CreateSession(IConnection connection)
        {
            var session = new PlayerSession(connection);
            _sessions.TryAdd(connection.ConnectionId, session);
            return session;
        }

        public PlayerSession? GetSession(string connectionId)
        {
            _sessions.TryGetValue(connectionId, out var session);
            return session;
        }

        public System.Collections.Generic.IEnumerable<PlayerSession> GetAllSessions()
        {
            return _sessions.Values;
        }

        public void RemoveSession(string connectionId)
        {
            _sessions.TryRemove(connectionId, out _);
        }
    }
}
