using System;
using System.Linq;
using System.Threading.Tasks;
using Arch.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharpMUD.Core;
using SharpMUD.Core.Components;
using SharpMUD.Data;
using SharpMUD.Data.Models;

namespace SharpMUD.Game
{
    public class CommandParser
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly WorldManager _worldManager;

        public CommandParser(IServiceProvider serviceProvider, WorldManager worldManager)
        {
            _serviceProvider = serviceProvider;
            _worldManager = worldManager;
        }

        public async Task ParseAsync(PlayerSession session, string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var verb = parts[0].ToLower();

            if (session.State == SessionState.Connected)
            {
                if (verb == "login" && parts.Length > 1)
                {
                    var username = parts[1];
                    await HandleLoginAsync(session, username);
                }
                else
                {
                    await session.Connection.SendAsync("Please login first: login <username>");
                }
                return;
            }

            switch (verb)
            {
                case "move":
                case "n":
                case "s":
                case "e":
                case "w":
                case "u":
                case "d":
                    await HandleMove(session, verb == "move" && parts.Length > 1 ? parts[1] : verb);
                    break;
                case "look":
                case "l":
                    await HandleLook(session);
                    break;
                case "get":
                case "take":
                case "grab":
                    if (parts.Length > 1)
                        await HandleGet(session, string.Join(" ", parts.Skip(1)));
                    else
                        await session.Connection.SendAsync("Get what?");
                    break;
                case "loot":
                    if (parts.Length > 1)
                        await HandleLoot(session, string.Join(" ", parts.Skip(1)));
                    else
                        await session.Connection.SendAsync("Loot what?");
                    break;
                case "inventory":
                case "i":
                    await HandleInventory(session);
                    break;
                case "drop":
                    if (parts.Length > 1)
                        await HandleDrop(session, string.Join(" ", parts.Skip(1)));
                    else
                        await session.Connection.SendAsync("Drop what?");
                    break;
                case "equip":
                case "wear":
                case "wield":
                    if (parts.Length > 1)
                        await HandleEquip(session, string.Join(" ", parts.Skip(1)));
                    else
                        await session.Connection.SendAsync("Equip what?");
                    break;
                case "unequip":
                case "remove":
                    if (parts.Length > 1)
                        await HandleUnequip(session, string.Join(" ", parts.Skip(1)));
                    else
                        await session.Connection.SendAsync("Unequip what?");
                    break;
                case "quests":
                case "quest":
                    if (parts.Length > 1)
                    {
                        var subCommand = parts[1].ToLower();
                        if (subCommand == "list")
                            await HandleQuestList(session);
                        else if (subCommand == "accept" && parts.Length > 2)
                            await HandleQuestAccept(session, string.Join(" ", parts.Skip(2)));
                        else if (subCommand == "complete" && parts.Length > 2)
                            await HandleQuestComplete(session, string.Join(" ", parts.Skip(2)));
                        else
                            await session.Connection.SendAsync("Usage: quest list, quest accept <quest name>, quest complete <quest name>");
                    }
                    else
                    {
                        await HandleQuestList(session);
                    }
                    break;
                case "attack":
                case "fire":
                    if (parts.Length > 1)
                        await HandleAttack(session, string.Join(" ", parts.Skip(1)));
                    else
                        await session.Connection.SendAsync("Attack what?");
                    break;
                case "cast":
                case "use":
                    if (parts.Length > 1)
                        await HandleCast(session, string.Join(" ", parts.Skip(1)));
                    else
                        await session.Connection.SendAsync("Cast what?");
                    break;
                case "stop":
                    await HandleStop(session);
                    break;
                case "land":
                    if (parts.Length > 1)
                        await HandleLand(session, string.Join(" ", parts.Skip(1)));
                    else
                        await session.Connection.SendAsync("Land on what?");
                    break;
                case "launch":
                    await HandleLaunch(session);
                    break;
                case "score":
                case "status":
                    await HandleScore(session);
                    break;
                case "buy":
                    if (parts.Length > 1)
                        await HandleBuy(session, string.Join(" ", parts.Skip(1)));
                    else
                        await session.Connection.SendAsync("Buy what?");
                    break;
                case "sell":
                    if (parts.Length > 1)
                        await HandleSell(session, string.Join(" ", parts.Skip(1)));
                    else
                        await session.Connection.SendAsync("Sell what?");
                    break;
                case "help":
                    await session.Connection.SendAsync("Commands: look, move <direction>, attack <target>, land <planet>, launch, score, buy <item>, sell <item>, stop, quit");
                    break;
                case "quit":
                    await SavePlayerState(session);
                    await session.Connection.SendAsync("Goodbye.");
                    await session.Connection.DisconnectAsync();
                    break;
                default:
                    await session.Connection.SendAsync("Unknown command.");
                    break;
            }
        }

        private async Task HandleLook(PlayerSession session)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;
            Entity observer = playerEntity;

            if (_worldManager.World.Has<Controlling>(playerEntity))
            {
                observer = _worldManager.World.Get<Controlling>(playerEntity).Target;
            }

            if (!_worldManager.World.IsAlive(observer)) return;

            if (_worldManager.World.Has<SpacePosition>(observer))
            {
                var myPos = _worldManager.World.Get<SpacePosition>(observer);
                await session.Connection.SendAsync($"You are at Sector {myPos.SectorId} ({myPos.X}, {myPos.Y}, {myPos.Z})");
                
                var query = new QueryDescription().WithAll<SpacePosition, Description>();
                _worldManager.World.Query(in query, (Entity entity, ref SpacePosition pos, ref Description desc) => 
                {
                    if (entity == observer) return;
                    if (pos.SectorId == myPos.SectorId && pos.X == myPos.X && pos.Y == myPos.Y && pos.Z == myPos.Z)
                    {
                        _ = session.Connection.SendAsync(desc.Long);
                    }
                });
            }
            else if (_worldManager.World.Has<LandPosition>(observer))
            {
                var myPos = _worldManager.World.Get<LandPosition>(observer);
                await session.Connection.SendAsync($"You are at Zone {myPos.ZoneId} ({myPos.X}, {myPos.Y})");

                var query = new QueryDescription().WithAll<LandPosition, Description>();
                _worldManager.World.Query(in query, (Entity entity, ref LandPosition pos, ref Description desc) => 
                {
                    if (entity == observer) return;
                    if (pos.ZoneId == myPos.ZoneId && pos.X == myPos.X && pos.Y == myPos.Y)
                    {
                        _ = session.Connection.SendAsync(desc.Long);
                    }
                });
            }
        }

        private async Task HandleMove(PlayerSession session, string direction)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;
            
            Entity mover = playerEntity;
            
            // Check if controlling a ship
            if (_worldManager.World.Has<Controlling>(playerEntity))
            {
                mover = _worldManager.World.Get<Controlling>(playerEntity).Target;
            }

            if (!_worldManager.World.IsAlive(mover)) return;

            // Stop Combat if moving
            if (_worldManager.World.Has<CombatState>(mover))
            {
                _worldManager.World.Remove<CombatState>(mover);
                await session.Connection.SendAsync("You break off the attack.");
            }

            if (_worldManager.World.Has<SpacePosition>(mover))
            {
                var pos = _worldManager.World.Get<SpacePosition>(mover);
                switch (direction.ToLower())
                {
                    case "n": pos.Y += 1; break;
                    case "s": pos.Y -= 1; break;
                    case "e": pos.X += 1; break;
                    case "w": pos.X -= 1; break;
                    case "u": pos.Z += 1; break;
                    case "d": pos.Z -= 1; break;
                    default:
                        await session.Connection.SendAsync("Invalid direction. Use n, s, e, w, u, d.");
                        return;
                }
                _worldManager.World.Set(mover, pos);
                await session.Connection.SendAsync($"Moved {direction}. Position: {pos.X:F1}, {pos.Y:F1}, {pos.Z:F1}");
            }
            else if (_worldManager.World.Has<LandPosition>(mover))
            {
                var pos = _worldManager.World.Get<LandPosition>(mover);
                switch (direction.ToLower())
                {
                    case "n": pos.Y += 1; break;
                    case "s": pos.Y -= 1; break;
                    case "e": pos.X += 1; break;
                    case "w": pos.X -= 1; break;
                    default:
                        await session.Connection.SendAsync("Invalid direction. Use n, s, e, w.");
                        return;
                }
                _worldManager.World.Set(mover, pos);
                await session.Connection.SendAsync($"Moved {direction}. Position: {pos.X:F1}, {pos.Y:F1}");
            }
            else
            {
                 await session.Connection.SendAsync("You cannot move.");
            }
        }

        private async Task HandleLoginAsync(PlayerSession session, string username)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SharpMUDContext>();

            var player = await db.Players.Include(p => p.Items).FirstOrDefaultAsync(p => p.Username == username);
            if (player == null)
            {
                // Auto-create for now
                player = new PlayerAccount
                {
                    Username = username,
                    PasswordHash = "dummy", // TODO: Hash
                    LastLogin = DateTime.UtcNow,
                    X = 0, Y = 0, Z = 0,
                    LocationId = "Alpha",
                    IsSpace = true,
                    CurrentHealth = 100,
                    MaxHealth = 100,
                    Experience = 0,
                    Level = 1,
                    Money = 100
                };
                db.Players.Add(player);
                await db.SaveChangesAsync();
                await session.Connection.SendAsync($"Account created. Welcome, {username}!");
            }
            else
            {
                player.LastLogin = DateTime.UtcNow;
                await db.SaveChangesAsync();
                await session.Connection.SendAsync($"Welcome back, {username}!");
            }

            session.Username = player.Username;
            session.AccountId = player.Id;
            session.State = SessionState.InGame;

            // Create Player Entity
            var playerEntity = _worldManager.World.Create(
                new Player { Name = username, ConnectionId = session.Connection.ConnectionId },
                new Description { Short = username, Long = $"This is {username}." },
                new Health { Current = player.CurrentHealth, Max = player.MaxHealth },
                new Experience { Value = player.Experience, Level = player.Level },
                new Money { Amount = player.Money },
                new Mana { Current = 100, Max = 100 }, // Default Mana
                new KnownSkills { SkillIds = new System.Collections.Generic.List<string> { "skill_fireball", "skill_heal" } }, // Default Skills
                new SkillCooldowns(),
                new Weapon { Name = "Blaster", Range = 100, Damage = 10, CooldownMs = 1000, LastFired = DateTime.MinValue }
            );
            
            if (player.IsSpace)
            {
                _worldManager.World.Add(playerEntity, new SpacePosition { X = player.X, Y = player.Y, Z = player.Z, SectorId = player.LocationId });
                // Treat player as a ship in space for now
                _worldManager.World.Add(playerEntity, new Ship { Name = $"{username}'s Ship", Shields = 100, MaxShields = 100, Hull = 100, MaxHull = 100 });
            }
            else
            {
                _worldManager.World.Add(playerEntity, new LandPosition { X = player.X, Y = player.Y, ZoneId = player.LocationId });
            }
            
            session.Entity = playerEntity;

            // Load Items
            if (player.Items != null)
            {
                foreach (var item in player.Items)
                {
                    _worldManager.World.Create(
                        new Item { Value = item.Value, Weight = item.Weight },
                        new Description { Short = item.Name, Long = item.Name },
                        new ContainedBy { Container = playerEntity },
                        new DbId { Id = item.Id }
                    );
                }
            }
        }

        private async Task HandleAttack(PlayerSession session, string targetName)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;

            // Determine attacker (Player or Ship)
            Entity attacker = playerEntity;
            if (_worldManager.World.Has<Controlling>(playerEntity))
            {
                attacker = _worldManager.World.Get<Controlling>(playerEntity).Target;
            }

            if (!_worldManager.World.Has<Weapon>(attacker))
            {
                await session.Connection.SendAsync("You have no weapons!");
                return;
            }

            var weapon = _worldManager.World.Get<Weapon>(attacker);

            if ((DateTime.UtcNow - weapon.LastFired).TotalMilliseconds < weapon.CooldownMs)
            {
                await session.Connection.SendAsync("Weapons are recharging...");
                return;
            }

            Entity? targetEntity = null;
            double distance = double.MaxValue;

            // Space Combat
            if (_worldManager.World.Has<SpacePosition>(attacker))
            {
                var myPos = _worldManager.World.Get<SpacePosition>(attacker);
                var query = new QueryDescription().WithAll<SpacePosition, Ship>();
                
                // Arch Query to find target
                var entities = new System.Collections.Generic.List<Entity>();
                _worldManager.World.Query(in query, (Entity entity) => entities.Add(entity));

                foreach(var entity in entities)
                {
                    if (entity == attacker) continue;
                    
                    var ship = _worldManager.World.Get<Ship>(entity);
                    if (ship.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        var pos = _worldManager.World.Get<SpacePosition>(entity);
                        if (pos.SectorId == myPos.SectorId)
                        {
                            var dist = Math.Sqrt(Math.Pow(pos.X - myPos.X, 2) + Math.Pow(pos.Y - myPos.Y, 2) + Math.Pow(pos.Z - myPos.Z, 2));
                            if (dist < distance)
                            {
                                distance = dist;
                                targetEntity = entity;
                            }
                        }
                    }
                }
            }
            // Land Combat
            else if (_worldManager.World.Has<LandPosition>(attacker))
            {
                var myPos = _worldManager.World.Get<LandPosition>(attacker);
                var query = new QueryDescription().WithAll<LandPosition, Health>();
                
                var entities = new System.Collections.Generic.List<Entity>();
                _worldManager.World.Query(in query, (Entity entity) => entities.Add(entity));

                foreach(var entity in entities)
                {
                    if (entity == attacker) continue;
                    
                    string name = "Unknown";
                    if (_worldManager.World.Has<Description>(entity))
                        name = _worldManager.World.Get<Description>(entity).Short;
                    else if (_worldManager.World.Has<Player>(entity))
                        name = _worldManager.World.Get<Player>(entity).Name;

                    if (name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        var pos = _worldManager.World.Get<LandPosition>(entity);
                        if (pos.ZoneId == myPos.ZoneId)
                        {
                            var dist = Math.Sqrt(Math.Pow(pos.X - myPos.X, 2) + Math.Pow(pos.Y - myPos.Y, 2));
                            if (dist < distance)
                            {
                                distance = dist;
                                targetEntity = entity;
                            }
                        }
                    }
                }
            }

            if (targetEntity == null)
            {
                await session.Connection.SendAsync("Target not found.");
                return;
            }

            if (distance > weapon.Range)
            {
                await session.Connection.SendAsync("Target is out of range.");
                return;
            }

            // Start Combat
            var combatState = new CombatState 
            { 
                Target = targetEntity.Value,
                NextAttackTime = DateTime.UtcNow
            };
            
            if (_worldManager.World.Has<CombatState>(attacker))
            {
                _worldManager.World.Set(attacker, combatState);
                await session.Connection.SendAsync($"Switched target to {targetName}.");
            }
            else
            {
                _worldManager.World.Add(attacker, combatState);
                await session.Connection.SendAsync($"Attacking {targetName}!");
            }
        }

        private async Task HandleStop(PlayerSession session)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;
            
            Entity attacker = playerEntity;
            if (_worldManager.World.Has<Controlling>(playerEntity))
            {
                attacker = _worldManager.World.Get<Controlling>(playerEntity).Target;
            }

            if (_worldManager.World.Has<CombatState>(attacker))
            {
                _worldManager.World.Remove<CombatState>(attacker);
                await session.Connection.SendAsync("You stop attacking.");
            }
            else
            {
                await session.Connection.SendAsync("You are not attacking anyone.");
            }
        }

        private async Task HandleLoot(PlayerSession session, string targetName)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;
            Entity looter = playerEntity;

            if (_worldManager.World.Has<Controlling>(playerEntity))
            {
                looter = _worldManager.World.Get<Controlling>(playerEntity).Target;
            }

            Entity? targetEntity = null;
            double distance = double.MaxValue;

            if (_worldManager.World.Has<SpacePosition>(looter))
            {
                var myPos = _worldManager.World.Get<SpacePosition>(looter);
                var query = new QueryDescription().WithAll<SpacePosition, Description>();
                
                var entities = new System.Collections.Generic.List<Entity>();
                _worldManager.World.Query(in query, (Entity entity) => entities.Add(entity));

                foreach(var entity in entities)
                {
                    if (entity == looter) continue;
                    
                    var desc = _worldManager.World.Get<Description>(entity);
                    if (desc.Short.Contains(targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        var pos = _worldManager.World.Get<SpacePosition>(entity);
                        if (pos.SectorId == myPos.SectorId)
                        {
                            var dist = Math.Sqrt(Math.Pow(pos.X - myPos.X, 2) + Math.Pow(pos.Y - myPos.Y, 2) + Math.Pow(pos.Z - myPos.Z, 2));
                            if (dist < 5 && dist < distance) // Must be close to loot
                            {
                                distance = dist;
                                targetEntity = entity;
                            }
                        }
                    }
                }
            }
            else if (_worldManager.World.Has<LandPosition>(looter))
            {
                var myPos = _worldManager.World.Get<LandPosition>(looter);
                var query = new QueryDescription().WithAll<LandPosition, Description>();
                
                var entities = new System.Collections.Generic.List<Entity>();
                _worldManager.World.Query(in query, (Entity entity) => entities.Add(entity));

                foreach(var entity in entities)
                {
                    if (entity == looter) continue;
                    
                    var desc = _worldManager.World.Get<Description>(entity);
                    if (desc.Short.Contains(targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        var pos = _worldManager.World.Get<LandPosition>(entity);
                        if (pos.ZoneId == myPos.ZoneId)
                        {
                            var dist = Math.Sqrt(Math.Pow(pos.X - myPos.X, 2) + Math.Pow(pos.Y - myPos.Y, 2));
                            if (dist < 5 && dist < distance)
                            {
                                distance = dist;
                                targetEntity = entity;
                            }
                        }
                    }
                }
            }

            if (targetEntity == null)
            {
                await session.Connection.SendAsync("You don't see that here.");
                return;
            }

            if (!_worldManager.World.Has<Container>(targetEntity.Value))
            {
                await session.Connection.SendAsync("That is not a container.");
                return;
            }

            // Loot items
            var itemQuery = new QueryDescription().WithAll<Item, ContainedBy>();
            bool foundAny = false;
            var itemsToLoot = new System.Collections.Generic.List<Entity>();

            _worldManager.World.Query(in itemQuery, (Entity itemEnt, ref Item item, ref ContainedBy contained) => 
            {
                if (contained.Container == targetEntity.Value)
                {
                    itemsToLoot.Add(itemEnt);
                }
            });

            foreach(var itemEnt in itemsToLoot)
            {
                var desc = _worldManager.World.Get<Description>(itemEnt);
                var item = _worldManager.World.Get<Item>(itemEnt);
                
                // Move to inventory
                var contained = _worldManager.World.Get<ContainedBy>(itemEnt);
                contained.Container = looter;
                _worldManager.World.Set(itemEnt, contained);

                await session.Connection.SendAsync($"You loot {desc.Short} (Value: {item.Value}).");
                foundAny = true;
            }
            
            if (!foundAny)
            {
                await session.Connection.SendAsync("It is empty.");
            }
        }

        private async Task HandleInventory(PlayerSession session)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;
            Entity looter = playerEntity;

            if (_worldManager.World.Has<Controlling>(playerEntity))
            {
                looter = _worldManager.World.Get<Controlling>(playerEntity).Target;
            }

            var itemQuery = new QueryDescription().WithAll<Item, ContainedBy, Description>();
            var items = new System.Collections.Generic.List<string>();

            _worldManager.World.Query(in itemQuery, (Entity itemEnt, ref Item item, ref ContainedBy contained, ref Description desc) => 
            {
                if (contained.Container == looter && !_worldManager.World.Has<Equipped>(itemEnt))
                {
                    items.Add($"{desc.Short} (Value: {item.Value})");
                }
            });

            if (items.Count == 0)
            {
                await session.Connection.SendAsync("You are not carrying anything.");
            }
            else
            {
                await session.Connection.SendAsync("You are carrying:");
                foreach (var item in items)
                {
                    await session.Connection.SendAsync($"- {item}");
                }
            }
        }

        private async Task HandleDrop(PlayerSession session, string itemName)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;
            Entity looter = playerEntity;

            if (_worldManager.World.Has<Controlling>(playerEntity))
            {
                looter = _worldManager.World.Get<Controlling>(playerEntity).Target;
            }

            var itemQuery = new QueryDescription().WithAll<Item, ContainedBy, Description>();
            Entity? itemToDrop = null;

            _worldManager.World.Query(in itemQuery, (Entity itemEnt, ref Item item, ref ContainedBy contained, ref Description desc) => 
            {
                if (contained.Container == looter && desc.Short.Equals(itemName, StringComparison.OrdinalIgnoreCase))
                {
                    itemToDrop = itemEnt;
                }
            });

            if (itemToDrop == null)
            {
                await session.Connection.SendAsync("You don't have that.");
                return;
            }

            _worldManager.World.Remove<ContainedBy>(itemToDrop.Value);
            
            if (_worldManager.World.Has<SpacePosition>(looter))
            {
                _worldManager.World.Add(itemToDrop.Value, _worldManager.World.Get<SpacePosition>(looter));
            }
            else if (_worldManager.World.Has<LandPosition>(looter))
            {
                _worldManager.World.Add(itemToDrop.Value, _worldManager.World.Get<LandPosition>(looter));
            }

            await session.Connection.SendAsync($"You drop {itemName}.");
        }

        private async Task HandleLand(PlayerSession session, string planetName)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;
            
            if (!_worldManager.World.Has<SpacePosition>(playerEntity))
            {
                await session.Connection.SendAsync("You are not in space.");
                return;
            }

            var myPos = _worldManager.World.Get<SpacePosition>(playerEntity);
            Entity? planetEntity = null;
            double distance = double.MaxValue;

            var query = new QueryDescription().WithAll<SpacePosition, Planet, Description>();
            var entities = new System.Collections.Generic.List<Entity>();
            _worldManager.World.Query(in query, (Entity entity) => entities.Add(entity));

            foreach(var entity in entities)
            {
                var desc = _worldManager.World.Get<Description>(entity);
                if (desc.Short.Contains(planetName, StringComparison.OrdinalIgnoreCase))
                {
                    var pos = _worldManager.World.Get<SpacePosition>(entity);
                    if (pos.SectorId == myPos.SectorId)
                    {
                        var dist = Math.Sqrt(Math.Pow(pos.X - myPos.X, 2) + Math.Pow(pos.Y - myPos.Y, 2) + Math.Pow(pos.Z - myPos.Z, 2));
                        if (dist < 10 && dist < distance) // Must be close to land
                        {
                            distance = dist;
                            planetEntity = entity;
                        }
                    }
                }
            }

            if (planetEntity == null)
            {
                await session.Connection.SendAsync("You don't see that planet nearby.");
                return;
            }

            var planet = _worldManager.World.Get<Planet>(planetEntity.Value);
            
            // Transition to Land
            _worldManager.World.Remove<SpacePosition>(playerEntity);
            if (_worldManager.World.Has<Ship>(playerEntity))
            {
                _worldManager.World.Remove<Ship>(playerEntity);
            }

            _worldManager.World.Add(playerEntity, new LandPosition { X = 0, Y = 0, ZoneId = planet.ZoneId });
            
            await session.Connection.SendAsync($"Landing on {planet.Name}...");
            await HandleLook(session);
        }

        private async Task HandleLaunch(PlayerSession session)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;

            if (!_worldManager.World.Has<LandPosition>(playerEntity))
            {
                await session.Connection.SendAsync("You are not on a planet.");
                return;
            }

            var myPos = _worldManager.World.Get<LandPosition>(playerEntity);
            
            // Find which planet corresponds to this ZoneId to determine Sector
            // For now, we'll just assume a default mapping or look it up
            string sectorId = "Alpha";
            double x = 0, y = 0, z = 0;

            var query = new QueryDescription().WithAll<SpacePosition, Planet>();
            bool foundPlanet = false;
            
            _worldManager.World.Query(in query, (Entity entity, ref SpacePosition pos, ref Planet planet) => 
            {
                if (planet.ZoneId == myPos.ZoneId)
                {
                    sectorId = pos.SectorId;
                    x = pos.X;
                    y = pos.Y;
                    z = pos.Z;
                    foundPlanet = true;
                }
            });

            if (!foundPlanet)
            {
                // Fallback if planet entity not found (shouldn't happen if world is persistent)
                sectorId = "Alpha"; 
            }

            // Transition to Space
            _worldManager.World.Remove<LandPosition>(playerEntity);
            _worldManager.World.Add(playerEntity, new SpacePosition { X = x, Y = y, Z = z, SectorId = sectorId });
            _worldManager.World.Add(playerEntity, new Ship { Name = $"{session.Username}'s Ship", Shields = 100, MaxShields = 100, Hull = 100, MaxHull = 100 });

            await session.Connection.SendAsync("Launching into space...");
            await HandleLook(session);
        }

        private async Task HandleScore(PlayerSession session)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Name: {session.Username}");
            
            if (_worldManager.World.Has<Health>(playerEntity))
            {
                var health = _worldManager.World.Get<Health>(playerEntity);
                sb.AppendLine($"Health: {health.Current}/{health.Max}");
            }

            if (_worldManager.World.Has<Experience>(playerEntity))
            {
                var xp = _worldManager.World.Get<Experience>(playerEntity);
                sb.AppendLine($"Level: {xp.Level}");
                sb.AppendLine($"XP: {xp.Value}");
            }

            if (_worldManager.World.Has<Money>(playerEntity))
            {
                var money = _worldManager.World.Get<Money>(playerEntity);
                sb.AppendLine($"Money: {money.Amount} Credits");
            }

            await session.Connection.SendAsync(sb.ToString());
        }

        private async Task HandleBuy(PlayerSession session, string itemName)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;

            // Check if shopkeeper is present
            Entity? shopkeeper = null;
            
            if (_worldManager.World.Has<LandPosition>(playerEntity))
            {
                var myPos = _worldManager.World.Get<LandPosition>(playerEntity);
                var query = new QueryDescription().WithAll<LandPosition, Shopkeeper>();
                
                _worldManager.World.Query(in query, (Entity entity, ref LandPosition pos) => 
                {
                    if (pos.ZoneId == myPos.ZoneId && pos.X == myPos.X && pos.Y == myPos.Y)
                    {
                        shopkeeper = entity;
                    }
                });
            }

            if (shopkeeper == null)
            {
                await session.Connection.SendAsync("There is no shop here.");
                return;
            }

            // For now, shopkeepers sell infinite "Health Potion" for 10 credits
            if (itemName.Equals("Health Potion", StringComparison.OrdinalIgnoreCase))
            {
                int cost = 10;
                var money = _worldManager.World.Get<Money>(playerEntity);
                
                if (money.Amount < cost)
                {
                    await session.Connection.SendAsync("You cannot afford that.");
                    return;
                }

                money.Amount -= cost;
                _worldManager.World.Set(playerEntity, money);

                _worldManager.World.Create(
                    new Item { Value = 5, Weight = 1 },
                    new Description { Short = "Health Potion", Long = "A small vial of red liquid." },
                    new ContainedBy { Container = playerEntity }
                );

                await session.Connection.SendAsync("You bought a Health Potion.");
            }
            else
            {
                await session.Connection.SendAsync("The shopkeeper doesn't sell that.");
            }
        }

        private async Task HandleSell(PlayerSession session, string itemName)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;

            // Check if shopkeeper is present
            bool shopkeeperPresent = false;
            if (_worldManager.World.Has<LandPosition>(playerEntity))
            {
                var myPos = _worldManager.World.Get<LandPosition>(playerEntity);
                var query = new QueryDescription().WithAll<LandPosition, Shopkeeper>();
                
                _worldManager.World.Query(in query, (Entity entity, ref LandPosition pos) => 
                {
                    if (pos.ZoneId == myPos.ZoneId && pos.X == myPos.X && pos.Y == myPos.Y)
                    {
                        shopkeeperPresent = true;
                    }
                });
            }

            if (!shopkeeperPresent)
            {
                await session.Connection.SendAsync("There is no shop here.");
                return;
            }

            // Find item in inventory
            Entity? itemToSell = null;
            int value = 0;

            var itemQuery = new QueryDescription().WithAll<Item, ContainedBy, Description>();
            _worldManager.World.Query(in itemQuery, (Entity entity, ref Item item, ref ContainedBy contained, ref Description desc) => 
            {
                if (contained.Container == playerEntity && desc.Short.Equals(itemName, StringComparison.OrdinalIgnoreCase))
                {
                    itemToSell = entity;
                    value = item.Value;
                }
            });

            if (itemToSell == null)
            {
                await session.Connection.SendAsync("You don't have that.");
                return;
            }

            // Sell it
            _worldManager.World.Destroy(itemToSell.Value);
            
            var money = _worldManager.World.Get<Money>(playerEntity);
            money.Amount += value;
            _worldManager.World.Set(playerEntity, money);

            await session.Connection.SendAsync($"You sold {itemName} for {value} Credits.");
        }

        private async Task HandleEquip(PlayerSession session, string itemName)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;
            Entity looter = playerEntity;

            if (_worldManager.World.Has<Controlling>(playerEntity))
            {
                looter = _worldManager.World.Get<Controlling>(playerEntity).Target;
            }

            // Find item in inventory
            var itemQuery = new QueryDescription().WithAll<Item, ContainedBy, Description, Equippable>();
            Entity? targetItem = null;
            
            _worldManager.World.Query(in itemQuery, (Entity itemEnt, ref ContainedBy contained, ref Description desc) => 
            {
                if (contained.Container == looter && !_worldManager.World.Has<Equipped>(itemEnt))
                {
                    if (desc.Short.Contains(itemName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetItem = itemEnt;
                    }
                }
            });

            if (targetItem == null)
            {
                await session.Connection.SendAsync("You don't have that.");
                return;
            }

            var equippable = _worldManager.World.Get<Equippable>(targetItem.Value);
            
            // Ensure player has Equipment component
            if (!_worldManager.World.Has<Equipment>(looter))
            {
                _worldManager.World.Add(looter, new Equipment());
            }
            
            var equipment = _worldManager.World.Get<Equipment>(looter);
            Entity oldItem = Entity.Null;

            // Check slot
            switch (equippable.Slot)
            {
                case EquipmentSlot.Head:
                    oldItem = equipment.Head;
                    equipment.Head = targetItem.Value;
                    break;
                case EquipmentSlot.Chest:
                    oldItem = equipment.Chest;
                    equipment.Chest = targetItem.Value;
                    break;
                case EquipmentSlot.Legs:
                    oldItem = equipment.Legs;
                    equipment.Legs = targetItem.Value;
                    break;
                case EquipmentSlot.Feet:
                    oldItem = equipment.Feet;
                    equipment.Feet = targetItem.Value;
                    break;
                case EquipmentSlot.MainHand:
                    oldItem = equipment.MainHand;
                    equipment.MainHand = targetItem.Value;
                    break;
                case EquipmentSlot.OffHand:
                    oldItem = equipment.OffHand;
                    equipment.OffHand = targetItem.Value;
                    break;
            }

            // Unequip old item if exists
            if (oldItem != Entity.Null && _worldManager.World.IsAlive(oldItem))
            {
                _worldManager.World.Remove<Equipped>(oldItem);
                var oldDesc = _worldManager.World.Get<Description>(oldItem);
                await session.Connection.SendAsync($"You remove {oldDesc.Short}.");
            }

            // Equip new item
            _worldManager.World.Set(looter, equipment);
            _worldManager.World.Add(targetItem.Value, new Equipped { Wearer = looter, Slot = equippable.Slot });
            
            var newDesc = _worldManager.World.Get<Description>(targetItem.Value);
            await session.Connection.SendAsync($"You equip {newDesc.Short}.");
        }

        private async Task HandleUnequip(PlayerSession session, string itemName)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;
            Entity looter = playerEntity;

            if (_worldManager.World.Has<Controlling>(playerEntity))
            {
                looter = _worldManager.World.Get<Controlling>(playerEntity).Target;
            }

            if (!_worldManager.World.Has<Equipment>(looter))
            {
                await session.Connection.SendAsync("You are not wearing anything.");
                return;
            }

            var equipment = _worldManager.World.Get<Equipment>(looter);
            Entity targetItem = Entity.Null;

            // Check slots for item name
            // Helper to check slot
            bool CheckSlot(Entity itemEnt, out string name)
            {
                name = "";
                if (itemEnt == Entity.Null || !_worldManager.World.IsAlive(itemEnt)) return false;
                var desc = _worldManager.World.Get<Description>(itemEnt);
                name = desc.Short;
                return name.Contains(itemName, StringComparison.OrdinalIgnoreCase);
            }

            string foundName;
            if (CheckSlot(equipment.Head, out foundName)) { targetItem = equipment.Head; equipment.Head = Entity.Null; }
            else if (CheckSlot(equipment.Chest, out foundName)) { targetItem = equipment.Chest; equipment.Chest = Entity.Null; }
            else if (CheckSlot(equipment.Legs, out foundName)) { targetItem = equipment.Legs; equipment.Legs = Entity.Null; }
            else if (CheckSlot(equipment.Feet, out foundName)) { targetItem = equipment.Feet; equipment.Feet = Entity.Null; }
            else if (CheckSlot(equipment.MainHand, out foundName)) { targetItem = equipment.MainHand; equipment.MainHand = Entity.Null; }
            else if (CheckSlot(equipment.OffHand, out foundName)) { targetItem = equipment.OffHand; equipment.OffHand = Entity.Null; }

            if (targetItem == Entity.Null)
            {
                await session.Connection.SendAsync("You are not wearing that.");
                return;
            }

            _worldManager.World.Set(looter, equipment);
            _worldManager.World.Remove<Equipped>(targetItem);
            
            await session.Connection.SendAsync($"You unequip {foundName}.");
        }

        private async Task HandleGet(PlayerSession session, string itemName)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;
            Entity looter = playerEntity;

            if (_worldManager.World.Has<Controlling>(playerEntity))
            {
                looter = _worldManager.World.Get<Controlling>(playerEntity).Target;
            }

            // Find item on ground at player's location
            // Check LandPosition
            if (!_worldManager.World.Has<LandPosition>(looter))
            {
                await session.Connection.SendAsync("You can't pick things up here.");
                return;
            }

            var myPos = _worldManager.World.Get<LandPosition>(looter);
            var query = new QueryDescription().WithAll<LandPosition, Item, Description>();
            
            Entity? targetItem = null;
            
            _worldManager.World.Query(in query, (Entity entity, ref LandPosition pos, ref Description desc) => 
            {
                if (pos.ZoneId == myPos.ZoneId && pos.X == myPos.X && pos.Y == myPos.Y)
                {
                    if (desc.Short.Contains(itemName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetItem = entity;
                    }
                }
            });

            if (targetItem == null)
            {
                await session.Connection.SendAsync("You don't see that here.");
                return;
            }

            // Pick it up
            _worldManager.World.Remove<LandPosition>(targetItem.Value);
            _worldManager.World.Add(targetItem.Value, new ContainedBy { Container = looter });
            
            var itemDesc = _worldManager.World.Get<Description>(targetItem.Value);
            await session.Connection.SendAsync($"You pick up {itemDesc.Short}.");
        }

        private async Task HandleQuestList(PlayerSession session)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;

            if (!_worldManager.World.Has<QuestLog>(playerEntity))
            {
                await session.Connection.SendAsync("You have no active quests.");
                return;
            }

            var questLog = _worldManager.World.Get<QuestLog>(playerEntity);
            if (questLog.Quests.Count == 0)
            {
                await session.Connection.SendAsync("You have no active quests.");
                return;
            }

            await session.Connection.SendAsync("--- Quest Log ---");
            foreach (var questState in questLog.Quests)
            {
                if (_worldManager.QuestRegistry.TryGetValue(questState.QuestId, out var questDef))
                {
                    string status = questState.Status.ToString();
                    string progress = "";
                    if (questState.Status == QuestStatus.InProgress)
                    {
                        progress = $" ({questState.Progress}/{questDef.TargetCount})";
                    }
                    await session.Connection.SendAsync($"{questDef.Title}: {status}{progress}");
                }
                else
                {
                    await session.Connection.SendAsync($"Unknown Quest ({questState.QuestId}): {questState.Status}");
                }
            }
        }

        private async Task HandleQuestAccept(PlayerSession session, string questName)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;
            Entity looter = playerEntity;

            if (_worldManager.World.Has<Controlling>(playerEntity))
            {
                looter = _worldManager.World.Get<Controlling>(playerEntity).Target;
            }

            // Find Quest Giver in the same room/location
            // Check LandPosition
            if (!_worldManager.World.Has<LandPosition>(looter))
            {
                await session.Connection.SendAsync("You can't accept quests here.");
                return;
            }

            var myPos = _worldManager.World.Get<LandPosition>(looter);
            var query = new QueryDescription().WithAll<LandPosition, QuestGiver>();
            
            string? foundQuestId = null;
            QuestDefinition? foundQuestDef = null;

            _worldManager.World.Query(in query, (Entity entity, ref LandPosition pos, ref QuestGiver giver) => 
            {
                if (pos.ZoneId == myPos.ZoneId && pos.X == myPos.X && pos.Y == myPos.Y)
                {
                    foreach (var questId in giver.QuestIds)
                    {
                        if (_worldManager.QuestRegistry.TryGetValue(questId, out var questDef))
                        {
                            if (questDef.Title.Contains(questName, StringComparison.OrdinalIgnoreCase))
                            {
                                foundQuestId = questId;
                                foundQuestDef = questDef;
                                return; // Found it
                            }
                        }
                    }
                }
            });

            if (foundQuestId == null || foundQuestDef == null)
            {
                await session.Connection.SendAsync("No one here has that quest for you.");
                return;
            }

            // Add to QuestLog
            if (!_worldManager.World.Has<QuestLog>(playerEntity))
            {
                _worldManager.World.Add(playerEntity, new QuestLog());
            }

            var questLog = _worldManager.World.Get<QuestLog>(playerEntity);
            
            if (questLog.Quests.Any(q => q.QuestId == foundQuestId))
            {
                await session.Connection.SendAsync("You already have that quest.");
                return;
            }

            questLog.Quests.Add(new PlayerQuestState
            {
                QuestId = foundQuestId,
                Status = QuestStatus.InProgress,
                Progress = 0
            });

            await session.Connection.SendAsync($"Quest Accepted: {foundQuestDef.Title}");
            await session.Connection.SendAsync(foundQuestDef.Description);
        }

        private async Task HandleQuestComplete(PlayerSession session, string questName)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;
            Entity looter = playerEntity;

            if (_worldManager.World.Has<Controlling>(playerEntity))
            {
                looter = _worldManager.World.Get<Controlling>(playerEntity).Target;
            }

            if (!_worldManager.World.Has<QuestLog>(playerEntity))
            {
                await session.Connection.SendAsync("You have no active quests.");
                return;
            }

            var questLog = _worldManager.World.Get<QuestLog>(playerEntity);
            
            // Find the quest in the log by name
            PlayerQuestState? targetQuestState = null;
            QuestDefinition? targetQuestDef = null;

            foreach (var qState in questLog.Quests)
            {
                if (_worldManager.QuestRegistry.TryGetValue(qState.QuestId, out var qDef))
                {
                    if (qDef.Title.Contains(questName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetQuestState = qState;
                        targetQuestDef = qDef;
                        break;
                    }
                }
            }

            if (targetQuestState == null || targetQuestDef == null)
            {
                await session.Connection.SendAsync("You don't have that quest.");
                return;
            }

            if (targetQuestState.Status != QuestStatus.Completed)
            {
                if (targetQuestState.Status == QuestStatus.TurnedIn)
                    await session.Connection.SendAsync("You have already completed that quest.");
                else
                    await session.Connection.SendAsync("You haven't completed the objectives yet.");
                return;
            }

            // Check if Quest Giver is present
            if (!_worldManager.World.Has<LandPosition>(looter))
            {
                await session.Connection.SendAsync("You can't turn in quests here.");
                return;
            }

            var myPos = _worldManager.World.Get<LandPosition>(looter);
            var query = new QueryDescription().WithAll<LandPosition, QuestGiver>();
            bool giverFound = false;

            _worldManager.World.Query(in query, (Entity entity, ref LandPosition pos, ref QuestGiver giver) => 
            {
                if (pos.ZoneId == myPos.ZoneId && pos.X == myPos.X && pos.Y == myPos.Y)
                {
                    if (giver.QuestIds.Contains(targetQuestState.QuestId))
                    {
                        giverFound = true;
                    }
                }
            });

            if (!giverFound)
            {
                await session.Connection.SendAsync("The quest giver is not here.");
                return;
            }

            // Complete Quest
            targetQuestState.Status = QuestStatus.TurnedIn;
            
            // Rewards
            if (targetQuestDef.RewardXp > 0)
            {
                if (_worldManager.World.Has<Experience>(playerEntity))
                {
                    var xp = _worldManager.World.Get<Experience>(playerEntity);
                    xp.Value += targetQuestDef.RewardXp;
                    
                    // Check level up (Duplicate logic from CombatSystem, maybe refactor later)
                    int xpForNextLevel = xp.Level * 1000;
                    if (xp.Value >= xpForNextLevel)
                    {
                        xp.Level++;
                        await session.Connection.SendAsync($"*** LEVEL UP! You are now level {xp.Level}! ***");
                        
                        if (_worldManager.World.Has<Health>(playerEntity))
                        {
                            var health = _worldManager.World.Get<Health>(playerEntity);
                            health.Max += 10;
                            health.Current = health.Max;
                            _worldManager.World.Set(playerEntity, health);
                        }
                    }
                    _worldManager.World.Set(playerEntity, xp);
                    await session.Connection.SendAsync($"You gained {targetQuestDef.RewardXp} XP.");
                }
            }

            if (targetQuestDef.RewardGold > 0)
            {
                if (!_worldManager.World.Has<Money>(playerEntity))
                {
                    _worldManager.World.Add(playerEntity, new Money { Amount = 0 });
                }
                var money = _worldManager.World.Get<Money>(playerEntity);
                money.Amount += targetQuestDef.RewardGold;
                _worldManager.World.Set(playerEntity, money);
                await session.Connection.SendAsync($"You gained {targetQuestDef.RewardGold} credits.");
            }

            await session.Connection.SendAsync($"Quest Completed: {targetQuestDef.Title}");
        }

        public async Task SavePlayerState(PlayerSession session)
        {
            if (!session.Entity.HasValue) return;
            var entity = session.Entity.Value;
            
            Entity target = entity;
            if (_worldManager.World.Has<Controlling>(entity))
            {
                target = _worldManager.World.Get<Controlling>(entity).Target;
            }
            
            if (!_worldManager.World.IsAlive(target)) return;

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SharpMUDContext>();
            var player = await db.Players.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == session.AccountId);
            
            if (player != null)
            {
                if (_worldManager.World.Has<Health>(target))
                {
                    var health = _worldManager.World.Get<Health>(target);
                    player.CurrentHealth = health.Current;
                    player.MaxHealth = health.Max;
                }

                if (_worldManager.World.Has<Experience>(target))
                {
                    var xp = _worldManager.World.Get<Experience>(target);
                    player.Experience = xp.Value;
                    player.Level = xp.Level;
                }

                if (_worldManager.World.Has<Money>(target))
                {
                    var money = _worldManager.World.Get<Money>(target);
                    player.Money = money.Amount;
                }

                if (_worldManager.World.Has<SpacePosition>(target))
                {
                    var pos = _worldManager.World.Get<SpacePosition>(target);
                    player.X = (int)pos.X;
                    player.Y = (int)pos.Y;
                    player.Z = (int)pos.Z;
                    player.LocationId = pos.SectorId;
                    player.IsSpace = true;
                }
                else if (_worldManager.World.Has<LandPosition>(target))
                {
                    var pos = _worldManager.World.Get<LandPosition>(target);
                    player.X = (int)pos.X;
                    player.Y = (int)pos.Y;
                    player.Z = 0;
                    player.LocationId = pos.ZoneId;
                    player.IsSpace = false;
                }

                // Save Items
                var currentItemIds = new System.Collections.Generic.HashSet<int>();
                var itemQuery = new QueryDescription().WithAll<Item, ContainedBy, Description>();
                
                _worldManager.World.Query(in itemQuery, (Entity itemEnt, ref Item item, ref ContainedBy contained, ref Description desc) => 
                {
                    if (contained.Container == target)
                    {
                        if (_worldManager.World.Has<DbId>(itemEnt))
                        {
                            // Update existing
                            var dbId = _worldManager.World.Get<DbId>(itemEnt).Id;
                            currentItemIds.Add(dbId);
                            var dbItem = player.Items.FirstOrDefault(i => i.Id == dbId);
                            if (dbItem != null)
                            {
                                dbItem.Name = desc.Short;
                                dbItem.Value = item.Value;
                                dbItem.Weight = item.Weight;
                            }
                        }
                        else
                        {
                            // Add new
                            var newItem = new PlayerItem
                            {
                                Name = desc.Short,
                                Value = item.Value,
                                Weight = item.Weight
                            };
                            player.Items.Add(newItem);
                        }
                    }
                });

                // Remove deleted items
                var itemsToRemove = player.Items.Where(i => !currentItemIds.Contains(i.Id) && i.Id != 0).ToList();
                foreach (var item in itemsToRemove)
                {
                    db.PlayerItems.Remove(item);
                }
                
                await db.SaveChangesAsync();
            }
        }

        private async Task HandleCast(PlayerSession session, string args)
        {
            if (!session.Entity.HasValue) return;
            var playerEntity = session.Entity.Value;
            Entity caster = playerEntity;

            if (_worldManager.World.Has<Controlling>(playerEntity))
            {
                caster = _worldManager.World.Get<Controlling>(playerEntity).Target;
            }

            if (!_worldManager.World.Has<KnownSkills>(caster))
            {
                await session.Connection.SendAsync("You don't know any skills.");
                return;
            }

            var knownSkills = _worldManager.World.Get<KnownSkills>(caster);
            SkillDefinition? skillToCast = null;
            string targetName = "";

            // Parse args: <skill name> [target]
            // Since skill names can have spaces, this is tricky.
            // We iterate known skills and see if args starts with one.
            foreach (var skillId in knownSkills.SkillIds)
            {
                if (_worldManager.SkillRegistry.TryGetValue(skillId, out var skillDef))
                {
                    if (args.StartsWith(skillDef.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        skillToCast = skillDef;
                        if (args.Length > skillDef.Name.Length)
                        {
                            targetName = args.Substring(skillDef.Name.Length).Trim();
                        }
                        break;
                    }
                }
            }

            if (skillToCast == null)
            {
                await session.Connection.SendAsync("You don't know that skill.");
                return;
            }

            // Check Mana
            if (_worldManager.World.Has<Mana>(caster))
            {
                var mana = _worldManager.World.Get<Mana>(caster);
                if (mana.Current < skillToCast.ManaCost)
                {
                    await session.Connection.SendAsync("Not enough mana.");
                    return;
                }
            }

            // Check Cooldown
            if (_worldManager.World.Has<SkillCooldowns>(caster))
            {
                var cooldowns = _worldManager.World.Get<SkillCooldowns>(caster);
                if (cooldowns.Cooldowns.TryGetValue(skillToCast.Id, out var readyTime))
                {
                    if (DateTime.UtcNow < readyTime)
                    {
                        var remaining = (readyTime - DateTime.UtcNow).TotalSeconds;
                        await session.Connection.SendAsync($"{skillToCast.Name} is on cooldown ({remaining:F1}s).");
                        return;
                    }
                }
            }

            // Execute Skill
            if (skillToCast.Type == SkillType.Heal)
            {
                // Self heal for now
                if (_worldManager.World.Has<Health>(caster))
                {
                    var health = _worldManager.World.Get<Health>(caster);
                    health.Current = Math.Min(health.Max, health.Current + skillToCast.Value);
                    _worldManager.World.Set(caster, health);
                    await session.Connection.SendAsync($"You cast {skillToCast.Name} and heal for {skillToCast.Value}.");
                }
            }
            else if (skillToCast.Type == SkillType.Damage)
            {
                if (string.IsNullOrEmpty(targetName))
                {
                    // If in combat, target current target
                    if (_worldManager.World.Has<CombatState>(caster))
                    {
                        var combat = _worldManager.World.Get<CombatState>(caster);
                        await CastDamageSpell(session, caster, combat.Target, skillToCast);
                    }
                    else
                    {
                        await session.Connection.SendAsync("Cast at whom?");
                        return;
                    }
                }
                else
                {
                    // Find target
                    var target = FindTarget(caster, targetName);
                    if (target == Entity.Null)
                    {
                        await session.Connection.SendAsync("You don't see them here.");
                        return;
                    }
                    await CastDamageSpell(session, caster, target, skillToCast);
                }
            }

            // Deduct Mana
            if (_worldManager.World.Has<Mana>(caster))
            {
                var mana = _worldManager.World.Get<Mana>(caster);
                mana.Current -= skillToCast.ManaCost;
                _worldManager.World.Set(caster, mana);
            }

            // Set Cooldown
            if (!_worldManager.World.Has<SkillCooldowns>(caster))
            {
                _worldManager.World.Add(caster, new SkillCooldowns());
            }
            var cd = _worldManager.World.Get<SkillCooldowns>(caster);
            cd.Cooldowns[skillToCast.Id] = DateTime.UtcNow.AddMilliseconds(skillToCast.CooldownMs);
            // _worldManager.World.Set(caster, cd); // Class reference, no need to set
        }

        private Entity FindTarget(Entity searcher, string name)
        {
            // Check LandPosition
            if (_worldManager.World.Has<LandPosition>(searcher))
            {
                var myPos = _worldManager.World.Get<LandPosition>(searcher);
                var query = new QueryDescription().WithAll<LandPosition, Description>();
                Entity found = Entity.Null;
                
                _worldManager.World.Query(in query, (Entity entity, ref LandPosition pos, ref Description desc) => 
                {
                    if (entity == searcher) return;
                    if (pos.ZoneId == myPos.ZoneId && pos.X == myPos.X && pos.Y == myPos.Y)
                    {
                        if (desc.Short.Contains(name, StringComparison.OrdinalIgnoreCase))
                        {
                            found = entity;
                        }
                    }
                });
                return found;
            }
            return Entity.Null;
        }

        private async Task CastDamageSpell(PlayerSession session, Entity caster, Entity target, SkillDefinition skill)
        {
            if (!_worldManager.World.IsAlive(target))
            {
                await session.Connection.SendAsync("Target is dead or gone.");
                return;
            }

            // Apply Damage
            if (_worldManager.World.Has<Health>(target))
            {
                var health = _worldManager.World.Get<Health>(target);
                health.Current -= skill.Value;
                _worldManager.World.Set(target, health);
                
                var targetDesc = _worldManager.World.Get<Description>(target);
                await session.Connection.SendAsync($"You cast {skill.Name} on {targetDesc.Short} for {skill.Value} damage!");

                if (health.Current <= 0)
                {
                    // Handle Death (Need to call CombatSystem logic or duplicate it? 
                    // Ideally CombatSystem handles death. 
                    // We can just set health to <= 0 and let CombatSystem clean up next tick?
                    // Or we can invoke a helper.
                    // For now, let's just leave it. The CombatSystem might not pick it up if not in combat.
                    // We should probably trigger combat if not already.
                }
                else
                {
                    // Trigger combat if not already
                    if (!_worldManager.World.Has<CombatState>(caster))
                    {
                        _worldManager.World.Add(caster, new CombatState { Target = target, NextAttackTime = DateTime.UtcNow.AddSeconds(1) });
                    }
                    // Target retaliates (handled by CombatSystem usually, but we might need to force it)
                }
            }
        }
    }
}
