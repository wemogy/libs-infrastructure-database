using System;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public abstract partial class RepositoryTestBase : IDisposable
{
    protected RepositoryTestBase(Func<IDatabaseRepository<User>> userRepositoryFactory)
    {
        MicrosoftUserRepository = userRepositoryFactory();
        UserRepositoryFactory = userRepositoryFactory;
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = null;
    }

    protected IDatabaseRepository<User> MicrosoftUserRepository { get; set; }
    private Func<IDatabaseRepository<User>> UserRepositoryFactory { get; }

    public virtual void Dispose()
    {
        // Cleanup
        MicrosoftUserRepository.DeleteAsync(x => true).Wait();
    }

    protected virtual async Task ResetAsync()
    {
        await MicrosoftUserRepository.DeleteAsync(x => true);
    }
}
