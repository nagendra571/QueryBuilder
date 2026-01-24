using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QueryBuilder.Web.Data;

#nullable disable

namespace QueryBuilder.Web.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260201120000_AddVisualizationSoftDelete")]
    public partial class AddVisualizationSoftDelete : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Visualizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Visualizations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "Visualizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Visualizations_IsDeleted",
                table: "Visualizations",
                column: "IsDeleted");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Visualizations_IsDeleted",
                table: "Visualizations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Visualizations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Visualizations");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Visualizations");
        }
    }
}
