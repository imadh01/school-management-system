using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ContractStudentAdmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AadhaarNumber",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Allergies",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AuthorizedPickupPersons",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "BloodGroup",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "DietaryRequirements",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FatherMobile",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FatherName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FatherOccupation",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "GuardianMobile",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "GuardianName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "GuardianRelation",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "InsurancePolicyExpiry",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "InsuranceProvider",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MedicalNotes",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MotherMobile",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MotherName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MotherOccupation",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PassportExpiry",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PassportNumber",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PrimaryContactParent",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "SpecialEducationalNeeds",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "VisaExpiry",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "VisaType",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FatherMobile",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "FatherName",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "Grade",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "GuardianMobile",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "GuardianName",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "GuardianRelation",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "MotherMobile",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "MotherName",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "RegistrationFee",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "Admissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AadhaarNumber",
                table: "Students",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Allergies",
                table: "Students",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorizedPickupPersons",
                table: "Students",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodGroup",
                table: "Students",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DietaryRequirements",
                table: "Students",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherMobile",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherName",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherOccupation",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianMobile",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianName",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianRelation",
                table: "Students",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InsurancePolicyExpiry",
                table: "Students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceProvider",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedicalNotes",
                table: "Students",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherMobile",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherName",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherOccupation",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PassportExpiry",
                table: "Students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassportNumber",
                table: "Students",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryContactParent",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecialEducationalNeeds",
                table: "Students",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "VisaExpiry",
                table: "Students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisaType",
                table: "Students",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherMobile",
                table: "Admissions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherName",
                table: "Admissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Grade",
                table: "Admissions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianMobile",
                table: "Admissions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianName",
                table: "Admissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianRelation",
                table: "Admissions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherMobile",
                table: "Admissions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherName",
                table: "Admissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Admissions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RegistrationFee",
                table: "Admissions",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "Admissions",
                type: "int",
                nullable: true);
        }
    }
}
