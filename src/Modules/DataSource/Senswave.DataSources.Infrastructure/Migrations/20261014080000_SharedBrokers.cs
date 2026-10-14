using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Senswave.DataSources.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SharedBrokers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BrokerInfo_Url_Port",
                schema: "DataSources",
                table: "BrokerInfo");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerInfo_Url_Port",
                schema: "DataSources",
                table: "BrokerInfo",
                columns: new[] { "Url", "Port" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BrokerInfo_Url_Port",
                schema: "DataSources",
                table: "BrokerInfo");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerInfo_Url_Port",
                schema: "DataSources",
                table: "BrokerInfo",
                columns: new[] { "Url", "Port" },
                unique: true);
        }
    }
}
