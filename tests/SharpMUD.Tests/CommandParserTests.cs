using System;
using System.Threading.Tasks;
using Arch.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SharpMUD.Core;
using SharpMUD.Core.Components;
using SharpMUD.Data;
using SharpMUD.Game;
using Xunit;

namespace SharpMUD.Tests
{
    public class CommandParserTests
    {
        private readonly Mock<IConnection> _mockConnection;
        private readonly WorldManager _worldManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly SharpMUDContext _dbContext;

        public CommandParserTests()
        {
            _mockConnection = new Mock<IConnection>();
            _worldManager = new WorldManager();

            var services = new ServiceCollection();
            
            // Setup InMemory Database with a fixed name for this test instance
            var dbName = Guid.NewGuid().ToString();
            services.AddDbContext<SharpMUDContext>(options =>
                options.UseInMemoryDatabase(databaseName: dbName));

            _serviceProvider = services.BuildServiceProvider();
            _dbContext = _serviceProvider.GetRequiredService<SharpMUDContext>();
            _dbContext.Database.EnsureCreated();
        }

        [Fact]
        public async Task HandleLoginAsync_CreatesNewPlayer_AndAssignsSpacePosition()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            // Act
            await parser.ParseAsync(session, "login Kirk");

            // Assert
            Assert.Equal(SessionState.InGame, session.State);
            Assert.Equal("Kirk", session.Username);
            Assert.True(session.Entity.HasValue);

            // Verify player entity has SpacePosition and Ship components (default for new player)
            var playerEntity = session.Entity.Value;
            Assert.True(_worldManager.World.Has<SpacePosition>(playerEntity));
            Assert.True(_worldManager.World.Has<Ship>(playerEntity));
            
            // Verify messages sent
            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("Welcome, Kirk"))), Times.Once);
        }

        [Fact]
        public async Task HandleMove_UpdatesPosition()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            // Setup world with ship and player
            var shipEntity = _worldManager.World.Create(
                new Ship { Name = "USS Enterprise" },
                new SpacePosition { X = 0, Y = 0, Z = 0 }
            );

            var playerEntity = _worldManager.World.Create(
                new Player { Name = "Kirk" },
                new Controlling { Target = shipEntity }
            );

            session.State = SessionState.InGame;
            session.Entity = playerEntity;

            // Act
            await parser.ParseAsync(session, "move n");

            // Assert
            var pos = _worldManager.World.Get<SpacePosition>(shipEntity);
            Assert.Equal(0, pos.X);
            Assert.Equal(1, pos.Y);
            Assert.Equal(0, pos.Z);

            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("Moved n"))), Times.Once);
        }

        [Fact]
        public async Task HandleAttack_StartsCombat()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            // Attacker Ship
            var attackerShip = _worldManager.World.Create(
                new Ship { Name = "USS Enterprise" },
                new SpacePosition { X = 0, Y = 0, Z = 0, SectorId = "Alpha" },
                new Weapon { Name = "Phasers", Damage = 10, Range = 100, CooldownMs = 1000 }
            );

            var playerEntity = _worldManager.World.Create(
                new Player { Name = "Kirk" },
                new Controlling { Target = attackerShip }
            );

            // Target Ship
            var targetShip = _worldManager.World.Create(
                new Ship { Name = "Klingon Bird of Prey", Hull = 100, MaxHull = 100, Shields = 50, MaxShields = 50 },
                new SpacePosition { X = 10, Y = 0, Z = 0, SectorId = "Alpha" }
            );

            session.State = SessionState.InGame;
            session.Entity = playerEntity;

            // Act
            await parser.ParseAsync(session, "attack Klingon Bird of Prey");

            // Assert
            Assert.True(_worldManager.World.Has<CombatState>(attackerShip));
            var combatState = _worldManager.World.Get<CombatState>(attackerShip);
            Assert.Equal(targetShip, combatState.Target);

            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("Attacking Klingon Bird of Prey"))), Times.Once);
        }

        [Fact]
        public async Task HandleAttack_LandCombat_StartsCombat()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            // Attacker Player (Land)
            var playerEntity = _worldManager.World.Create(
                new Player { Name = "Kirk" },
                new LandPosition { X = 10, Y = 10, ZoneId = "PlanetX" },
                new Weapon { Name = "Phaser Rifle", Damage = 20, Range = 50, CooldownMs = 1000 }
            );

            // Target Mob
            var targetMob = _worldManager.World.Create(
                new Description { Short = "Alien Soldier" },
                new LandPosition { X = 12, Y = 12, ZoneId = "PlanetX" },
                new Health { Current = 100, Max = 100 }
            );

            session.State = SessionState.InGame;
            session.Entity = playerEntity;

            // Act
            await parser.ParseAsync(session, "attack Alien");

            // Assert
            Assert.True(_worldManager.World.Has<CombatState>(playerEntity));
            var combatState = _worldManager.World.Get<CombatState>(playerEntity);
            Assert.Equal(targetMob, combatState.Target);

            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("Attacking Alien"))), Times.Once);
        }

        [Fact]
        public async Task HandleLoot_LootCorpse_GetsItems()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            var playerEntity = _worldManager.World.Create(
                new Player { Name = "Kirk" },
                new LandPosition { X = 10, Y = 10, ZoneId = "PlanetX" }
            );

            var corpse = _worldManager.World.Create(
                new Description { Short = "Corpse of Alien" },
                new LandPosition { X = 10, Y = 10, ZoneId = "PlanetX" },
                new Container { Capacity = 10 }
            );

            var item = _worldManager.World.Create(
                new Description { Short = "Credits" },
                new Item { Value = 100 },
                new ContainedBy { Container = corpse }
            );

            session.State = SessionState.InGame;
            session.Entity = playerEntity;

            // Act
            await parser.ParseAsync(session, "loot Corpse");

            // Assert
            Assert.True(_worldManager.World.IsAlive(item)); // Item should NOT be destroyed
            var contained = _worldManager.World.Get<ContainedBy>(item);
            Assert.Equal(playerEntity, contained.Container); // Item should be in player inventory

            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("You loot Credits"))), Times.Once);
        }

        [Fact]
        public async Task HandleInventory_ListsItems()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            var playerEntity = _worldManager.World.Create(
                new Player { Name = "Kirk" }
            );

            var item = _worldManager.World.Create(
                new Description { Short = "Phaser" },
                new Item { Value = 500 },
                new ContainedBy { Container = playerEntity }
            );

            session.State = SessionState.InGame;
            session.Entity = playerEntity;

            // Act
            await parser.ParseAsync(session, "inventory");

            // Assert
            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("Phaser"))), Times.Once);
        }

        [Fact]
        public async Task HandleDrop_DropsItem()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            var playerEntity = _worldManager.World.Create(
                new Player { Name = "Kirk" },
                new LandPosition { X = 5, Y = 5, ZoneId = "PlanetX" }
            );

            var item = _worldManager.World.Create(
                new Description { Short = "Phaser" },
                new Item { Value = 500 },
                new ContainedBy { Container = playerEntity }
            );

            session.State = SessionState.InGame;
            session.Entity = playerEntity;

            // Act
            await parser.ParseAsync(session, "drop Phaser");

            // Assert
            Assert.False(_worldManager.World.Has<ContainedBy>(item));
            Assert.True(_worldManager.World.Has<LandPosition>(item));
            var pos = _worldManager.World.Get<LandPosition>(item);
            Assert.Equal(5, pos.X);
            Assert.Equal(5, pos.Y);

            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("You drop Phaser"))), Times.Once);
        }

        [Fact]
        public async Task HandleLook_ShowsCorpse()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            var playerEntity = _worldManager.World.Create(
                new Player { Name = "Kirk" },
                new LandPosition { X = 10, Y = 10, ZoneId = "PlanetX" }
            );

            var corpse = _worldManager.World.Create(
                new Description { Short = "Corpse of Alien", Long = "A dead alien lies here." },
                new LandPosition { X = 10, Y = 10, ZoneId = "PlanetX" }
            );

            session.State = SessionState.InGame;
            session.Entity = playerEntity;

            // Act
            await parser.ParseAsync(session, "look");

            // Assert
            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("A dead alien lies here."))), Times.Once);
        }

        [Fact]
        public async Task HandleLand_TransitionsToLand()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            var playerEntity = _worldManager.World.Create(
                new Player { Name = "Kirk" },
                new SpacePosition { X = 10, Y = 0, Z = 0, SectorId = "Alpha" },
                new Ship { Name = "Enterprise" }
            );

            var planet = _worldManager.World.Create(
                new Planet { Name = "Planet X", ZoneId = "PlanetX" },
                new SpacePosition { X = 10, Y = 0, Z = 0, SectorId = "Alpha" },
                new Description { Short = "Planet X" }
            );

            session.State = SessionState.InGame;
            session.Entity = playerEntity;

            // Act
            await parser.ParseAsync(session, "land Planet X");

            // Assert
            Assert.False(_worldManager.World.Has<SpacePosition>(playerEntity));
            Assert.False(_worldManager.World.Has<Ship>(playerEntity));
            Assert.True(_worldManager.World.Has<LandPosition>(playerEntity));
            
            var pos = _worldManager.World.Get<LandPosition>(playerEntity);
            Assert.Equal("PlanetX", pos.ZoneId);

            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("Landing on Planet X"))), Times.Once);
        }

        [Fact]
        public async Task HandleLaunch_TransitionsToSpace()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            var playerEntity = _worldManager.World.Create(
                new Player { Name = "Kirk" },
                new LandPosition { X = 0, Y = 0, ZoneId = "PlanetX" }
            );

            var planet = _worldManager.World.Create(
                new Planet { Name = "Planet X", ZoneId = "PlanetX" },
                new SpacePosition { X = 10, Y = 0, Z = 0, SectorId = "Alpha" }
            );

            session.State = SessionState.InGame;
            session.Entity = playerEntity;
            session.Username = "Kirk";

            // Act
            await parser.ParseAsync(session, "launch");

            // Assert
            Assert.False(_worldManager.World.Has<LandPosition>(playerEntity));
            Assert.True(_worldManager.World.Has<SpacePosition>(playerEntity));
            Assert.True(_worldManager.World.Has<Ship>(playerEntity));
            
            var pos = _worldManager.World.Get<SpacePosition>(playerEntity);
            Assert.Equal("Alpha", pos.SectorId);
            Assert.Equal(10, pos.X); // Should launch to planet's position

            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("Launching into space"))), Times.Once);
        }

        [Fact]
        public async Task HandleQuit_SavesPlayerState()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            // Create initial player in DB using a scope
            var playerAccount = new Data.Models.PlayerAccount 
            { 
                Username = "Kirk", 
                X = 0, Y = 0, 
                LocationId = "Alpha", 
                IsSpace = true 
            };

            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SharpMUDContext>();
                db.Players.Add(playerAccount);
                await db.SaveChangesAsync();
            }

            session.AccountId = playerAccount.Id;
            session.Username = "Kirk";
            session.State = SessionState.InGame;

            var playerEntity = _worldManager.World.Create(
                new Player { Name = "Kirk" },
                new SpacePosition { X = 100, Y = 50, Z = 25, SectorId = "Beta" },
                new Health { Current = 80, Max = 100 }
            );
            session.Entity = playerEntity;

            // Act
            await parser.ParseAsync(session, "quit");

            // Assert
            // Use the main context to verify
            var savedPlayer = await _dbContext.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playerAccount.Id);
            
            Assert.True(playerAccount.Id > 0, $"Player ID should be > 0, but was {playerAccount.Id}");
            Assert.NotNull(savedPlayer);
            Assert.Equal(100, savedPlayer.X);
            Assert.Equal(50, savedPlayer.Y);
            Assert.Equal(25, savedPlayer.Z);
            Assert.Equal("Beta", savedPlayer.LocationId);
            Assert.Equal(80, savedPlayer.CurrentHealth);
        }

        [Fact]
        public async Task HandleScore_ReturnsStats()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            var playerEntity = _worldManager.World.Create(
                new Player { Name = "Kirk" },
                new Health { Current = 90, Max = 100 },
                new Experience { Value = 500, Level = 2 },
                new Money { Amount = 1000 }
            );

            session.State = SessionState.InGame;
            session.Entity = playerEntity;
            session.Username = "Kirk";

            // Act
            await parser.ParseAsync(session, "score");

            // Assert
            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("Name: Kirk"))), Times.Once);
            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("Health: 90/100"))), Times.Once);
            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("Level: 2"))), Times.Once);
            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("XP: 500"))), Times.Once);
            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("Money: 1000 Credits"))), Times.Once);
        }

        [Fact]
        public async Task HandleBuy_BuysItem_WhenShopkeeperPresent()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            var playerEntity = _worldManager.World.Create(
                new Player { Name = "Kirk" },
                new LandPosition { X = 0, Y = 0, ZoneId = "Earth" },
                new Money { Amount = 100 }
            );

            var shopkeeper = _worldManager.World.Create(
                new Shopkeeper(),
                new LandPosition { X = 0, Y = 0, ZoneId = "Earth" }
            );

            session.State = SessionState.InGame;
            session.Entity = playerEntity;

            // Act
            await parser.ParseAsync(session, "buy Health Potion");

            // Assert
            var money = _worldManager.World.Get<Money>(playerEntity);
            Assert.Equal(90, money.Amount); // Cost is 10

            // Check inventory
            var query = new QueryDescription().WithAll<Item, ContainedBy, Description>();
            bool found = false;
            _worldManager.World.Query(in query, (Entity entity, ref Description desc, ref ContainedBy contained) => 
            {
                if (contained.Container == playerEntity && desc.Short == "Health Potion")
                {
                    found = true;
                }
            });
            Assert.True(found);

            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("You bought a Health Potion"))), Times.Once);
        }

        [Fact]
        public async Task HandleSell_SellsItem_WhenShopkeeperPresent()
        {
            // Arrange
            var parser = new CommandParser(_serviceProvider, _worldManager);
            var session = new PlayerSession(_mockConnection.Object);
            
            var playerEntity = _worldManager.World.Create(
                new Player { Name = "Kirk" },
                new LandPosition { X = 0, Y = 0, ZoneId = "Earth" },
                new Money { Amount = 100 }
            );

            var item = _worldManager.World.Create(
                new Item { Value = 50 },
                new Description { Short = "Gem" },
                new ContainedBy { Container = playerEntity }
            );

            var shopkeeper = _worldManager.World.Create(
                new Shopkeeper(),
                new LandPosition { X = 0, Y = 0, ZoneId = "Earth" }
            );

            session.State = SessionState.InGame;
            session.Entity = playerEntity;

            // Act
            await parser.ParseAsync(session, "sell Gem");

            // Assert
            var money = _worldManager.World.Get<Money>(playerEntity);
            Assert.Equal(150, money.Amount); // 100 + 50

            Assert.False(_worldManager.World.IsAlive(item));

            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("You sold Gem"))), Times.Once);
        }
    }
}
