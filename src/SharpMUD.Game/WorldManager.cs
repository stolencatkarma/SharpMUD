using Arch.Core;
using System.Collections.Generic;
using SharpMUD.Core.Components;

namespace SharpMUD.Game
{
    public class WorldManager
    {
        public World World { get; }
        public Dictionary<string, QuestDefinition> QuestRegistry { get; } = new Dictionary<string, QuestDefinition>();
        public Dictionary<string, SkillDefinition> SkillRegistry { get; } = new Dictionary<string, SkillDefinition>();

        public WorldManager()
        {
            World = World.Create();
        }
    }
}
