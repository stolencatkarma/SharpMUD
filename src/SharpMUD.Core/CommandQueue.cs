using System.Collections.Concurrent;

namespace SharpMUD.Core
{
    public class CommandQueue
    {
        private readonly ConcurrentQueue<(IConnection Connection, string Command)> _queue = new();

        public void Enqueue(IConnection connection, string command)
        {
            _queue.Enqueue((connection, command));
        }

        public bool TryDequeue(out (IConnection Connection, string Command) item)
        {
            return _queue.TryDequeue(out item);
        }
    }
}
