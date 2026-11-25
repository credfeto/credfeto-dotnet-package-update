using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Package.Update;

[DebuggerDisplay("{PackageId} in {Folder}")]
public sealed record Options(
    string PackageId,
    string Folder,
    string? Cache,
    IReadOnlyList<string> Source,
    IReadOnlyList<string> Exclude
);
