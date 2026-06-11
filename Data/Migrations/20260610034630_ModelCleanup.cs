using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TimesheetApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModelCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "01f2a3b2-a052-4656-ad13-60354021925d");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "d042b05d-34f4-488e-b9ac-30fa5f73be38");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "e830da53-b175-4a57-9e55-2c1573a96026");

            migrationBuilder.AlterColumn<string>(
                name: "WPProjectId",
                table: "Budgets",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "37e7120e-5609-4a75-bec4-908ecb7497c2", null, "HR", "HR" },
                    { "8ba1a177-5c9e-4b6c-87a7-44c8f44d0e5f", null, "Admin", "ADMIN" },
                    { "ef38ee2e-5e03-462f-9254-2cc57c51eebf", null, "Supervisor", "SUPERVISOR" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_WPProjectId",
                table: "Budgets",
                column: "WPProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Budgets_WPProjectId",
                table: "Budgets");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "37e7120e-5609-4a75-bec4-908ecb7497c2");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "8ba1a177-5c9e-4b6c-87a7-44c8f44d0e5f");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "ef38ee2e-5e03-462f-9254-2cc57c51eebf");

            migrationBuilder.AlterColumn<string>(
                name: "WPProjectId",
                table: "Budgets",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "01f2a3b2-a052-4656-ad13-60354021925d", null, "Supervisor", "SUPERVISOR" },
                    { "d042b05d-34f4-488e-b9ac-30fa5f73be38", null, "Admin", "ADMIN" },
                    { "e830da53-b175-4a57-9e55-2c1573a96026", null, "HR", "HR" }
                });
        }
    }
}
