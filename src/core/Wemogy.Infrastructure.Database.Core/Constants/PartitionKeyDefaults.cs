namespace Wemogy.Infrastructure.Database.Core.Constants;

public static class PartitionKeyDefaults
{
    /// <summary>
    ///     The value of the global partition key.
    /// </summary>
    public static string GlobalPartition { get; private set; } = "global";

    public static void CustomizeGlobalPartition(string value)
    {
        GlobalPartition = value;
    }
}
