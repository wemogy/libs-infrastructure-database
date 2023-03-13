using System;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public abstract partial class RepositoryTestBase
{
    protected RepositoryTestBase(Func<IDatabaseRepository<User>> userRepositoryFactory, Func<IDatabaseRepository<DataCenter>> dataCenterRepositoryFactory)
    {
        MicrosoftUserRepository = userRepositoryFactory();
        UserRepositoryFactory = userRepositoryFactory;
        DataCenterRepository = dataCenterRepositoryFactory();
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = null;
    }

    protected IDatabaseRepository<User> MicrosoftUserRepository { get; set; }
    protected IDatabaseRepository<DataCenter> DataCenterRepository { get; set; }
    private Func<IDatabaseRepository<User>> UserRepositoryFactory { get; }

    protected virtual async Task ResetAsync()
    {
        await MicrosoftUserRepository.DeleteAsync(x => true);
        await DataCenterRepository.DeleteAsync(x => true);
    }
}
