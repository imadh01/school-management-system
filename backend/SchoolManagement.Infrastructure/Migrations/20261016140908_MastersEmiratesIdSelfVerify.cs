using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MastersEmiratesIdSelfVerify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_NoSelfVerify",
                table: "Documents");

            migrationBuilder.AddColumn<string>(
                name: "EmiratesIdNumber",
                table: "StudentIdentityDocuments",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdmissionFees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionFees", x => x.Id);
                    table.CheckConstraint("CK_AdmissionFees_AmountNotNegative", "[Amount] >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "UX_StudentIdentityDocuments_EmiratesId",
                table: "StudentIdentityDocuments",
                column: "EmiratesIdNumber",
                unique: true,
                filter: "[EmiratesIdNumber] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudentIdentityDocuments_EmiratesId",
                table: "StudentIdentityDocuments",
                sql: "[EmiratesIdNumber] IS NULL OR (LEN([EmiratesIdNumber]) = 15 AND [EmiratesIdNumber] NOT LIKE '%[^0-9]%' AND [EmiratesIdNumber] LIKE '784%')");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionFees_ClassName",
                table: "AdmissionFees",
                column: "ClassName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmissionFees");

            migrationBuilder.DropIndex(
                name: "UX_StudentIdentityDocuments_EmiratesId",
                table: "StudentIdentityDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StudentIdentityDocuments_EmiratesId",
                table: "StudentIdentityDocuments");

            migrationBuilder.DropColumn(
                name: "EmiratesIdNumber",
                table: "StudentIdentityDocuments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_NoSelfVerify",
                table: "Documents",
                sql: "[VerifiedBy] IS NULL OR [CreatedBy] IS NULL OR [VerifiedBy] <> [CreatedBy]");
        }
    }
}
