using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBooking.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingOwnershipAndCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "Bookings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerKeyHash",
                table: "Bookings",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_OwnerKeyHash",
                table: "Bookings",
                column: "OwnerKeyHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_OwnerKeyHash",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "OwnerKeyHash",
                table: "Bookings");
        }
    }
}
