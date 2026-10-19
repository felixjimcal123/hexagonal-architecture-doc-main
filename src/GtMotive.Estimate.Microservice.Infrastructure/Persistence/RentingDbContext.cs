using System;
using GtMotive.Estimate.Microservice.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GtMotive.Estimate.Microservice.Infrastructure.Persistence
{
    /// <summary>
    /// SQLite persistence model. The database, not the use case, is the final arbiter of rental exclusivity.
    /// </summary>
    public sealed class RentingDbContext(DbContextOptions<RentingDbContext> options) : DbContext(options)
    {
        public DbSet<Vehicle> Vehicles => Set<Vehicle>();

        public DbSet<Rental> Rentals => Set<Rental>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<Vehicle>(entity =>
            {
                entity.ToTable("Vehicles");
                entity.HasKey(vehicle => vehicle.Id);
                entity.Property(vehicle => vehicle.Id).ValueGeneratedNever();
                entity.Property(vehicle => vehicle.LicensePlate).IsRequired().HasMaxLength(32);
                entity.Property(vehicle => vehicle.Brand).IsRequired().HasMaxLength(100);
                entity.Property(vehicle => vehicle.Model).IsRequired().HasMaxLength(100);
                entity.Property(vehicle => vehicle.ManufacturedOn).IsRequired();
                entity.HasIndex(vehicle => vehicle.LicensePlate).IsUnique();
            });

            // SQLite doesn't retain DateTime.Kind. Persist UTC ticks and restore the UTC kind on reading.
            var utcConverter = new ValueConverter<DateTime, long>(
                date => date.Ticks,
                ticks => new DateTime(ticks, DateTimeKind.Utc));

            modelBuilder.Entity<Rental>(entity =>
            {
                entity.ToTable("Rentals");
                entity.HasKey(rental => rental.Id);
                entity.Property(rental => rental.Id).ValueGeneratedNever();
                entity.Property(rental => rental.CustomerId).IsRequired().HasMaxLength(100);
                entity.Property(rental => rental.StartedAtUtc).HasConversion(utcConverter).IsRequired();
                entity.Property(rental => rental.ReturnedAtUtc).HasConversion(utcConverter).IsConcurrencyToken();
                entity.Ignore(rental => rental.IsActive);
                entity.HasOne<Vehicle>()
                    .WithMany()
                    .HasForeignKey(rental => rental.VehicleId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Historical rentals are allowed; only active ones must be unique.
                entity.HasIndex(rental => rental.VehicleId)
                    .IsUnique()
                    .HasFilter("\"ReturnedAtUtc\" IS NULL");
                entity.HasIndex(rental => rental.CustomerId)
                    .IsUnique()
                    .HasFilter("\"ReturnedAtUtc\" IS NULL");
            });
        }
    }
}
