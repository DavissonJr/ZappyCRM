using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsappCrmIA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GlobalAiKeyAndCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnthropicApiKeyEncrypted",
                table: "AiAgentConfigs");

            migrationBuilder.DropColumn(
                name: "AnthropicApiKeyPreview",
                table: "AiAgentConfigs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnthropicApiKeyEncrypted",
                table: "AiAgentConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnthropicApiKeyPreview",
                table: "AiAgentConfigs",
                type: "text",
                nullable: true);
        }
    }
}
