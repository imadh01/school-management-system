using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class D2_RefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecurityStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FamilyExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ReplacedByTokenId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedByIp = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.CheckConstraint("CK_RefreshTokens_Expiry", "[ExpiresAt] > [IssuedAt] AND [ExpiresAt] <= [FamilyExpiresAt]");
                    table.CheckConstraint("CK_RefreshTokens_Revoked", "([RevokedAt] IS NULL AND [RevokedReason] IS NULL) OR ([RevokedAt] IS NOT NULL AND [RevokedReason] IS NOT NULL)");
                    table.CheckConstraint("CK_RefreshTokens_RevokedReason", "[RevokedReason] IS NULL OR [RevokedReason] IN ('Rotated','Logout','LogoutAll','PasswordChanged','ReuseDetected','SessionInvalidated')");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_RefreshTokens_ReplacedByTokenId",
                        column: x => x.ReplacedByTokenId,
                        principalTable: "RefreshTokens",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyExpiresAt",
                table: "RefreshTokens",
                column: "FamilyExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId",
                table: "RefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ReplacedByTokenId",
                table: "RefreshTokens",
                column: "ReplacedByTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_Active",
                table: "RefreshTokens",
                column: "UserId",
                filter: "[RevokedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokens");
        }
    }
}
