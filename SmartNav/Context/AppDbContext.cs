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
        public DbSet<Mood> Moods { get; set; }
        public DbSet<Station> Stations { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<FilteredPreference> FilteredPreferences { get; set; }
        public DbSet<Preset> Presets { get; set; }
        public DbSet<PresetIcon> PresetIcons { get; set; }
        public DbSet<AdminActionLog> AdminActionLogs { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.UserName)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email);

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
            });

            modelBuilder.Entity<FilteredPreference>(entity =>
            {
                entity.Property(x => x.SelectedPreferenceCode).HasMaxLength(60);
                entity.Property(x => x.SelectedPreferencePrompt).HasMaxLength(400);
                entity.Property(x => x.MoodCode).HasMaxLength(60);
                entity.Property(x => x.VehicleSize).HasMaxLength(32);
                entity.Property(x => x.TrafficTimeMode).HasMaxLength(24);
                entity.Property(x => x.StationsJson).HasColumnType("nvarchar(max)");
                entity.Property(x => x.AppliedAt).HasDefaultValueSql("SYSUTCDATETIME()");
                entity.HasIndex(x => new { x.UserID, x.AppliedAt });
            });

            modelBuilder.Entity<FilteredPreference>()
                .HasOne(x => x.User)
                .WithMany(u => u.FilteredPreferences)
                .HasForeignKey(x => x.UserID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Preset>(entity =>
            {
                entity.Property(x => x.Street).HasMaxLength(120);
                entity.Property(x => x.Number).HasMaxLength(20);
                entity.Property(x => x.CityArea).HasMaxLength(80);
                entity.Property(x => x.PostalCode).HasMaxLength(20);
                entity.HasIndex(x => new { x.UserID, x.Position });
            });

            modelBuilder.Entity<PresetIcon>(entity =>
            {
                entity.Property(x => x.IconData).HasMaxLength(120);
                entity.Property(x => x.TranslationField).HasMaxLength(120);
            });

            modelBuilder.Entity<Preset>()
                .HasOne(x => x.User)
                .WithMany(u => u.Presets)
                .HasForeignKey(x => x.UserID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Preset>()
                .HasOne(x => x.PresetIcon)
                .WithMany(i => i.Presets)
                .HasForeignKey(x => x.PresetIconId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AdminActionLog>(entity =>
            {
                entity.Property(x => x.ActionType).IsRequired().HasMaxLength(80);
                entity.Property(x => x.Details).HasMaxLength(800);
                entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
                entity.HasIndex(x => new { x.AdminUserId, x.CreatedAt });
                entity.HasIndex(x => x.TargetUserId);
            });

            modelBuilder.Entity<UserSettings>(entity =>
            {
                entity.HasIndex(x => x.UserID).IsUnique();
                entity.Property(x => x.Theme).HasMaxLength(20);
                entity.Property(x => x.MapStyle).HasMaxLength(20);
                entity.Property(x => x.DistanceUnit).HasMaxLength(10);
                entity.Property(x => x.TimeFormat).HasMaxLength(10);
                entity.Property(x => x.ChipDensity).HasMaxLength(20);
                entity.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            });

            modelBuilder.Entity<UserSettings>()
                .HasOne(x => x.User)
                .WithOne(u => u.UserSettings)
                .HasForeignKey<UserSettings>(x => x.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Vehicle>().HasData(
                new Vehicle { Id = 1, Code = "small", Label = "Small car", TranslationField = "FILTER_VEHICLE_SIZE_SMALL" },
                new Vehicle { Id = 2, Code = "medium", Label = "SUV / Van", TranslationField = "FILTER_VEHICLE_SIZE_MEDIUM" },
                new Vehicle { Id = 3, Code = "large", Label = "Large van", TranslationField = "FILTER_VEHICLE_SIZE_LARGE" },
                new Vehicle { Id = 4, Code = "truck", Label = "Truck", TranslationField = "FILTER_VEHICLE_SIZE_TRUCK" },
                new Vehicle { Id = 5, Code = "motorcycle", Label = "Motorcycle", TranslationField = "FILTER_VEHICLE_SIZE_MOTORCYCLE" }
            );
        }
    }
}
