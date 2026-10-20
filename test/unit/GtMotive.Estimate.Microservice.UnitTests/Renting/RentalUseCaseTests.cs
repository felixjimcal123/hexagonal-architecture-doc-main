using System;
using System.Threading;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.ApplicationCore.Exceptions;
using GtMotive.Estimate.Microservice.ApplicationCore.UseCases.Rentals;
using GtMotive.Estimate.Microservice.Domain;
using GtMotive.Estimate.Microservice.Domain.Entities;
using GtMotive.Estimate.Microservice.Domain.Interfaces;
using Moq;
using Xunit;

namespace GtMotive.Estimate.Microservice.UnitTests.Renting
{
    public sealed class RentalUseCaseTests
    {
        private static readonly DateOnly Today = new(2026, 10, 19);
        private static readonly Guid VehicleId = Guid.NewGuid();
        private static readonly DateTime StartedAt = new(2026, 10, 19, 11, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task RentCreatesPersistedRentalAndNormalizesCustomer()
        {
            var vehicles = new Mock<IVehicleRepository>();
            var rentals = new Mock<IRentalRepository>();
            var work = new Mock<IUnitOfWork>();
            vehicles.Setup(repository => repository.GetByIdAsync(VehicleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(EligibleVehicle());
            rentals.Setup(repository => repository.AddAsync(It.IsAny<Rental>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            work.Setup(unit => unit.Save()).ReturnsAsync(1);
            var useCase = new RentVehicleUseCase(vehicles.Object, rentals.Object, work.Object, FixedClock());

            var rental = await useCase.ExecuteAsync(VehicleId, " client-1 ");

            Assert.Equal(VehicleId, rental.VehicleId);
            Assert.Equal("CLIENT-1", rental.CustomerId);
            Assert.True(rental.IsActive);
            rentals.Verify(repository => repository.HasActiveRentalForCustomerAsync("CLIENT-1", It.IsAny<CancellationToken>()), Times.Once);
            rentals.Verify(repository => repository.AddAsync(rental, It.IsAny<CancellationToken>()), Times.Once);
            work.Verify(unit => unit.Save(), Times.Once);
        }

        [Fact]
        public async Task RentMissingVehicleReturnsNotFound()
        {
            var vehicles = new Mock<IVehicleRepository>();
            var rentals = new Mock<IRentalRepository>(MockBehavior.Strict);
            var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
            var useCase = new RentVehicleUseCase(vehicles.Object, rentals.Object, work.Object, FixedClock());

            await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(VehicleId, "client-1"));
            rentals.VerifyNoOtherCalls();
            work.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RentRejectsVehicleOutsideAgeLimit()
        {
            var old = Vehicle.Register(Guid.NewGuid(), "OLD-100", "Brand", "Model", Today.AddYears(-6), Today.AddYears(-4));
            var vehicles = new Mock<IVehicleRepository>();
            var rentals = new Mock<IRentalRepository>(MockBehavior.Strict);
            var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
            vehicles.Setup(repository => repository.GetByIdAsync(old.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(old);
            var useCase = new RentVehicleUseCase(vehicles.Object, rentals.Object, work.Object, FixedClock());

            await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(old.Id, "client-1"));
            rentals.VerifyNoOtherCalls();
            work.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RentRejectsCustomerWithActiveRental()
        {
            var vehicles = new Mock<IVehicleRepository>();
            var rentals = new Mock<IRentalRepository>();
            var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
            vehicles.Setup(repository => repository.GetByIdAsync(VehicleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(EligibleVehicle());
            rentals.Setup(repository => repository.HasActiveRentalForCustomerAsync("CLIENT-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var useCase = new RentVehicleUseCase(vehicles.Object, rentals.Object, work.Object, FixedClock());

            await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(VehicleId, "client-1"));
            rentals.Verify(repository => repository.AddAsync(It.IsAny<Rental>(), It.IsAny<CancellationToken>()), Times.Never);
            work.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RentRejectsAlreadyRentedVehicle()
        {
            var vehicles = new Mock<IVehicleRepository>();
            var rentals = new Mock<IRentalRepository>();
            var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
            vehicles.Setup(repository => repository.GetByIdAsync(VehicleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(EligibleVehicle());
            rentals.Setup(repository => repository.HasActiveRentalForVehicleAsync(VehicleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var useCase = new RentVehicleUseCase(vehicles.Object, rentals.Object, work.Object, FixedClock());

            await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(VehicleId, "client-2"));
            rentals.Verify(repository => repository.AddAsync(It.IsAny<Rental>(), It.IsAny<CancellationToken>()), Times.Never);
            work.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReturnCompletesActiveRentalAndSaves()
        {
            var rental = Rental.Start(Guid.NewGuid(), VehicleId, "client-1", StartedAt);
            var rentals = new Mock<IRentalRepository>();
            var work = new Mock<IUnitOfWork>();
            rentals.Setup(repository => repository.GetByIdAsync(rental.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(rental);
            work.Setup(unit => unit.Save()).ReturnsAsync(1);
            var useCase = new ReturnVehicleUseCase(rentals.Object, work.Object, FixedClock());

            var result = await useCase.ExecuteAsync(rental.Id);

            Assert.Same(rental, result);
            Assert.False(result.IsActive);
            Assert.Equal(new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc), result.ReturnedAtUtc);
            work.Verify(unit => unit.Save(), Times.Once);
        }

        [Fact]
        public async Task ReturnUnknownRentalReturnsNotFound()
        {
            var rentals = new Mock<IRentalRepository>();
            var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
            var useCase = new ReturnVehicleUseCase(rentals.Object, work.Object, FixedClock());

            await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid()));
            work.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReturnAlreadyCompletedRentalReturnsConflict()
        {
            var rental = Rental.Start(Guid.NewGuid(), VehicleId, "client-1", StartedAt);
            rental.Return(StartedAt.AddMinutes(30));
            var rentals = new Mock<IRentalRepository>();
            var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
            rentals.Setup(repository => repository.GetByIdAsync(rental.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(rental);
            var useCase = new ReturnVehicleUseCase(rentals.Object, work.Object, FixedClock());

            await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(rental.Id));
            work.VerifyNoOtherCalls();
        }

        private static Vehicle EligibleVehicle()
        {
            return Vehicle.Register(VehicleId, "RENT-01", "Brand", "Model", Today.AddYears(-1), Today);
        }

        private static TimeProvider FixedClock()
        {
            var clock = new Mock<TimeProvider>();
            clock.Setup(provider => provider.GetUtcNow())
                .Returns(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));
            return clock.Object;
        }
    }
}
