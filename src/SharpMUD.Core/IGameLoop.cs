using System.Threading;
using System.Threading.Tasks;

namespace SharpMUD.Core
{
    public interface IGameLoop
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }
}
