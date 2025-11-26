using System.Collections.Generic;

namespace SharpMUD.Game
{
    public class WorldConfig
    {
        public List<SectorConfig>? Sectors { get; set; }
        public List<ZoneConfig>? Zones { get; set; }
        public List<QuestConfig>? Quests { get; set; }
        public List<SkillConfig>? Skills { get; set; }
    }

    public class SkillConfig
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public int ManaCost { get; set; }
        public int Cooldown { get; set; }
        public int Value { get; set; }
        public int Range { get; set; }
    }

    public class QuestConfig
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; } // "Kill" or "Fetch"
        public string? TargetName { get; set; }
        public int TargetCount { get; set; }
        public int RewardXp { get; set; }
        public int RewardGold { get; set; }
    }

    public class SectorConfig
    {
        public string? Id { get; set; }
        public List<PlanetConfig>? Planets { get; set; }
        public List<SpaceMobConfig>? Mobs { get; set; }
    }

    public class PlanetConfig
    {
        public string? Name { get; set; }
        public string? ZoneId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public string? Description { get; set; }
    }

    public class SpaceMobConfig
    {
        public string? Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int Hull { get; set; }
        public int Shields { get; set; }
        public WeaponConfig? Weapon { get; set; }
        public bool Aggressive { get; set; }
        public string? Description { get; set; }
    }

    public class ZoneConfig
    {
        public string? Id { get; set; }
        public List<RoomConfig>? Rooms { get; set; }
    }

    public class RoomConfig
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string? Description { get; set; }
        public string? LongDescription { get; set; }
        public bool Shopkeeper { get; set; }
        public List<LandMobConfig>? Mobs { get; set; }
        public List<ItemConfig>? Items { get; set; }
    }

    public class LandMobConfig
    {
        public string? Name { get; set; }
        public int Health { get; set; }
        public WeaponConfig? Weapon { get; set; }
        public bool Aggressive { get; set; }
        public string? Description { get; set; }
        public List<ItemConfig>? Drops { get; set; }
        public List<string>? Quests { get; set; }
    }

    public class ItemConfig
    {
        public string? Name { get; set; }
        public int Value { get; set; }
        public float Weight { get; set; }
        public WeaponConfig? Weapon { get; set; }
        public EquippableConfig? Equippable { get; set; }
        public string? Description { get; set; }
    }

    public class EquippableConfig
    {
        public string? Slot { get; set; }
        public int ArmorBonus { get; set; }
    }

    public class WeaponConfig
    {
        public string? Name { get; set; }
        public int Damage { get; set; }
        public int Range { get; set; }
        public int Cooldown { get; set; }
    }
}
