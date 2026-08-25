using System;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public abstract partial class RepositoryTestBase
{
    protected RepositoryTestBase(
        Func<IDatabaseRepository<User>> userRepositoryFactory,
        Func<IDatabaseRepository<User>> filteredUserRepositoryFactory,
        Func<IDatabaseRepository<DataCenter>> dataCenterRepositoryFactory)
    {
        // cleared before the repositories are built, not after: the retry tests install a flaky
        // proxy and leave it installed, and the factories below bake whatever proxy is set into
        // the repositories they return. Clearing it afterwards left the next test running against
        // the previous test's fault injection, which only stayed hidden while no test that
        // replaces an entity happened to run right after one of them
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = null;

        MicrosoftUserRepository = userRepositoryFactory();
        FilteredUserRepository = filteredUserRepositoryFactory();
        UserRepositoryFactory = userRepositoryFactory;
        DataCenterRepository = dataCenterRepositoryFactory();
    }

    protected IDatabaseRepository<User> MicrosoftUserRepository { get; set; }
    protected IDatabaseRepository<User> FilteredUserRepository { get; set; }
    protected IDatabaseRepository<DataCenter> DataCenterRepository { get; set; }
    private Func<IDatabaseRepository<User>> UserRepositoryFactory { get; }

    /// <summary>
    ///     The repository the change feed tests write to and read the feed of. Defaults to the one
    ///     the rest of the suite uses, which is all the in-memory provider needs: its change log
    ///     starts at the end of the feed and is trimmed to what a processor still has to read, so a
    ///     long write history costs a processor nothing.
    ///     <para>
    ///         A provider whose processor has to read its way through that history - Cosmos DB does -
    ///         overrides this with a repository over a collection of its own.
    ///     </para>
    /// </summary>
    protected virtual IDatabaseRepository<User> ChangeFeedUserRepository => MicrosoftUserRepository;

    protected virtual async Task ResetAsync()
    {
        await MicrosoftUserRepository.DeleteAsync(x => true);
        await DataCenterRepository.DeleteAsync(x => true);
    }
}
