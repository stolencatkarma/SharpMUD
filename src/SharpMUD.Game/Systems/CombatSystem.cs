using System;
using System.Threading.Tasks;
using Arch.Core;
using SharpMUD.Core.Components;
using SharpMUD.Game;

namespace SharpMUD.Game.Systems
{
    public class CombatSystem
    {
        private readonly World _world;
        private readonly SessionManager _sessionManager;
        private readonly QuestSystem _questSystem;

        public CombatSystem(World world, SessionManager sessionManager, QuestSystem questSystem)
        {
            _world = world;
            _sessionManager = sessionManager;
            _questSystem = questSystem;
        }

        public async Task Update(double deltaTime)
        {
            var query = new QueryDescription().WithAll<CombatState>();
            
            var attackers = new System.Collections.Generic.List<Entity>();
            _world.Query(in query, (Entity entity) => attackers.Add(entity));

            foreach (var attacker in attackers)
            {
                await ProcessAttack(attacker);
            }
        }

        private async Task ProcessAttack(Entity attacker)
        {
            if (!_world.IsAlive(attacker)) return;
            if (!_world.Has<CombatState>(attacker)) return;

            Weapon weapon;
            Entity weaponEntity = Entity.Null;

            if (_world.Has<Weapon>(attacker))
            {
                weapon = _world.Get<Weapon>(attacker);
                weaponEntity = attacker;
            }
            else if (_world.Has<Equipment>(attacker))
            {
                var equipment = _world.Get<Equipment>(attacker);
                if (equipment.MainHand != Entity.Null && _world.IsAlive(equipment.MainHand) && _world.Has<Weapon>(equipment.MainHand))
                {
                    weaponEntity = equipment.MainHand;
                    weapon = _world.Get<Weapon>(weaponEntity);
                }
                else
                {
                    // Unarmed
                    weapon = new Weapon { Name = "Fists", Damage = 1, Range = 1, CooldownMs = 1000 };
                }
            }
            else
            {
                // Unarmed default
                weapon = new Weapon { Name = "Fists", Damage = 1, Range = 1, CooldownMs = 1000 };
            }

            var combatState = _world.Get<CombatState>(attacker);

            if (DateTime.UtcNow < combatState.NextAttackTime) return;

            if (!_world.IsAlive(combatState.Target))
            {
                _world.Remove<CombatState>(attacker);
                await SendMessage(attacker, "Target is gone.");
                return;
            }

            // Check Range
            if (!IsInRange(attacker, combatState.Target, weapon.Range))
            {
                 _world.Remove<CombatState>(attacker);
                 await SendMessage(attacker, "Target is out of range. Combat ended.");
                 return;
            }

            // Apply Damage
            await ApplyDamage(attacker, combatState.Target, weapon);

            // Check if combat state still exists (it might have been removed if target died)
            if (_world.Has<CombatState>(attacker))
            {
                // Update Cooldown
                combatState.NextAttackTime = DateTime.UtcNow.AddMilliseconds(weapon.CooldownMs);
                _world.Set(attacker, combatState);
                
                // Update Weapon LastFired if it's a real entity
                if (weaponEntity != Entity.Null)
                {
                    weapon.LastFired = DateTime.UtcNow;
                    _world.Set(weaponEntity, weapon);
                }
            }
        }

        private bool IsInRange(Entity attacker, Entity target, int range)
        {
            if (_world.Has<SpacePosition>(attacker) && _world.Has<SpacePosition>(target))
            {
                var p1 = _world.Get<SpacePosition>(attacker);
                var p2 = _world.Get<SpacePosition>(target);
                if (p1.SectorId != p2.SectorId) return false;
                var dist = Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2) + Math.Pow(p1.Z - p2.Z, 2));
                return dist <= range;
            }
            
            if (_world.Has<LandPosition>(attacker) && _world.Has<LandPosition>(target))
            {
                var p1 = _world.Get<LandPosition>(attacker);
                var p2 = _world.Get<LandPosition>(target);
                if (p1.ZoneId != p2.ZoneId) return false;
                var dist = Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
                return dist <= range;
            }

            return false;
        }

        private async Task ApplyDamage(Entity attacker, Entity target, Weapon weapon)
        {
            if (_world.Has<Ship>(target))
            {
                var targetShip = _world.Get<Ship>(target);
                var damage = weapon.Damage;

                if (targetShip.Shields > 0)
                {
                    if (targetShip.Shields >= damage)
                    {
                        targetShip.Shields -= damage;
                        damage = 0;
                    }
                    else
                    {
                        damage -= (int)targetShip.Shields;
                        targetShip.Shields = 0;
                    }
                }

                targetShip.Hull -= damage;
                _world.Set(target, targetShip);

                await SendMessage(attacker, $"You fired {weapon.Name} at {targetShip.Name}!");
                await SendMessage(target, $"{GetEntityName(attacker)} fired {weapon.Name} at you!");
                
                if (targetShip.Hull <= 0)
                {
                    await HandleDeath(target, attacker);
                }
                else
                {
                    await SendMessage(attacker, $"{targetShip.Name} Status - Shields: {targetShip.Shields}, Hull: {targetShip.Hull}");
                    await CheckRetaliation(attacker, target);
                }
            }
            else if (_world.Has<Health>(target))
            {
                var health = _world.Get<Health>(target);
                health.Current -= weapon.Damage;
                _world.Set(target, health);
                
                await SendMessage(attacker, $"You hit {GetEntityName(target)} for {weapon.Damage} damage!");
                await SendMessage(target, $"{GetEntityName(attacker)} hit you for {weapon.Damage} damage!");

                if (health.Current <= 0)
                {
                    await HandleDeath(target, attacker);
                }
                else
                {
                    await SendMessage(attacker, $"{GetEntityName(target)} Health: {health.Current}/{health.Max}");
                    await CheckRetaliation(attacker, target);
                }
            }
        }

        private async Task HandleDeath(Entity victim, Entity killer)
        {
            var victimName = GetEntityName(victim);
            await SendMessage(killer, $"You have defeated {victimName}!");
            await SendMessage(victim, "You have died!");

            // Award XP
            if (_world.Has<Experience>(killer))
            {
                var xp = _world.Get<Experience>(killer);
                int xpGain = 100; // Base XP
                
                // Bonus for higher level targets?
                if (_world.Has<Experience>(victim))
                {
                    var victimXp = _world.Get<Experience>(victim);
                    xpGain += victimXp.Level * 50;
                }

                xp.Value += xpGain;
                
                // Level Up Check
                int xpForNextLevel = xp.Level * 1000;
                if (xp.Value >= xpForNextLevel)
                {
                    xp.Level++;
                    await SendMessage(killer, $"*** LEVEL UP! You are now level {xp.Level}! ***");
                    
                    // Increase Stats
                    if (_world.Has<Health>(killer))
                    {
                        var health = _world.Get<Health>(killer);
                        health.Max += 10;
                        health.Current = health.Max;
                        _world.Set(killer, health);
                    }
                }
                else
                {
                    await SendMessage(killer, $"You gain {xpGain} XP.");
                }

                _world.Set(killer, xp);
            }

            bool isPlayer = _world.Has<Player>(victim);
            if (!isPlayer)
            {
                 var query = new QueryDescription().WithAll<Player, Controlling>();
                 _world.Query(in query, (Entity p, ref Controlling c) => 
                 {
                     if (c.Target == victim) isPlayer = true;
                 });
            }

            if (isPlayer)
            {
                await SendMessage(victim, "Respawning at safe location...");
                Respawn(victim);
            }
            else
            {
                // Notify Quest System
                _questSystem.OnMobKilled(killer, GetEntityName(victim));

                SpawnCorpse(victim);
                _world.Destroy(victim);
            }

            if (_world.Has<CombatState>(killer))
            {
                _world.Remove<CombatState>(killer);
            }
        }

        private void SpawnCorpse(Entity victim)
        {
            var name = GetEntityName(victim);
            var corpse = _world.Create(
                new Description { Short = $"Corpse of {name}", Long = $"The dead body of {name} lies here." },
                new Container { Capacity = 10 },
                new Corpse()
            );

            if (_world.Has<SpacePosition>(victim))
                _world.Add(corpse, _world.Get<SpacePosition>(victim));
            
            if (_world.Has<LandPosition>(victim))
                _world.Add(corpse, _world.Get<LandPosition>(victim));

            // Add some loot
            var loot = _world.Create(
                new Description { Short = "Credits", Long = "A small pile of credits." },
                new Item { Value = 100, Weight = 0 },
                new ContainedBy { Container = corpse }
            );
        }

        private void Respawn(Entity entity)
        {
            if (_world.Has<Ship>(entity))
            {
                var ship = _world.Get<Ship>(entity);
                ship.Hull = ship.MaxHull;
                ship.Shields = ship.MaxShields;
                _world.Set(entity, ship);
            }
            
            if (_world.Has<Health>(entity))
            {
                var health = _world.Get<Health>(entity);
                health.Current = health.Max;
                _world.Set(entity, health);
            }

            if (_world.Has<SpacePosition>(entity))
            {
                _world.Set(entity, new SpacePosition { X = 0, Y = 0, Z = 0, SectorId = "Alpha" });
            }
            
            if (_world.Has<LandPosition>(entity))
            {
                var pos = _world.Get<LandPosition>(entity);
                pos.X = 0;
                pos.Y = 0;
                _world.Set(entity, pos);
            }

            if (_world.Has<CombatState>(entity))
            {
                _world.Remove<CombatState>(entity);
            }
        }

        private async Task CheckRetaliation(Entity attacker, Entity target)
        {
            // If target is already fighting, do nothing
            if (_world.Has<CombatState>(target)) return;

            // If target has no weapon, they can't fight back
            if (!_world.Has<Weapon>(target)) return;

            // Start combat
            _world.Add(target, new CombatState 
            { 
                Target = attacker, 
                NextAttackTime = DateTime.UtcNow 
            });

            await SendMessage(target, $"You are under attack by {GetEntityName(attacker)}! Engaging!");
            await SendMessage(attacker, $"{GetEntityName(target)} turns to fight you!");
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
                        // We can't await inside this query callback easily if we want to be safe, 
                        // but SendAsync is usually fire-and-forget safe enough for this context 
                        // or we should collect sessions and send after.
                        // For now, we'll just fire it.
                        _ = session.Connection.SendAsync(message);
                    }
                }
            });
        }
    }
}
