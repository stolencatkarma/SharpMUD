using Arch.Core;

namespace SharpMUD.Core.Components
{
    public struct Description
    {
        public string Short;
        public string Long;
    }

    // Component on a Player entity indicating they are controlling a specific entity (like a ship)
    public struct Controlling
    {
        public Entity Target;
    }

    public struct Weapon
    {
        public string Name;
        public int Damage;
        public int Range;
        public int CooldownMs;
        public System.DateTime LastFired;
    }

    public struct CombatState
    {
        public Entity Target;
        public System.DateTime NextAttackTime;
    }

    public struct Aggressive { }

    public struct Container
    {
        public int Capacity;
    }

    public struct ContainedBy
    {
        public Entity Container;
    }

    public struct Item
    {
        public int Value;
        public float Weight;
    }

    public struct Corpse { }

    public struct DbId
    {
        public int Id;
    }

    public struct Shopkeeper { }

    public struct Experience
    {
        public int Value;
        public int Level;
    }

    public struct Money
    {
        public int Amount;
    }
}
