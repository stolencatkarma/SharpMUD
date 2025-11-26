using System;
using System.Threading.Tasks;
using Arch.Core;
using SharpMUD.Core.Components;
using SharpMUD.Game;

namespace SharpMUD.Game.Systems
{
    public class MobAISystem
    {
        private readonly World _world;
        private readonly SessionManager _sessionManager;

        public MobAISystem(World world, SessionManager sessionManager)
        {
            _world = world;
            _sessionManager = sessionManager;
        }

        public async Task Update(double deltaTime)
        {
            // Find all aggressive entities that have a weapon and are NOT currently fighting
            var query = new QueryDescription().WithAll<Aggressive, Weapon>().WithNone<CombatState>();
            
            var mobs = new System.Collections.Generic.List<Entity>();
            _world.Query(in query, (Entity entity) => mobs.Add(entity));

            foreach (var mob in mobs)
            {
                await ScanForTargets(mob);
            }
        }

        private async Task ScanForTargets(Entity mob)
        {
            Entity? target = null;

            if (_world.Has<SpacePosition>(mob))
            {
                target = FindSpaceTarget(mob);
            }
            else if (_world.Has<LandPosition>(mob))
            {
                target = FindLandTarget(mob);
            }

            if (target.HasValue)
            {
                // Start Combat
                _world.Add(mob, new CombatState 
                { 
                    Target = target.Value, 
                    NextAttackTime = DateTime.UtcNow 
                });

                var mobName = GetEntityName(mob);
                var targetName = GetEntityName(target.Value);
                
                await SendMessage(target.Value, $"{mobName} screams and attacks you!");
            }
        }

        private Entity? FindSpaceTarget(Entity mob)
        {
            var mobPos = _world.Get<SpacePosition>(mob);
            var weapon = _world.Get<Weapon>(mob);
            
            // Find ships in the same sector within range
            var query = new QueryDescription().WithAll<SpacePosition, Ship>();
            Entity? foundTarget = null;
            double closestDist = double.MaxValue;

            _world.Query(in query, (Entity entity, ref SpacePosition pos, ref Ship ship) => 
            {
                if (entity == mob) return;
                if (pos.SectorId != mobPos.SectorId) return;

                // Check if this ship is controlled by a player
                if (!IsPlayerControlled(entity)) return;

                var dist = Math.Sqrt(Math.Pow(pos.X - mobPos.X, 2) + Math.Pow(pos.Y - mobPos.Y, 2) + Math.Pow(pos.Z - mobPos.Z, 2));
                if (dist <= weapon.Range && dist < closestDist)
                {
                    closestDist = dist;
                    foundTarget = entity;
                }
            });

            return foundTarget;
        }

        private Entity? FindLandTarget(Entity mob)
        {
            var mobPos = _world.Get<LandPosition>(mob);
            var weapon = _world.Get<Weapon>(mob);

            // Find players in the same zone within range
            var query = new QueryDescription().WithAll<LandPosition, Player>();
            Entity? foundTarget = null;
            double closestDist = double.MaxValue;

            _world.Query(in query, (Entity entity, ref LandPosition pos, ref Player player) => 
            {
                if (entity == mob) return;
                if (pos.ZoneId != mobPos.ZoneId) return;

                var dist = Math.Sqrt(Math.Pow(pos.X - mobPos.X, 2) + Math.Pow(pos.Y - mobPos.Y, 2));
                if (dist <= weapon.Range && dist < closestDist)
                {
                    closestDist = dist;
                    foundTarget = entity;
                }
            });

            return foundTarget;
        }

        private bool IsPlayerControlled(Entity target)
        {
            // Check if any player is controlling this entity
            var isControlled = false;
            var query = new QueryDescription().WithAll<Player, Controlling>();
            _world.Query(in query, (Entity entity, ref Controlling controlling) => 
            {
                if (controlling.Target == target) isControlled = true;
            });
            return isControlled;
        }

        private string GetEntityName(Entity entity)
        {
            if (_world.Has<Ship>(entity)) return _world.Get<Ship>(entity).Name;
            if (_world.Has<Player>(entity)) return _world.Get<Player>(entity).Name;
            if (_world.Has<Description>(entity)) return _world.Get<Description>(entity).Short;
            return "Unknown";
        }

        private async Task SendMessage(Entity entity, string message)
        {
            // If entity is a player
            if (_world.Has<Player>(entity))
            {
                var player = _world.Get<Player>(entity);
                if (!string.IsNullOrEmpty(player.ConnectionId))
                {
                    var session = _sessionManager.GetSession(player.ConnectionId);
                    if (session != null) await session.Connection.SendAsync(message);
                }
            }

            // If entity is controlled by a player (e.g. Ship)
            var query = new QueryDescription().WithAll<Player, Controlling>();
            _world.Query(in query, (Entity playerEnt, ref Player player, ref Controlling controlling) => 
            {
                if (controlling.Target == entity && !string.IsNullOrEmpty(player.ConnectionId))
                {
                    var session = _sessionManager.GetSession(player.ConnectionId);
                    if (session != null)
                    {
                        _ = session.Connection.SendAsync(message);
                    }
                }
            });
        }
    }
}
