using System.Collections.Generic;

namespace SharpMUD.Core.Components
{
    public enum QuestStatus
    {
        NotStarted,
        InProgress,
        Completed, // Ready to turn in
        TurnedIn
    }

    public enum QuestType
    {
        Kill,
        Fetch
    }

    public class QuestDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public QuestType Type { get; set; }
        public string TargetName { get; set; } = string.Empty;
        public int TargetCount { get; set; }
        public int RewardXp { get; set; }
        public int RewardGold { get; set; }
    }

    public class QuestGiver
    {
        public List<string> QuestIds { get; set; } = new List<string>();
    }

    public class QuestLog
    {
        public List<PlayerQuestState> Quests { get; set; } = new List<PlayerQuestState>();
    }

    public class PlayerQuestState
    {
        public string QuestId { get; set; } = string.Empty;
        public QuestStatus Status { get; set; }
        public int Progress { get; set; }
    }
}
