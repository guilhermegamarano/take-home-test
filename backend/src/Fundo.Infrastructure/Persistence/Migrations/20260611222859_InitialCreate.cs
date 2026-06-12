using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // EF Core generates multidimensional seed data arrays.
#pragma warning disable CA1861 // EF Core generates constant column arrays for InsertData.

namespace Fundo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Loans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loans", x => x.Id);
                    table.CheckConstraint("CK_Loans_Amount_Positive", "[Amount] > 0");
                    table.CheckConstraint("CK_Loans_CurrentBalance_Valid", "[CurrentBalance] >= 0 AND [CurrentBalance] <= [Amount]");
                    table.CheckConstraint("CK_Loans_Status_Balance_Consistent", "([Status] = 'Active' AND [CurrentBalance] > 0) OR ([Status] = 'Paid' AND [CurrentBalance] = 0)");
                    table.CheckConstraint("CK_Loans_Status_Valid", "[Status] IN ('Active', 'Paid')");
                    table.CheckConstraint("CK_Loans_Type_Valid", "[Type] IN ('Personal', 'SmallBusiness', 'Bridge')");
                });

            migrationBuilder.InsertData(
                table: "Loans",
                columns: new[] { "Id", "Amount", "ApplicantName", "CreatedAtUtc", "CurrentBalance", "Status", "Type" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), 25000m, "John Doe", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 18750m, "Active", "Personal" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), 15000m, "Jane Smith", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0m, "Paid", "Personal" },
                    { new Guid("33333333-3333-3333-3333-333333333333"), 50000m, "Robert Johnson", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 32500m, "Active", "SmallBusiness" },
                    { new Guid("44444444-4444-4444-4444-444444444444"), 10000m, "Emily Williams", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0m, "Paid", "Personal" },
                    { new Guid("55555555-5555-5555-5555-555555555555"), 75000m, "Michael Brown", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 72000m, "Active", "Bridge" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Loans_CreatedAtUtc",
                table: "Loans",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Loans_Status",
                table: "Loans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Loans_Type",
                table: "Loans",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Loans");
        }
    }
}
