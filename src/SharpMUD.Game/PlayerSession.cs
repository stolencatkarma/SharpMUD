using Arch.Core;
using SharpMUD.Core;

namespace SharpMUD.Game
{
    public enum SessionState
    {
        Connected,
        Authenticating,
        InGame
    }

    public class PlayerSession
    {
        public IConnection Connection { get; }
        public SessionState State { get; set; } = SessionState.Connected;
        public string? Username { get; set; }
        public int? AccountId { get; set; }
        // Entity in the ECS world
        public Entity? Entity { get; set; }

        public PlayerSession(IConnection connection)
        {
            Connection = connection;
        }
    }
}
