using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QueryBuilder.Web.Data;

#nullable disable

namespace QueryBuilder.Web.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260121013000_AddQueryParameters")]
    public partial class AddQueryParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DashboardParameterControls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DashboardId = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    Placement = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardParameterControls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DashboardParameterControls_Dashboards_DashboardId",
                        column: x => x.DashboardId,
                        principalTable: "Dashboards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QueryParameterDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QueryId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueryParameterDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueryParameterDefinitions_Queries_QueryId",
                        column: x => x.QueryId,
                        principalTable: "Queries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DashboardParameterMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DashboardWidgetId = table.Column<int>(type: "int", nullable: false),
                    QueryId = table.Column<int>(type: "int", nullable: false),
                    QueryParamKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BindingType = table.Column<int>(type: "int", nullable: false),
                    StaticValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ControlKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardParameterMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DashboardParameterMappings_DashboardWidgets_DashboardWidgetId",
                        column: x => x.DashboardWidgetId,
                        principalTable: "DashboardWidgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DashboardParameterMappings_Queries_QueryId",
                        column: x => x.QueryId,
                        principalTable: "Queries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DashboardParameterControls_DashboardId",
                table: "DashboardParameterControls",
                column: "DashboardId");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardParameterControls_DashboardId_Key",
                table: "DashboardParameterControls",
                columns: new[] { "DashboardId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DashboardParameterMappings_DashboardWidgetId",
                table: "DashboardParameterMappings",
                column: "DashboardWidgetId");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardParameterMappings_DashboardWidgetId_QueryParamKey",
                table: "DashboardParameterMappings",
                columns: new[] { "DashboardWidgetId", "QueryParamKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DashboardParameterMappings_QueryId",
                table: "DashboardParameterMappings",
                column: "QueryId");

            migrationBuilder.CreateIndex(
                name: "IX_QueryParameterDefinitions_QueryId",
                table: "QueryParameterDefinitions",
                column: "QueryId");

            migrationBuilder.CreateIndex(
                name: "IX_QueryParameterDefinitions_QueryId_Name",
                table: "QueryParameterDefinitions",
                columns: new[] { "QueryId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DashboardParameterMappings");

            migrationBuilder.DropTable(
                name: "DashboardParameterControls");

            migrationBuilder.DropTable(
                name: "QueryParameterDefinitions");
        }
    }
}
