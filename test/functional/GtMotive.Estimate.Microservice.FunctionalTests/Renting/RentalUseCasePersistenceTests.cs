using System;
using System.IO;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.ApplicationCore;
using GtMotive.Estimate.Microservice.ApplicationCore.Exceptions;
using GtMotive.Estimate.Microservice.ApplicationCore.UseCases.Rentals;
using GtMotive.Estimate.Microservice.ApplicationCore.UseCases.Vehicles;
using GtMotive.Estimate.Microservice.Domain.Interfaces;
using GtMotive.Estimate.Microservice.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GtMotive.Estimate.Microservice.FunctionalTests.Renting
{
    /// <summary>
    /// Exercises the application use cases and real SQLite adapter without an HTTP host.
    /// </summary>
    public sealed class RentalUseCasePersistenceTests : IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"gtmotive-rent-{Guid.NewGuid():N}.db");
        private ServiceProvider _provider;

        public async Task InitializeAsync()
        {
            var services = new ServiceCollection();
            services.AddUseCases();
            services.AddRentingPersistence($"Data Source={_databasePath};Pooling=False");
            _provider = services.BuildServiceProvider();

            await using var scope = _provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<RentingDbContext>().Database.EnsureCreatedAsync();
        }

        public async Task DisposeAsync()
        {
            await _provider.DisposeAsync();
            File.Delete(_databasePath);
        }

        [Fact]
        public async Task RentReturnAndRentAgainPreservesHistoryAcrossScopes()
        {
            var vehicleId = await CreateVehicleAsync("CYCLE-01");
            var original = await RentAsync(vehicleId, "client-A");

            await using (var rentedScope = _provider.CreateAsyncScope())
            {
                var available = await rentedScope.ServiceProvider.GetRequiredService<ListAvailableVehiclesUseCase>().ExecuteAsync();
                Assert.Empty(available);
            }

            await using (var returnScope = _provider.CreateAsyncScope())
            {
                var returned = await returnScope.ServiceProvider.GetRequiredService<ReturnVehicleUseCase>()
                    .ExecuteAsync(original.Id);
                Assert.False(returned.IsActive);
                Assert.NotNull(returned.ReturnedAtUtc);
            }

            await using (var availableScope = _provider.CreateAsyncScope())
            {
                var available = await availableScope.ServiceProvider.GetRequiredService<ListAvailableVehiclesUseCase>().ExecuteAsync();
                Assert.Single(available);
                Assert.Equal(vehicleId, available[0].Id);
            }

            var replacement = await RentAsync(vehicleId, "client-B");
            Assert.NotEqual(original.Id, replacement.Id);

            await using var verifyScope = _provider.CreateAsyncScope();
            var repository = verifyScope.ServiceProvider.GetRequiredService<IRentalRepository>();
            var history = await repository.GetByIdAsync(original.Id);
            Assert.NotNull(history);
            Assert.False(history.IsActive);
            Assert.Equal(DateTimeKind.Utc, history.ReturnedAtUtc.Value.Kind);
            Assert.True(await repository.HasActiveRentalForVehicleAsync(vehicleId));
        }

        [Fact]
        public async Task ACustomerCannotRentTwoDifferentVehicles()
        {
            var first = await CreateVehicleAsync("CUSTOMER-01");
            var second = await CreateVehicleAsync("CUSTOMER-02");
            await RentAsync(first, " client-a ");

            await using var scope = _provider.CreateAsyncScope();
            var rent = scope.ServiceProvider.GetRequiredService<RentVehicleUseCase>();
            await Assert.ThrowsAsync<ConflictException>(() => rent.ExecuteAsync(second, "CLIENT-A"));
        }

        [Fact]
        public async Task TwoCustomersCannotRentTheSameVehicle()
        {
            var vehicleId = await CreateVehicleAsync("VEHICLE-01");
            await RentAsync(vehicleId, "client-a");

            await using var scope = _provider.CreateAsyncScope();
            var rent = scope.ServiceProvider.GetRequiredService<RentVehicleUseCase>();
            await Assert.ThrowsAsync<ConflictException>(() => rent.ExecuteAsync(vehicleId, "client-b"));
        }

        [Fact]
        public async Task ReturningTwiceIsAConflictAndHistoryIsUnchanged()
        {
            var vehicleId = await CreateVehicleAsync("RETURN-01");
            var original = await RentAsync(vehicleId, "client-a");
            DateTime? firstReturn;

            await using (var scope = _provider.CreateAsyncScope())
            {
                var rental = await scope.ServiceProvider.GetRequiredService<ReturnVehicleUseCase>().ExecuteAsync(original.Id);
                firstReturn = rental.ReturnedAtUtc;
            }

            await using var secondScope = _provider.CreateAsyncScope();
            await Assert.ThrowsAsync<ConflictException>(() =>
                secondScope.ServiceProvider.GetRequiredService<ReturnVehicleUseCase>().ExecuteAsync(original.Id));

            var history = await secondScope.ServiceProvider.GetRequiredService<IRentalRepository>().GetByIdAsync(original.Id);
            Assert.Equal(firstReturn, history.ReturnedAtUtc);
        }

        [Fact]
        public async Task UnknownVehicleAndRentalReturnNotFound()
        {
            await using var scope = _provider.CreateAsyncScope();
            await Assert.ThrowsAsync<NotFoundException>(() =>
                scope.ServiceProvider.GetRequiredService<RentVehicleUseCase>().ExecuteAsync(Guid.NewGuid(), "client-a"));
            await Assert.ThrowsAsync<NotFoundException>(() =>
                scope.ServiceProvider.GetRequiredService<ReturnVehicleUseCase>().ExecuteAsync(Guid.NewGuid()));
        }

        private async Task<Guid> CreateVehicleAsync(string plate)
        {
            await using var scope = _provider.CreateAsyncScope();
            var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
            var result = await scope.ServiceProvider.GetRequiredService<CreateVehicleUseCase>()
                .ExecuteAsync(plate, "Brand", "Model", today.AddYears(-1));
            return result.Id;
        }

        private async Task<GtMotive.Estimate.Microservice.Domain.Entities.Rental> RentAsync(Guid vehicleId, string customerId)
        {
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<RentVehicleUseCase>()
                .ExecuteAsync(vehicleId, customerId);
        }
    }
}
