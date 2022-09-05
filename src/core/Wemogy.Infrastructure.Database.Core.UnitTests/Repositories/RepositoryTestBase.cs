using System;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public abstract partial class RepositoryTestBase : IDisposable
{
    protected IUserRepository UserRepository { get; }
    protected Func<IUserRepository> UserRepositoryFactory { get; }

    protected RepositoryTestBase(Func<IUserRepository> userRepositoryFactory)
    {
        UserRepository = userRepositoryFactory();
        UserRepositoryFactory = userRepositoryFactory;
        RepositoryFactoryFactory.DatabaseClientProxy = null;
    }

    protected async Task ResetAsync()
    {
        await UserRepository.DeleteAsync(x => true);
    }

    [Fact]
    public async Task DeleteAsync_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        await UserRepository.CreateAsync(User.Faker.Generate());

        // Act
        await UserRepository.DeleteAsync(x => true);

        // Assert
        var entities = await UserRepository.QueryAsync(x => true);
        entities.Should().BeEmpty();
    }

    public void Dispose()
    {
        // Cleanup
        UserRepository.DeleteAsync(x => true).Wait();
    }
}
