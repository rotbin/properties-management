using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantMessageThreading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentMessageId",
                table: "TenantMessages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantMessages_ParentMessageId",
                table: "TenantMessages",
                column: "ParentMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantMessages_TenantMessages_ParentMessageId",
                table: "TenantMessages",
                column: "ParentMessageId",
                principalTable: "TenantMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TenantMessages_TenantMessages_ParentMessageId",
                table: "TenantMessages");

            migrationBuilder.DropIndex(
                name: "IX_TenantMessages_ParentMessageId",
                table: "TenantMessages");

            migrationBuilder.DropColumn(
                name: "ParentMessageId",
                table: "TenantMessages");
        }
    }
}
