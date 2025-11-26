using System;
using System.Threading.Tasks;
using Arch.Core;
using Moq;
using SharpMUD.Core;
using SharpMUD.Core.Components;
using SharpMUD.Game;
using SharpMUD.Game.Systems;
using Xunit;

namespace SharpMUD.Tests
{
    public class MobAISystemTests
    {
        private readonly World _world;
        private readonly SessionManager _sessionManager;
        private readonly Mock<IConnection> _mockConnection;
        private readonly MobAISystem _mobAISystem;

        public MobAISystemTests()
        {
            _world = World.Create();
            _sessionManager = new SessionManager();
            _mockConnection = new Mock<IConnection>();
            _mockConnection.Setup(c => c.ConnectionId).Returns("test-connection");
            _mobAISystem = new MobAISystem(_world, _sessionManager);
        }

        [Fact]
        public async Task Update_ShouldMakeAggressiveMobAttackPlayer_WhenInRange()
        {
            // Arrange
            var player = _world.Create(
                new Player { Name = "Kirk", ConnectionId = "test-connection" },
                new LandPosition { X = 10, Y = 10, ZoneId = "PlanetX" }
            );
            _sessionManager.CreateSession(_mockConnection.Object);

            var mob = _world.Create(
                new Description { Short = "Angry Alien" },
                new LandPosition { X = 12, Y = 12, ZoneId = "PlanetX" },
                new Weapon { Name = "Claws", Damage = 5, Range = 10, CooldownMs = 1000 },
                new Aggressive()
            );

            // Act
            await _mobAISystem.Update(0.1);

            // Assert
            Assert.True(_world.Has<CombatState>(mob));
            var combatState = _world.Get<CombatState>(mob);
            Assert.Equal(player, combatState.Target);

            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("Angry Alien screams and attacks you"))), Times.Once);
        }

        [Fact]
        public async Task Update_ShouldNotAttack_WhenOutOfRange()
        {
            // Arrange
            var player = _world.Create(
                new Player { Name = "Kirk" },
                new LandPosition { X = 100, Y = 100, ZoneId = "PlanetX" }
            );

            var mob = _world.Create(
                new Description { Short = "Angry Alien" },
                new LandPosition { X = 12, Y = 12, ZoneId = "PlanetX" },
                new Weapon { Name = "Claws", Damage = 5, Range = 10, CooldownMs = 1000 },
                new Aggressive()
            );

            // Act
            await _mobAISystem.Update(0.1);

            // Assert
            Assert.False(_world.Has<CombatState>(mob));
        }

        [Fact]
        public async Task Update_ShouldNotAttack_WhenNotAggressive()
        {
            // Arrange
            var player = _world.Create(
                new Player { Name = "Kirk" },
                new LandPosition { X = 12, Y = 12, ZoneId = "PlanetX" }
            );

            var mob = _world.Create(
                new Description { Short = "Peaceful Alien" },
                new LandPosition { X = 12, Y = 12, ZoneId = "PlanetX" },
                new Weapon { Name = "Claws", Damage = 5, Range = 10, CooldownMs = 1000 }
                // No Aggressive component
            );

            // Act
            await _mobAISystem.Update(0.1);

            // Assert
            Assert.False(_world.Has<CombatState>(mob));
        }
    }
}
