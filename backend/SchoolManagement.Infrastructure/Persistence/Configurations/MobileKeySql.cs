namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// The single definition of "same mobile number": spaces, dashes, dots, brackets and '+' removed.
/// Used by persisted computed columns on Parents and AdmissionGuardians so the two always
/// normalise identically, and the matching query can join them in SQL.
/// Deliberately exact-match only (no country-code or fuzzy logic): a missed match costs staff
/// one search, a wrong match links a child to the wrong family.
/// </summary>
internal static class MobileKeySql
{
    public static string Expression(string column) =>
        $"CAST(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE([{column}],' ',''),'-',''),'+',''),'(',''),')',''),'.','') AS nvarchar(20))";
}
