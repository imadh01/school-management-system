namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Builds the SQL for CHECK constraints from the same lists the validators use.</summary>
internal static class CheckSql
{
    /// <summary>[Status] IN ('Active','Inactive',...)</summary>
    public static string In(string column, IEnumerable<string> values) =>
        $"[{column}] IN ({string.Join(",", values.Select(v => $"'{v.Replace("'", "''")}'"))})";
}
