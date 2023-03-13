using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories
{
    public interface IDataCenterRepository : IDatabaseRepository<DataCenter>
    {
    }
}
