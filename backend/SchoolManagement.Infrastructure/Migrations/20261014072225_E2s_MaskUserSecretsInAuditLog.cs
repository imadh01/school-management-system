using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <summary>
    /// E2 security fix (Problem 3): password hashes and security stamps were written to the audit log in
    /// plain text. From this release the code masks them ([AuditMasked] on User.PasswordHash and
    /// User.SecurityStamp). This migration overwrites the values ALREADY in AuditLogs with "***".
    /// Who changed what, and when, stays: only the old/new values of those two fields are replaced.
    ///
    /// No schema change (the model is unchanged; the attributes only affect what the interceptor writes).
    /// Back up the database first: this rewrite cannot be undone, and Down does nothing (by design: the
    /// whole point is that the hashes are gone).
    ///
    /// HAND-WRITTEN. The audit interceptor writes the field names as the C# property names
    /// ("PasswordHash"); the camelCase spellings are covered too, in case any row was written differently.
    /// </summary>
    public partial class E2s_MaskUserSecretsInAuditLog : Migration
    {
        private static readonly string[] Fields = { "PasswordHash", "SecurityStamp", "passwordHash", "securityStamp" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var statement in ScrubStatements())
                migrationBuilder.Sql(statement);
        }

        /// <summary>
        /// The UPDATE statements, one per field and side. Public so a test can run them against rows it
        /// wrote itself (the migration already ran when the test database was created).
        /// </summary>
        public static IEnumerable<string> ScrubStatements()
        {
            foreach (var field in Fields)
                foreach (var side in new[] { "old", "new" })
                {
                    var path = $"$.{field}.{side}";
                    yield return
                        $"UPDATE [AuditLogs] SET [Changes] = JSON_MODIFY([Changes], '{path}', N'***')\n" +
                        "WHERE [TableName] = N'Users' AND ISJSON([Changes]) = 1\n" +
                        $"  AND JSON_VALUE([Changes], '{path}') IS NOT NULL AND JSON_VALUE([Changes], '{path}') <> N'***';";
                }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo on purpose: the removed values must not come back. Restore a backup if needed.
        }
    }
}
