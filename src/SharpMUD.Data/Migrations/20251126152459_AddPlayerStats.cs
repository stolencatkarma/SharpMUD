using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpMUD.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentHealth",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsSpace",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LocationId",
                table: "Players",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MaxHealth",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "X",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Y",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Z",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentHealth",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsSpace",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MaxHealth",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "X",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Y",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Z",
                table: "Players");
        }
    }
}
