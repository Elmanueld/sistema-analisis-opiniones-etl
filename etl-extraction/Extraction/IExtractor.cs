using System.Runtime.CompilerServices;
using Opiniones.ExtractionWorker.Models;

namespace Opiniones.ExtractionWorker.Extraction;

public interface IExtractor
{
    string SourceName { get; }
    bool IsEnabled { get; }
    IAsyncEnumerable<ExtractedOpinion> ExtractAsync(CancellationToken cancellationToken);
}

public abstract class ExtractorBase : IExtractor
{
    public abstract string SourceName { get; }
    public abstract bool IsEnabled { get; }
    public abstract IAsyncEnumerable<ExtractedOpinion> ExtractAsync(CancellationToken cancellationToken);

    protected static async IAsyncEnumerable<T> EnumerateAsync<T>(IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in source.WithCancellation(cancellationToken))
            yield return item;
    }
}
