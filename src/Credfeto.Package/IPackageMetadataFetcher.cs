using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace Credfeto.Package;

public interface IPackageMetadataFetcher
{
    Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(
        SourceRepository sourceRepository,
        string packageId,
        CancellationToken cancellationToken
    );
}
