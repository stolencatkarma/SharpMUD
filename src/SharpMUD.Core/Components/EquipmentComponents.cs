using Arch.Core;

namespace SharpMUD.Core.Components
{
    public enum EquipmentSlot
    {
        Head,
        Chest,
        Legs,
        Feet,
        MainHand,
        OffHand
    }

    public struct Equippable
    {
        public EquipmentSlot Slot;
        public int ArmorBonus;
    }

    public struct Equipment
    {
        public Entity Head;
        public Entity Chest;
        public Entity Legs;
        public Entity Feet;
        public Entity MainHand;
        public Entity OffHand;
    }

    public struct Equipped
    {
        public Entity Wearer;
        public EquipmentSlot Slot;
    }
}
