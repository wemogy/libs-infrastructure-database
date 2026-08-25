using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Delegates;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    public IChangeFeedProcessor CreateChangeFeedProcessor(
        string processorName,
        ChangeFeedHandler<TEntity> onChanges,
        ChangeFeedProcessorOptions? options = null)
    {
        // deliberately unfiltered: the read filters and the soft delete filter shape what a *query*
        // returns, and applying them here would drop the very changes a projection needs to react
        // to - a document that a filter hides is still a document that changed
        return _database.CreateChangeFeedProcessor(
            processorName,
            onChanges,
            options);
    }

    public IChangeFeedProcessor CreateAllVersionsAndDeletesChangeFeedProcessor(
        string processorName,
        AllVersionsAndDeletesChangeFeedHandler<TEntity> onChanges,
        ChangeFeedProcessorOptions? options = null)
    {
        return _database.CreateAllVersionsAndDeletesChangeFeedProcessor(
            processorName,
            onChanges,
            options);
    }
}
