using Microsoft.EntityFrameworkCore;
using SmartNav.Models;

namespace SmartNav.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Avatar> Avatars { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Trip> Trips { get; set; }
        public DbSet<Preference> Preferences { get; set; }
        public DbSet<Station> Stations { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.UserName)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.UserName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<User>()
            .ToTable(tb => tb.HasTrigger("trg_users_UpdateTimestamp"));

            modelBuilder.Entity<Trip>()
                .HasOne(t => t.Vehicle)
                .WithMany(v => v.Trips)
                .HasForeignKey(t => t.VehicleID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Trip>()
                .HasMany(t => t.Stations)
                .WithOne(s => s.Trip)
                .HasForeignKey(s => s.TripID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Vehicle>(entity =>
            {
                entity.HasIndex(v => v.Code).IsUnique();
                entity.Property(v => v.Code).IsRequired().HasMaxLength(32);
                entity.Property(v => v.Label).IsRequired().HasMaxLength(100);
                entity.Property(v => v.TranslationField).HasMaxLength(100);
            });

            modelBuilder.Entity<Station>(entity =>
            {
                entity.Property(s => s.Street).HasMaxLength(120);
                entity.Property(s => s.Number).HasMaxLength(20);
                entity.Property(s => s.CityArea).HasMaxLength(80);
                entity.Property(s => s.PostalCode).HasMaxLength(20);
                entity.Property(s => s.Position).IsRequired();
            });

            modelBuilder.Entity<Vehicle>().HasData(
                new Vehicle { Id = 1, Code = "small", Label = "Small car", TranslationField = "FILTER_VEHICLE_SIZE_SMALL" },
                new Vehicle { Id = 2, Code = "medium", Label = "SUV / Van", TranslationField = "FILTER_VEHICLE_SIZE_MEDIUM" },
                new Vehicle { Id = 3, Code = "large", Label = "Large van", TranslationField = "FILTER_VEHICLE_SIZE_LARGE" },
                new Vehicle { Id = 4, Code = "truck", Label = "Truck", TranslationField = "FILTER_VEHICLE_SIZE_TRUCK" }
            );
        }
    }
}
