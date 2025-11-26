namespace SharpMUD.Core.Components
{
    public struct SpacePosition
    {
        public double X;
        public double Y;
        public double Z;
        public string SectorId;
    }

    public struct Ship
    {
        public string Name;
        public double Hull;
        public double MaxHull;
        public double Shields;
        public double MaxShields;
    }

    public struct Planet
    {
        public string Name;
        public string ZoneId; // The ID used for LandPosition.ZoneId
    }
}
