using System;
using System.Threading.Tasks;
using Arch.Core;
using Moq;
using SharpMUD.Core;
using SharpMUD.Core.Components;
using SharpMUD.Game;
using SharpMUD.Game.Systems;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SharpMUD.Tests
{
    public class CombatSystemTests
    {
        private readonly World _world;
        private readonly SessionManager _sessionManager;
        private readonly Mock<IConnection> _mockConnection;
        private readonly CombatSystem _combatSystem;

        public CombatSystemTests()
        {
            _world = World.Create();
            _sessionManager = new SessionManager();
            _mockConnection = new Mock<IConnection>();
            _mockConnection.Setup(c => c.ConnectionId).Returns("test-connection");
            _combatSystem = new CombatSystem(_world, _sessionManager);
        }

        [Fact]
        public async Task Update_ShouldApplyDamage_WhenCooldownReady()
        {
            // Arrange
            var target = _world.Create(
                new Ship { Name = "Target Ship", Hull = 100, MaxHull = 100 },
                new SpacePosition { X = 10, Y = 0, Z = 0, SectorId = "Alpha" }
            );

            var attacker = _world.Create(
                new Ship { Name = "Attacker Ship", Hull = 100, MaxHull = 100 },
                new SpacePosition { X = 0, Y = 0, Z = 0, SectorId = "Alpha" },
                new Weapon { Name = "Laser", Damage = 10, Range = 100, CooldownMs = 1000, LastFired = DateTime.MinValue },
                new CombatState { Target = target, NextAttackTime = DateTime.MinValue }
            );

            // Act
            await _combatSystem.Update(0.1);

            // Assert
            var targetShip = _world.Get<Ship>(target);
            Assert.Equal(90, targetShip.Hull);
        }

        [Fact]
        public async Task Update_ShouldNotApplyDamage_WhenCooldownNotReady()
        {
            // Arrange
            var target = _world.Create(
                new Ship { Name = "Target Ship", Hull = 100, MaxHull = 100 },
                new SpacePosition { X = 10, Y = 0, Z = 0, SectorId = "Alpha" }
            );

            var attacker = _world.Create(
                new Ship { Name = "Attacker Ship", Hull = 100, MaxHull = 100 },
                new SpacePosition { X = 0, Y = 0, Z = 0, SectorId = "Alpha" },
                new Weapon { Name = "Laser", Damage = 10, Range = 100, CooldownMs = 1000, LastFired = DateTime.UtcNow }, // Just fired
                new CombatState { Target = target, NextAttackTime = DateTime.UtcNow.AddSeconds(1) }
            );

            // Act
            await _combatSystem.Update(0.1);

            // Assert
            var targetShip = _world.Get<Ship>(target);
            Assert.Equal(100, targetShip.Hull);
        }

        [Fact]
        public async Task Update_ShouldStopCombat_WhenTargetOutOfRange()
        {
            // Arrange
            var target = _world.Create(
                new Ship { Name = "Target Ship", Hull = 100, MaxHull = 100 },
                new SpacePosition { X = 200, Y = 0, Z = 0, SectorId = "Alpha" } // Out of range (100)
            );

            var attacker = _world.Create(
                new Ship { Name = "Attacker Ship", Hull = 100, MaxHull = 100 },
                new SpacePosition { X = 0, Y = 0, Z = 0, SectorId = "Alpha" },
                new Weapon { Name = "Laser", Damage = 10, Range = 100, CooldownMs = 1000 },
                new CombatState { Target = target, NextAttackTime = DateTime.MinValue }
            );

            // Act
            await _combatSystem.Update(0.1);

            // Assert
            Assert.False(_world.Has<CombatState>(attacker));
        }

        [Fact]
        public async Task Update_ShouldTriggerRetaliation_WhenTargetAttacked()
        {
            // Arrange
            var target = _world.Create(
                new Ship { Name = "Target Ship", Hull = 100, MaxHull = 100 },
                new SpacePosition { X = 10, Y = 0, Z = 0, SectorId = "Alpha" },
                new Weapon { Name = "Phasers", Damage = 10, Range = 100, CooldownMs = 1000 }
            );

            var attacker = _world.Create(
                new Ship { Name = "Attacker Ship", Hull = 100, MaxHull = 100 },
                new SpacePosition { X = 0, Y = 0, Z = 0, SectorId = "Alpha" },
                new Weapon { Name = "Laser", Damage = 10, Range = 100, CooldownMs = 1000, LastFired = DateTime.MinValue },
                new CombatState { Target = target, NextAttackTime = DateTime.MinValue }
            );

            // Act
            await _combatSystem.Update(0.1);

            // Assert
            Assert.True(_world.Has<CombatState>(target));
            var targetCombat = _world.Get<CombatState>(target);
            Assert.Equal(attacker, targetCombat.Target);
        }

        [Fact]
        public async Task Update_ShouldRespawnPlayer_WhenKilled()
        {
            // Arrange
            var playerShip = _world.Create(
                new Ship { Name = "Player Ship", Hull = 10, MaxHull = 100 },
                new SpacePosition { X = 10, Y = 0, Z = 0, SectorId = "Alpha" }
            );
            
            var playerEntity = _world.Create(new Player { Name = "Kirk", ConnectionId = "test-connection" }, new Controlling { Target = playerShip });
            
            var attacker = _world.Create(
                new Ship { Name = "Enemy", Hull = 100, MaxHull = 100 },
                new SpacePosition { X = 10, Y = 0, Z = 0, SectorId = "Alpha" },
                new Weapon { Name = "Laser", Damage = 20, Range = 100, CooldownMs = 1000 },
                new CombatState { Target = playerShip, NextAttackTime = DateTime.MinValue }
            );

            // Act
            await _combatSystem.Update(0.1);

            // Assert
            // Player ship should NOT be destroyed
            Assert.True(_world.IsAlive(playerShip));
            
            // Player ship should be at 0,0,0
            var pos = _world.Get<SpacePosition>(playerShip);
            Assert.Equal(0, pos.X);
            
            // Player ship should have full health
            var ship = _world.Get<Ship>(playerShip);
            Assert.Equal(100, ship.Hull);

            // Attacker should stop fighting
            Assert.False(_world.Has<CombatState>(attacker));
        }

        [Fact]
        public async Task Update_ShouldDestroyMob_WhenKilled()
        {
            // Arrange
            var mob = _world.Create(
                new Ship { Name = "Mob Ship", Hull = 10, MaxHull = 100 },
                new SpacePosition { X = 10, Y = 0, Z = 0, SectorId = "Alpha" }
            );

            var attacker = _world.Create(
                new Ship { Name = "Player", Hull = 100, MaxHull = 100 },
                new SpacePosition { X = 10, Y = 0, Z = 0, SectorId = "Alpha" },
                new Weapon { Name = "Laser", Damage = 20, Range = 100, CooldownMs = 1000 },
                new CombatState { Target = mob, NextAttackTime = DateTime.MinValue }
            );

            // Act
            await _combatSystem.Update(0.1);

            // Assert
            Assert.False(_world.IsAlive(mob));
            Assert.False(_world.Has<CombatState>(attacker));
        }

        [Fact]
        public async Task Update_ShouldAwardXP_OnKill()
        {
            // Arrange
            var target = _world.Create(
                new Description { Short = "Rat" },
                new Health { Current = 5, Max = 10 },
                new LandPosition { X = 0, Y = 0, ZoneId = "Earth" },
                new Experience { Value = 0, Level = 1 }
            );

            var attacker = _world.Create(
                new Player { Name = "Hero", ConnectionId = "test-connection" },
                new Health { Current = 100, Max = 100 },
                new LandPosition { X = 0, Y = 0, ZoneId = "Earth" },
                new Weapon { Name = "Sword", Damage = 10, Range = 1, CooldownMs = 1000 },
                new CombatState { Target = target, NextAttackTime = DateTime.MinValue },
                new Experience { Value = 0, Level = 1 }
            );

            var session = _sessionManager.CreateSession(_mockConnection.Object);
            session.Entity = attacker;

            // Act
            await _combatSystem.Update(0.1);

            // Assert
            Assert.False(_world.IsAlive(target)); // Should be dead
            
            var xp = _world.Get<Experience>(attacker);
            Assert.Equal(150, xp.Value); // 100 Base + 50 * Level 1

            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("You gain 150 XP"))), Times.Once);
        }

        [Fact]
        public async Task Update_ShouldLevelUp_WhenXPThresholdReached()
        {
            // Arrange
            var target = _world.Create(
                new Description { Short = "Dragon" },
                new Health { Current = 5, Max = 10 },
                new LandPosition { X = 0, Y = 0, ZoneId = "Earth" },
                new Experience { Value = 0, Level = 10 }
            );

            var attacker = _world.Create(
                new Player { Name = "Hero", ConnectionId = "test-connection" },
                new Health { Current = 100, Max = 100 },
                new LandPosition { X = 0, Y = 0, ZoneId = "Earth" },
                new Weapon { Name = "Sword", Damage = 10, Range = 1, CooldownMs = 1000 },
                new CombatState { Target = target, NextAttackTime = DateTime.MinValue },
                new Experience { Value = 900, Level = 1 } // Needs 1000 for Level 2
            );

            var session = _sessionManager.CreateSession(_mockConnection.Object);
            session.Entity = attacker;

            // Act
            await _combatSystem.Update(0.1);

            // Assert
            var xp = _world.Get<Experience>(attacker);
            Assert.Equal(2, xp.Level); // Should level up
            Assert.True(xp.Value >= 1000);

            var health = _world.Get<Health>(attacker);
            Assert.Equal(110, health.Max); // +10 Max Health
            Assert.Equal(110, health.Current); // Full heal

            _mockConnection.Verify(c => c.SendAsync(It.Is<string>(s => s.Contains("LEVEL UP"))), Times.Once);
        }
    }
}
