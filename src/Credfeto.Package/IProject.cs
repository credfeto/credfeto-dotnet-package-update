using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Package;

public interface IProject
{
    string FileName { get; }

    IReadOnlyList<PackageVersion> Packages { get; }

    bool Changed { get; }

    bool UpdatePackage(PackageVersion package);

    ValueTask<bool> SaveAsync(CancellationToken cancellationToken);
}
