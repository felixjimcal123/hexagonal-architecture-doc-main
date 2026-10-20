using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.ApplicationCore.Exceptions;
using GtMotive.Estimate.Microservice.ApplicationCore.UseCases.Vehicles;
using GtMotive.Estimate.Microservice.Domain;
using GtMotive.Estimate.Microservice.Domain.Entities;
using GtMotive.Estimate.Microservice.Domain.Interfaces;
using Moq;
using Xunit;

namespace GtMotive.Estimate.Microservice.UnitTests.Renting
{
    public sealed class VehicleUseCaseTests
    {
        private static readonly DateOnly Today = new(2026, 10, 19);

        [Fact]
        public async Task CreatePersistsNormalizedVehicle()
        {
            var vehicles = new Mock<IVehicleRepository>();
            var work = new Mock<IUnitOfWork>();
            Vehicle saved = null;
            vehicles.Setup(repository => repository.AddAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()))
                .Callback<Vehicle, CancellationToken>((vehicle, _) => saved = vehicle)
                .Returns(Task.CompletedTask);
            work.Setup(unit => unit.Save()).ReturnsAsync(1);
            var useCase = new CreateVehicleUseCase(vehicles.Object, work.Object, FixedClock());

            var result = await useCase.ExecuteAsync(" 1234 abc ", " Example ", " Model ", Today.AddYears(-2));

            Assert.Same(saved, result);
            Assert.Equal("1234ABC", result.LicensePlate);
            Assert.Equal("Example", result.Brand);
            work.Verify(unit => unit.Save(), Times.Once);
        }

        [Fact]
        public async Task CreateRejectsOverAgeVehicleBeforePersistence()
        {
            var vehicles = new Mock<IVehicleRepository>(MockBehavior.Strict);
            var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
            var useCase = new CreateVehicleUseCase(vehicles.Object, work.Object, FixedClock());

            await Assert.ThrowsAsync<DomainException>(() =>
                useCase.ExecuteAsync("1234ABC", "Example", "Model", Today.AddYears(-5).AddDays(-1)));

            vehicles.VerifyNoOtherCalls();
            work.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CreateRejectsDuplicateLicensePlateBeforeInsert()
        {
            var vehicles = new Mock<IVehicleRepository>();
            var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
            vehicles.Setup(repository => repository.ExistsByLicensePlateAsync("1234ABC", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var useCase = new CreateVehicleUseCase(vehicles.Object, work.Object, FixedClock());

            await Assert.ThrowsAsync<ConflictException>(() =>
                useCase.ExecuteAsync("1234 abc", "Example", "Model", Today.AddYears(-1)));

            vehicles.Verify(repository => repository.AddAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()), Times.Never);
            work.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ListDelegatesToRepositoryWithCurrentUtcDate()
        {
            var vehicles = new Mock<IVehicleRepository>();
            IReadOnlyList<Vehicle> rows = [];
            vehicles.Setup(repository => repository.ListAvailableAsync(Today, It.IsAny<CancellationToken>()))
                .ReturnsAsync(rows);
            var useCase = new ListAvailableVehiclesUseCase(vehicles.Object, FixedClock());

            var result = await useCase.ExecuteAsync();

            Assert.Same(rows, result);
            vehicles.Verify(repository => repository.ListAvailableAsync(Today, It.IsAny<CancellationToken>()), Times.Once);
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
