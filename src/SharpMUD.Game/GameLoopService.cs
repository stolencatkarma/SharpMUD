using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpMUD.Core;

using SharpMUD.Core.Components;
using SharpMUD.Game.Systems;

namespace SharpMUD.Game
{
    public class GameLoopService : BackgroundService
    {
        private readonly ILogger<GameLoopService> _logger;
        private readonly CommandQueue _commandQueue;
        private readonly WorldManager _worldManager;
        private readonly CommandParser _commandParser;
        private readonly SessionManager _sessionManager;
        private const int TickRate = 20; // Ticks per second
        private readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(1000.0 / TickRate);
        private DateTime _lastAutosave = DateTime.UtcNow;
        private readonly TimeSpan _autosaveInterval = TimeSpan.FromMinutes(5);

        public GameLoopService(ILogger<GameLoopService> logger, CommandQueue commandQueue, WorldManager worldManager, CommandParser commandParser, SessionManager sessionManager)
        {
            _logger = logger;
            _commandQueue = commandQueue;
            _worldManager = worldManager;
            _commandParser = commandParser;
            _sessionManager = sessionManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Game Loop started.");

            // Initialize Systems
            // var spaceMovementSystem = new SpaceMovementSystem(_worldManager.World);
            var questSystem = new QuestSystem(_worldManager, _sessionManager);
            var combatSystem = new CombatSystem(_worldManager.World, _sessionManager, questSystem);
            var mobAISystem = new MobAISystem(_worldManager.World, _sessionManager);
            var worldGenerator = new WorldGenerator(_worldManager);

            // Generate World
            worldGenerator.Generate();
            _logger.LogInformation("World Generated.");

            long tickCount = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                var startTime = DateTime.UtcNow;
                tickCount++;

                // 1. Process Input
                while (_commandQueue.TryDequeue(out var item))
                {
                    var session = _sessionManager.GetSession(item.Connection.ConnectionId);
                    if (session != null)
                    {
                        await _commandParser.ParseAsync(session, item.Command);
                    }
                }

                // 2. Run Systems
                // spaceMovementSystem.Update(_tickInterval.TotalSeconds);
                await mobAISystem.Update(_tickInterval.TotalSeconds);
                await combatSystem.Update(_tickInterval.TotalSeconds);

                // Autosave
                if (DateTime.UtcNow - _lastAutosave > _autosaveInterval)
                {
                    _logger.LogInformation("Autosaving players...");
                    foreach (var session in _sessionManager.GetAllSessions())
                    {
                        if (session.State == SessionState.InGame)
                        {
                            await _commandParser.SavePlayerState(session);
                        }
                    }
                    _lastAutosave = DateTime.UtcNow;
                }

                // Log position every 100 ticks
                if (tickCount % 100 == 0)
                {
                    // _logger.LogInformation("Tick {Tick}", tickCount);
                }

                // 3. Wait for next tick
                var elapsed = DateTime.UtcNow - startTime;
                var delay = _tickInterval - elapsed;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, stoppingToken);
                }
                else
                {
                    _logger.LogWarning("Game loop running slow! Tick took {Elapsed}ms", elapsed.TotalMilliseconds);
                }
            }

            _logger.LogInformation("Game Loop stopped.");
        }
    }
}
