namespace SharpMUD.Core.Components
{
    public struct LandPosition
    {
        public int X;
        public int Y;
        public string ZoneId; // Or PlanetId
    }

    public struct Player
    {
        public string Name;
        public string ConnectionId;
    }

    public struct Health
    {
        public int Current;
        public int Max;
    }
}
