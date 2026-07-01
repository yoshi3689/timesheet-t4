using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TimesheetApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationCreatedAtAndTargetUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "44cd2666-6814-4a33-8a44-ee4ff835c01a");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "697ab3b3-60b1-427c-ab43-93b96889ea65");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "bdda633e-99ae-4485-b250-e6f752509414");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Notifications",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "TargetUrl",
                table: "Notifications",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "3aa3ac9c-2ea8-4f91-a13c-3e98476fac1b", null, "HR", "HR" },
                    { "488c0c63-7db3-4dcb-9cea-225890c4368e", null, "Supervisor", "SUPERVISOR" },
                    { "c930ec34-3a30-482b-9df2-00b27bb7c0b2", null, "Admin", "ADMIN" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3aa3ac9c-2ea8-4f91-a13c-3e98476fac1b");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "488c0c63-7db3-4dcb-9cea-225890c4368e");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "c930ec34-3a30-482b-9df2-00b27bb7c0b2");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TargetUrl",
                table: "Notifications");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "44cd2666-6814-4a33-8a44-ee4ff835c01a", null, "Admin", "ADMIN" },
                    { "697ab3b3-60b1-427c-ab43-93b96889ea65", null, "Supervisor", "SUPERVISOR" },
                    { "bdda633e-99ae-4485-b250-e6f752509414", null, "HR", "HR" }
                });
        }
    }
}
