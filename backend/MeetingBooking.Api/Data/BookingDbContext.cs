using MeetingBooking.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MeetingBooking.Api.Data;

public class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public const int GuestNameMaxLength = 100;
    public const int MeetingTitleMaxLength = 200;

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Room>(room =>
        {
            room.Property(r => r.Name).HasMaxLength(100).IsRequired();
            room.HasIndex(r => r.Name).IsUnique();

            room.HasData(
                new Room { Id = 1, Name = "Orion", Capacity = 12 },
                new Room { Id = 2, Name = "Cedar", Capacity = 4 },
                new Room { Id = 3, Name = "Lighthouse", Capacity = 8 });
        });

        modelBuilder.Entity<Booking>(booking =>
        {
            booking.Property(b => b.GuestName).HasMaxLength(GuestNameMaxLength).IsRequired();
            booking.Property(b => b.MeetingTitle).HasMaxLength(MeetingTitleMaxLength).IsRequired();

            booking.HasOne(b => b.Room)
                .WithMany()
                .HasForeignKey(b => b.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            booking.Property(b => b.OwnerKeyHash).HasMaxLength(64);

            booking.HasIndex(b => new { b.RoomId, b.Date });

            booking.HasIndex(b => b.OwnerKeyHash);
        });
    }
}
