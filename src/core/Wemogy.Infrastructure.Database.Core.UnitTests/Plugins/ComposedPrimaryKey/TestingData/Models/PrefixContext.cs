namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.TestingData.Models;

public class PrefixContext
{
    public string Prefix { get; set; }

    public PrefixContext(string prefix)
    {
        Prefix = prefix;
    }
}
