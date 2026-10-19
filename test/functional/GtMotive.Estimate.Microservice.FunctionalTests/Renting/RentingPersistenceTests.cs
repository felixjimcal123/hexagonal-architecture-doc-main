using System;
using System.IO;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.Domain.Entities;
using GtMotive.Estimate.Microservice.Domain.Interfaces;
using GtMotive.Estimate.Microservice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GtMotive.Estimate.Microservice.FunctionalTests.Renting
{
    /// <summary>
    /// Tests the adapters and database constraints against a real, temporary SQLite file (no HTTP host).
    /// </summary>
    public sealed class RentingPersistenceTests : IAsyncLifetime
    {
        private static readonly DateOnly Today = new(2026, 10, 19);
        private static readonly DateTime StartedAt = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"gtmotive-{Guid.NewGuid():N}.db");
        private ServiceProvider _provider;

        public async Task InitializeAsync()
        {
            var services = new ServiceCollection();
            services.AddRentingPersistence($"Data Source={_databasePath};Pooling=False");
            _provider = services.BuildServiceProvider();

            await using var scope = _provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<RentingDbContext>();
            await context.Database.EnsureCreatedAsync();
        }

        public async Task DisposeAsync()
        {
            await _provider.DisposeAsync();
            File.Delete(_databasePath);
        }

        [Fact]
        public async Task AvailableVehiclesExcludeActiveRentalsAndOverAgeVehicles()
        {
            var freeId = await RegisterVehicleAsync("1000ABC", new DateOnly(2023, 5, 1));
            var rentedId = await RegisterVehicleAsync("2000ABC", new DateOnly(2024, 5, 1));
            await RegisterVehicleAsync("3000ABC", new DateOnly(2020, 5, 1), new DateOnly(2024, 5, 1));
            await StartRentalAsync(rentedId, "client-a");

            await using var scope = _provider.CreateAsyncScope();
            var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            var rentals = scope.ServiceProvider.GetRequiredService<IRentalRepository>();
            var available = await vehicles.ListAvailableAsync(Today);

            Assert.Single(available);
            Assert.Equal(freeId, available[0].Id);
            Assert.True(await rentals.HasActiveRentalForVehicleAsync(rentedId));
            Assert.True(await rentals.HasActiveRentalForCustomerAsync("CLIENT-A"));
            Assert.True(await vehicles.ExistsByLicensePlateAsync("1000ABC"));
        }

        [Fact]
        public async Task AvailabilityRespectsLeapDayCalendarBoundary()
        {
            await RegisterVehicleAsync("4000ABC", new DateOnly(2019, 2, 28), new DateOnly(2023, 2, 28));

            await using var scope = _provider.CreateAsyncScope();
            var available = await scope.ServiceProvider.GetRequiredService<IVehicleRepository>()
                .ListAvailableAsync(new DateOnly(2024, 2, 29));

            Assert.Empty(available);
        }

        [Fact]
        public async Task UniqueVehicleConstraintRejectsAnotherActiveRental()
        {
            var vehicleId = await RegisterVehicleAsync("5000ABC", new DateOnly(2024, 1, 1));
            await StartRentalAsync(vehicleId, "client-1");

            await using var scope = _provider.CreateAsyncScope();
            var rentals = scope.ServiceProvider.GetRequiredService<IRentalRepository>();
            await rentals.AddAsync(Rental.Start(Guid.NewGuid(), vehicleId, "client-2", StartedAt));

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Save();
            });
        }

        [Fact]
        public async Task UniqueCustomerConstraintRejectsAnotherActiveRental()
        {
            var firstVehicleId = await RegisterVehicleAsync("6000ABC", new DateOnly(2024, 1, 1));
            var otherVehicleId = await RegisterVehicleAsync("7000ABC", new DateOnly(2024, 1, 1));
            await StartRentalAsync(firstVehicleId, "client-1");

            await using var scope = _provider.CreateAsyncScope();
            var rentals = scope.ServiceProvider.GetRequiredService<IRentalRepository>();
            await rentals.AddAsync(Rental.Start(Guid.NewGuid(), otherVehicleId, "client-1", StartedAt));

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Save();
            });
        }

        [Fact]
        public async Task ReturningRentalPreservesHistoryAndReleasesUniquenessConstraints()
        {
            var vehicleId = await RegisterVehicleAsync("8000ABC", new DateOnly(2024, 1, 1));
            var otherVehicleId = await RegisterVehicleAsync("9000ABC", new DateOnly(2024, 1, 1));
            var originalRentalId = await StartRentalAsync(vehicleId, "client-1");
            var returnedAt = StartedAt.AddHours(2);

            await using (var scope = _provider.CreateAsyncScope())
            {
                var rentals = scope.ServiceProvider.GetRequiredService<IRentalRepository>();
                var originalRental = await rentals.GetByIdAsync(originalRentalId);
                Assert.NotNull(originalRental);
                originalRental.Return(returnedAt);
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Save();
            }

            await StartRentalAsync(vehicleId, "client-2");
            await StartRentalAsync(otherVehicleId, "client-1");

            await using var verificationScope = _provider.CreateAsyncScope();
            var repository = verificationScope.ServiceProvider.GetRequiredService<IRentalRepository>();
            var historicalRental = await repository.GetByIdAsync(originalRentalId);
            Assert.NotNull(historicalRental);
            Assert.False(historicalRental.IsActive);
            Assert.Equal(returnedAt, historicalRental.ReturnedAtUtc);
            Assert.Equal(DateTimeKind.Utc, historicalRental.StartedAtUtc.Kind);
            Assert.Equal(DateTimeKind.Utc, historicalRental.ReturnedAtUtc.Value.Kind);
        }

        [Fact]
        public async Task StaleSecondReturnIsRejectedByConcurrencyToken()
        {
            var vehicleId = await RegisterVehicleAsync("1100ABC", new DateOnly(2024, 1, 1));
            var rentalId = await StartRentalAsync(vehicleId, "client-1");
            await using var firstScope = _provider.CreateAsyncScope();
            await using var secondScope = _provider.CreateAsyncScope();
            var firstRepository = firstScope.ServiceProvider.GetRequiredService<IRentalRepository>();
            var secondRepository = secondScope.ServiceProvider.GetRequiredService<IRentalRepository>();
            var firstCopy = await firstRepository.GetByIdAsync(rentalId);
            var staleCopy = await secondRepository.GetByIdAsync(rentalId);
            Assert.NotNull(firstCopy);
            Assert.NotNull(staleCopy);

            firstCopy.Return(StartedAt.AddHours(1));
            await firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>().Save();

            staleCopy.Return(StartedAt.AddHours(2));
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
            {
                await secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>().Save();
            });
        }

        [Fact]
        public async Task UniqueLicensePlateConstraintRejectsDuplicateRegistrations()
        {
            await RegisterVehicleAsync("1200ABC", new DateOnly(2024, 1, 1));

            await using var scope = _provider.CreateAsyncScope();
            var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            await vehicles.AddAsync(Vehicle.Register(
                Guid.NewGuid(), "1200 abc", "Brand", "Model", new DateOnly(2024, 1, 1), Today));

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Save();
            });
        }

        private async Task<Guid> RegisterVehicleAsync(
            string plate,
            DateOnly manufacturedOn,
            DateOnly? registeredOn = null)
        {
            var vehicle = Vehicle.Register(Guid.NewGuid(), plate, "Brand", "Model", manufacturedOn, registeredOn ?? Today);
            await using var scope = _provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IVehicleRepository>().AddAsync(vehicle);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Save();
            return vehicle.Id;
        }

        private async Task<Guid> StartRentalAsync(Guid vehicleId, string customerId)
        {
            var rental = Rental.Start(Guid.NewGuid(), vehicleId, customerId, StartedAt);
            await using var scope = _provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IRentalRepository>().AddAsync(rental);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Save();
            return rental.Id;
        }
    }
}
