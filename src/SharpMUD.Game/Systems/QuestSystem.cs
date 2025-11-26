using System;
using System.Linq;
using System.Threading.Tasks;
using Arch.Core;
using SharpMUD.Core.Components;

namespace SharpMUD.Game.Systems
{
    public class QuestSystem
    {
        private readonly WorldManager _worldManager;
        private readonly SessionManager _sessionManager;

        public QuestSystem(WorldManager worldManager, SessionManager sessionManager)
        {
            _worldManager = worldManager;
            _sessionManager = sessionManager;
        }

        public void OnMobKilled(Entity killer, string mobName)
        {
            if (!_worldManager.World.IsAlive(killer)) return;
            
            // Check if killer is a player or controlled by a player
            Entity playerEntity = killer;
            if (_worldManager.World.Has<Controlling>(killer))
            {
                // If a ship kills something, the player controlling it gets credit
                // But wait, Controlling is on the Player, pointing to the Ship.
                // So if 'killer' is a Ship, we need to find who is controlling it.
                // This is reverse lookup, which is slow in ECS without an index.
                // However, usually the Player entity is the one with the QuestLog.
                // If the player is controlling a ship, the player entity is separate.
                // We need to find the player entity associated with this killer.
                
                // Actually, let's assume 'killer' passed in is the entity that dealt the blow.
                // If it's a ship, we need to find the pilot.
                // Since we don't have a "ControlledBy" component, we might have to iterate players.
                // Optimization: Add ControlledBy to the ship? Or just iterate sessions.
                
                foreach (var session in _sessionManager.GetAllSessions())
                {
                    if (session.Entity.HasValue)
                    {
                        if (_worldManager.World.Has<Controlling>(session.Entity.Value))
                        {
                            if (_worldManager.World.Get<Controlling>(session.Entity.Value).Target == killer)
                            {
                                playerEntity = session.Entity.Value;
                                break;
                            }
                        }
                    }
                }
            }

            if (_worldManager.World.Has<QuestLog>(playerEntity))
            {
                var questLog = _worldManager.World.Get<QuestLog>(playerEntity);
                bool updated = false;

                foreach (var questState in questLog.Quests)
                {
                    if (questState.Status == QuestStatus.InProgress)
                    {
                        if (_worldManager.QuestRegistry.TryGetValue(questState.QuestId, out var questDef))
                        {
                            if (questDef.Type == QuestType.Kill && 
                                questDef.TargetName.Equals(mobName, StringComparison.OrdinalIgnoreCase))
                            {
                                questState.Progress++;
                                if (questState.Progress >= questDef.TargetCount)
                                {
                                    questState.Status = QuestStatus.Completed;
                                    // Notify player? We need the session.
                                    NotifyPlayer(playerEntity, $"Quest Complete: {questDef.Title}!");
                                }
                                else
                                {
                                    NotifyPlayer(playerEntity, $"Quest Update: {questDef.Title} ({questState.Progress}/{questDef.TargetCount})");
                                }
                                updated = true;
                            }
                        }
                    }
                }

                if (updated)
                {
                    // QuestLog is a class, so we don't strictly need to Set() it back for the changes to stick if it's a reference type component.
                    // But if it were a struct we would. Arch supports classes.
                    // However, it's good practice to mark it as changed if the ECS supports change tracking.
                    // _worldManager.World.Set(playerEntity, questLog); 
                }
            }
        }

        private async void NotifyPlayer(Entity playerEntity, string message)
        {
            // Find session for this player entity
            foreach (var session in _sessionManager.GetAllSessions())
            {
                if (session.Entity == playerEntity)
                {
                    await session.Connection.SendAsync(message);
                    break;
                }
            }
        }
    }
}
