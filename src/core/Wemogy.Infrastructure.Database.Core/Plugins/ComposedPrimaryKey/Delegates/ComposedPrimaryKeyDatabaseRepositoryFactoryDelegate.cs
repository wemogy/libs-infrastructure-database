using System;

namespace Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Delegates;

public delegate TDatabaseRepository ComposedPrimaryKeyDatabaseRepositoryFactoryDelegate<out TDatabaseRepository>(IServiceProvider serviceProvider);
