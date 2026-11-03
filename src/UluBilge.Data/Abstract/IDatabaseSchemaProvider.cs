using UluBilge.Core.Models;

namespace UluBilge.Data.Abstract
{
    public interface IDatabaseSchemaProvider
    {
        DatabaseSchemaSnapshot Snapshot { get; }
        Task InitializeAsync(CancellationToken ct = default);
    }
}
