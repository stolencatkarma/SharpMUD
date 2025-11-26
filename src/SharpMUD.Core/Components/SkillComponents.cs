using System.Collections.Generic;

namespace SharpMUD.Core.Components
{
    public struct Mana
    {
        public int Current;
        public int Max;
    }

    public class KnownSkills
    {
        public List<string> SkillIds { get; set; } = new List<string>();
    }

    public class SkillCooldowns
    {
        public Dictionary<string, System.DateTime> Cooldowns { get; set; } = new Dictionary<string, System.DateTime>();
    }

    public enum SkillType
    {
        Damage,
        Heal,
        Buff
    }

    public class SkillDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SkillType Type { get; set; }
        public int ManaCost { get; set; }
        public int CooldownMs { get; set; }
        public int Value { get; set; } // Damage or Heal amount
        public int Range { get; set; }
    }
}
