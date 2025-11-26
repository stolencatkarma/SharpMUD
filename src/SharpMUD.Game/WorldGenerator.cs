using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using Arch.Core;
using SharpMUD.Core;
using SharpMUD.Core.Components;

namespace SharpMUD.Game
{
    public class WorldGenerator
    {
        private readonly WorldManager _worldManager;

        public WorldGenerator(WorldManager worldManager)
        {
            _worldManager = worldManager;
        }

        public void Generate(string configPath = "world.json")
        {
            string finalPath = configPath;
            if (!File.Exists(finalPath))
            {
                // Try looking in the base directory (where the exe is)
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string pathInBase = Path.Combine(basePath, configPath);
                if (File.Exists(pathInBase))
                {
                    finalPath = pathInBase;
                }
                else
                {
                    // Try looking in the source directory (for development)
                    // Assuming we are in the root of the repo or similar
                    // This is a bit hacky but helps with 'dotnet run' from root
                    string pathInSrc = Path.Combine("src", "SharpMUD.Server", configPath);
                    if (File.Exists(pathInSrc))
                    {
                        finalPath = pathInSrc;
                    }
                }
            }

            if (!File.Exists(finalPath))
            {
                Console.WriteLine($"Config file not found: {configPath} (checked {finalPath}). Using default generation.");
                GenerateSpace();
                GeneratePlanets();
                return;
            }

            try
            {
                Console.WriteLine($"Loading world config from: {finalPath}");
                string jsonString = File.ReadAllText(finalPath);
                var config = JsonSerializer.Deserialize<WorldConfig>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (config == null)
                {
                    Console.WriteLine("Failed to deserialize world config.");
                    return;
                }

                GenerateFromConfig(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading world config: {ex.Message}");
            }
        }

        private void GenerateFromConfig(WorldConfig config)
        {
            // Load Skills
            if (config.Skills != null)
            {
                foreach (var skillConfig in config.Skills)
                {
                    if (string.IsNullOrEmpty(skillConfig.Id)) continue;

                    var skillDef = new SkillDefinition
                    {
                        Id = skillConfig.Id,
                        Name = skillConfig.Name ?? "Unknown Skill",
                        Description = skillConfig.Description ?? "No description.",
                        Type = Enum.TryParse<SkillType>(skillConfig.Type, true, out var type) ? type : SkillType.Damage,
                        ManaCost = skillConfig.ManaCost,
                        CooldownMs = skillConfig.Cooldown,
                        Value = skillConfig.Value,
                        Range = skillConfig.Range
                    };

                    _worldManager.SkillRegistry[skillDef.Id] = skillDef;
                }
            }

            // Load Quests
            if (config.Quests != null)
            {
                foreach (var questConfig in config.Quests)
                {
                    if (string.IsNullOrEmpty(questConfig.Id)) continue;

                    var questDef = new QuestDefinition
                    {
                        Id = questConfig.Id,
                        Title = questConfig.Title ?? "Unknown Quest",
                        Description = questConfig.Description ?? "No description.",
                        Type = Enum.TryParse<QuestType>(questConfig.Type, true, out var type) ? type : QuestType.Kill,
                        TargetName = questConfig.TargetName ?? "",
                        TargetCount = questConfig.TargetCount,
                        RewardXp = questConfig.RewardXp,
                        RewardGold = questConfig.RewardGold
                    };

                    _worldManager.QuestRegistry[questDef.Id] = questDef;
                }
            }

            if (config.Sectors != null)
            {
                foreach (var sector in config.Sectors)
                {
                    if (sector.Planets != null)
                    {
                        foreach (var planet in sector.Planets)
                        {
                            _worldManager.World.Create(
                                new Planet { Name = planet.Name ?? "Unknown", ZoneId = planet.ZoneId ?? "Unknown" },
                                new SpacePosition { X = planet.X, Y = planet.Y, Z = planet.Z, SectorId = sector.Id ?? "Unknown" },
                                new Description { Short = planet.Name ?? "Unknown", Long = planet.Description ?? "A planet." }
                            );
                        }
                    }

                    if (sector.Mobs != null)
                    {
                        foreach (var mob in sector.Mobs)
                        {
                            var entity = _worldManager.World.Create(
                                new Ship { Name = mob.Name ?? "Unknown", Hull = mob.Hull, MaxHull = mob.Hull, Shields = mob.Shields, MaxShields = mob.Shields },
                                new SpacePosition { X = mob.X, Y = mob.Y, Z = mob.Z, SectorId = sector.Id ?? "Unknown" },
                                new Description { Short = mob.Name ?? "Unknown", Long = mob.Description ?? "A ship." }
                            );

                            if (mob.Weapon != null)
                            {
                                _worldManager.World.Add(entity, new Weapon
                                {
                                    Name = mob.Weapon.Name ?? "Weapon",
                                    Damage = mob.Weapon.Damage,
                                    Range = mob.Weapon.Range,
                                    CooldownMs = mob.Weapon.Cooldown
                                });
                            }

                            if (mob.Aggressive)
                            {
                                _worldManager.World.Add(entity, new Aggressive());
                            }
                        }
                    }
                }
            }

            if (config.Zones != null)
            {
                foreach (var zone in config.Zones)
                {
                    if (zone.Rooms != null)
                    {
                        foreach (var room in zone.Rooms)
                        {
                            // Create room entity if needed, or just mobs in it.
                            // For now, the system seems to rely on coordinates.
                            // Maybe we should create a "Room" entity to hold description?
                            // The current system seems to attach descriptions to mobs/players, but maybe locations too?
                            // Looking at previous code:
                            // _worldManager.World.Create(
                            //    new Description { Short = "Shopkeeper", ... },
                            //    new LandPosition { ... },
                            //    new Shopkeeper(), ...
                            // );
                            // It seems entities are placed in rooms.
                            // Does the room itself exist as an entity?
                            // The previous code didn't explicitly create "Room" entities, just things IN them.
                            // But wait, how does the player see the room description?
                            // The `Look` command probably looks for an entity at the location with a Description?
                            // Or maybe the room description is just implied?
                            // Let's assume we create an entity representing the room itself if it has a description.
                            
                            if (!string.IsNullOrEmpty(room.Description) || !string.IsNullOrEmpty(room.LongDescription))
                            {
                                _worldManager.World.Create(
                                    new Description { Short = room.Description ?? "Room", Long = room.LongDescription ?? "A room." },
                                    new LandPosition { X = room.X, Y = room.Y, ZoneId = zone.Id ?? "Unknown" }
                                    // Tag it as a Room? Or just rely on it having a Position and Description but no other components?
                                );
                            }

                            if (room.Shopkeeper)
                            {
                                _worldManager.World.Create(
                                    new Description { Short = "Shopkeeper", Long = "A shopkeeper." },
                                    new LandPosition { X = room.X, Y = room.Y, ZoneId = zone.Id ?? "Unknown" },
                                    new Shopkeeper(),
                                    new Health { Current = 100, Max = 100 }
                                );
                            }

                            if (room.Mobs != null)
                            {
                                foreach (var mob in room.Mobs)
                                {
                                    var entity = _worldManager.World.Create(
                                        new Description { Short = mob.Name ?? "Mob", Long = mob.Description ?? "A mob." },
                                        new LandPosition { X = room.X, Y = room.Y, ZoneId = zone.Id ?? "Unknown" },
                                        new Health { Current = mob.Health, Max = mob.Health }
                                    );

                                    if (mob.Weapon != null)
                                    {
                                        _worldManager.World.Add(entity, new Weapon
                                        {
                                            Name = mob.Weapon.Name ?? "Weapon",
                                            Damage = mob.Weapon.Damage,
                                            Range = mob.Weapon.Range,
                                            CooldownMs = mob.Weapon.Cooldown
                                        });
                                    }

                                    if (mob.Aggressive)
                                    {
                                        _worldManager.World.Add(entity, new Aggressive());
                                    }

                                    if (mob.Drops != null)
                                    {
                                        foreach (var drop in mob.Drops)
                                        {
                                            CreateItem(drop, entity);
                                        }
                                    }

                                    if (mob.Quests != null)
                                    {
                                        _worldManager.World.Add(entity, new QuestGiver { QuestIds = mob.Quests });
                                    }
                                }
                            }

                            if (room.Items != null)
                            {
                                foreach (var item in room.Items)
                                {
                                    // For items in the room, we place them on the ground (LandPosition).
                                    // We also need to make sure they can be picked up.
                                    // Currently CommandParser.HandleLoot expects a container.
                                    // We might need to make the room a container, or fix CommandParser.
                                    // For now, let's just place them.
                                    CreateItem(item, Entity.Null, new LandPosition { X = room.X, Y = room.Y, ZoneId = zone.Id ?? "Unknown" });
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CreateItem(ItemConfig itemConfig, Entity container, LandPosition? pos = null)
        {
            var itemEntity = _worldManager.World.Create(
                new Description { Short = itemConfig.Name ?? "Item", Long = itemConfig.Description ?? "An item." },
                new Item { Value = itemConfig.Value, Weight = itemConfig.Weight }
            );

            if (pos.HasValue)
            {
                _worldManager.World.Add(itemEntity, pos.Value);
                // Items on the ground are not "ContainedBy" anything.
            }
            else if (container != Entity.Null)
            {
                _worldManager.World.Add(itemEntity, new ContainedBy { Container = container });
            }

            if (itemConfig.Weapon != null)
            {
                _worldManager.World.Add(itemEntity, new Weapon
                {
                    Name = itemConfig.Weapon.Name ?? "Weapon",
                    Damage = itemConfig.Weapon.Damage,
                    Range = itemConfig.Weapon.Range,
                    CooldownMs = itemConfig.Weapon.Cooldown
                });
            }

            if (itemConfig.Equippable != null)
            {
                if (Enum.TryParse<EquipmentSlot>(itemConfig.Equippable.Slot, true, out var slot))
                {
                    _worldManager.World.Add(itemEntity, new Equippable
                    {
                        Slot = slot,
                        ArmorBonus = itemConfig.Equippable.ArmorBonus
                    });
                }
            }
        }

        private void GenerateSpace()
        {
            // ...existing code...
            // Sector Alpha
            _worldManager.World.Create(
                new Planet { Name = "Earth", ZoneId = "Earth" },
                new SpacePosition { X = 0, Y = 0, Z = 0, SectorId = "Alpha" },
                new Description { Short = "Earth", Long = "The blue marble. Home of humanity." }
            );

            _worldManager.World.Create(
                new Planet { Name = "Mars", ZoneId = "Mars" },
                new SpacePosition { X = 50, Y = 0, Z = 0, SectorId = "Alpha" },
                new Description { Short = "Mars", Long = "The red planet. Dusty and cold." }
            );

            // Space Station
            _worldManager.World.Create(
                new Description { Short = "Deep Space 9", Long = "A massive space station orbiting a wormhole." },
                new SpacePosition { X = 100, Y = 100, Z = 0, SectorId = "Alpha" },
                new Container { Capacity = 1000 } // It's a container? Maybe just a place.
            );

            // Mobs in Space
            _worldManager.World.Create(
                new Ship { Name = "Pirate Raider", Hull = 50, MaxHull = 50, Shields = 20, MaxShields = 20 },
                new SpacePosition { X = 20, Y = 20, Z = 0, SectorId = "Alpha" },
                new Weapon { Name = "Laser Cannon", Damage = 5, Range = 50, CooldownMs = 2000 },
                new Aggressive(),
                new Description { Short = "Pirate Raider", Long = "A rusty pirate ship looking for trouble." }
            );
        }

        private void GeneratePlanets()
        {
            // ...existing code...
            // Earth
            _worldManager.World.Create(
                new Description { Short = "Shopkeeper", Long = "A friendly shopkeeper stands here." },
                new LandPosition { X = 0, Y = 0, ZoneId = "Earth" },
                new Shopkeeper(),
                new Health { Current = 100, Max = 100 }
            );

            _worldManager.World.Create(
                new Description { Short = "Rat", Long = "A large sewer rat." },
                new LandPosition { X = 0, Y = 1, ZoneId = "Earth" },
                new Health { Current = 20, Max = 20 },
                new Weapon { Name = "Teeth", Damage = 2, Range = 1, CooldownMs = 1000 },
                new Aggressive()
            );

            // Mars
            _worldManager.World.Create(
                new Description { Short = "Martian Rover", Long = "An old rover, malfunctioning and hostile." },
                new LandPosition { X = 5, Y = 5, ZoneId = "Mars" },
                new Health { Current = 50, Max = 50 },
                new Weapon { Name = "Drill", Damage = 8, Range = 2, CooldownMs = 1500 },
                new Aggressive()
            );
        }
    }
}
