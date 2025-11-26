using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SharpMUD.Core;
using SharpMUD.Data;
using SharpMUD.Game;
using SharpMUD.Network;
using SharpMUD.Server;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog();

    // Data Services
    builder.Services.AddDbContext<SharpMUDContext>(options =>
        options.UseSqlite("Data Source=sharpmud.db"));

    // Core Services
    builder.Services.AddSingleton<CommandQueue>();

    // Game Services
    builder.Services.AddSingleton<WorldManager>();
    builder.Services.AddSingleton<SessionManager>();
    builder.Services.AddSingleton<CommandParser>();
    builder.Services.AddHostedService<GameLoopService>();

    // Network Services
    builder.Services.AddSingleton<INetworkServer, TelnetServer>();
    builder.Services.AddHostedService<NetworkService>();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
