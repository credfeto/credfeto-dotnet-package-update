using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Package;

public interface IProjectLoader
{
    ValueTask<IProject?> LoadAsync(string path, CancellationToken cancellationToken);

    void Reset();
}


