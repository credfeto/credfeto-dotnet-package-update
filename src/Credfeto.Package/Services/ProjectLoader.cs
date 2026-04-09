using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.Package.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using NonBlocking;

namespace Credfeto.Package.Services;

public sealed class ProjectLoader : IProjectLoader
{
    private readonly ConcurrentDictionary<string, IProject> _loadedProjects;
    private readonly ILogger<ProjectLoader> _logger;

    public ProjectLoader(ILogger<ProjectLoader> logger)
    {
        this._logger = logger;
        this._loadedProjects = new(StringComparer.Ordinal);
    }

    public async ValueTask<IProject?> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (this._loadedProjects.TryGetValue(key: path, out IProject? project))
        {
            return project;
        }

        try
        {
            string content = await File.ReadAllTextAsync(
                path: path,
                encoding: Encoding.UTF8,
                cancellationToken: cancellationToken
            );
            XmlDocument doc = new();

            doc.LoadXml(content);

            project = new Project(fileName: path, doc: doc);

            return this._loadedProjects.GetOrAdd(key: path, value: project);
        }
        catch (Exception exception)
        {
            this._logger.FailedToLoad(fileName: path, message: exception.Message, exception: exception);

            return null;
        }
    }

    public void Reset()
    {
        this._loadedProjects.Clear();
    }
}
