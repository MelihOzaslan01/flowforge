using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowForge.Worker.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxTopic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "topic",
                table: "outbox_messages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "topic",
                table: "outbox_messages");
        }
    }
}
