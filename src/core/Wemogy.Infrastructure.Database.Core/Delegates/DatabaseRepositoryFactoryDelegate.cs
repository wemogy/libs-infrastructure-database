using System;

namespace Wemogy.Infrastructure.Database.Core.Delegates;

public delegate TDatabaseRepository DatabaseRepositoryFactoryDelegate<out TDatabaseRepository>(
    IServiceProvider serviceProvider);
