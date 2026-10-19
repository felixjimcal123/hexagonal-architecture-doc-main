using System;
using GtMotive.Estimate.Microservice.Domain;
using GtMotive.Estimate.Microservice.Domain.Entities;
using Xunit;

namespace GtMotive.Estimate.Microservice.UnitTests.Renting
{
    public class VehicleTests
    {
        private static readonly DateOnly Today = new(2026, 10, 19);

        [Fact]
        public void RegisterExactlyFiveYearsOldAllowsVehicle()
        {
            var vehicle = NewVehicle(new DateOnly(2021, 10, 19));

            Assert.Equal(new DateOnly(2021, 10, 19), vehicle.ManufacturedOn);
            Assert.True(vehicle.IsEligibleForRental(Today));
        }

        [Fact]
        public void RegisterOlderThanFiveYearsRejectsVehicle()
        {
            Assert.Throws<DomainException>(() => NewVehicle(new DateOnly(2021, 10, 18)));
        }

        [Fact]
        public void RegisterFutureManufacturingDateRejectsVehicle()
        {
            Assert.Throws<DomainException>(() => NewVehicle(Today.AddDays(1)));
        }

        [Fact]
        public void RegisterNormalizesLicensePlateAndTrimsFields()
        {
            var vehicle = Vehicle.Register(
                Guid.NewGuid(),
                " 1234 ab c ",
                " Example Brand ",
                " Example Model ",
                new DateOnly(2024, 2, 1),
                Today);

            Assert.Equal("1234ABC", vehicle.LicensePlate);
            Assert.Equal("Example Brand", vehicle.Brand);
            Assert.Equal("Example Model", vehicle.Model);
        }

        [Fact]
        public void RegisterEmptyVehicleIdRejectsVehicle()
        {
            Assert.Throws<DomainException>(() => Vehicle.Register(
                Guid.Empty,
                "1234ABC",
                "Example Brand",
                "Example Model",
                new DateOnly(2024, 2, 1),
                Today));
        }

        [Fact]
        public void RegisterWhitespacePlateRejectsVehicle()
        {
            Assert.Throws<DomainException>(() => Vehicle.Register(
                Guid.NewGuid(),
                "  ",
                "Example Brand",
                "Example Model",
                new DateOnly(2024, 2, 1),
                Today));
        }

        [Fact]
        public void RegisterWhitespaceBrandRejectsVehicle()
        {
            Assert.Throws<DomainException>(() => Vehicle.Register(
                Guid.NewGuid(),
                "1234ABC",
                "  ",
                "Example Model",
                new DateOnly(2024, 2, 1),
                Today));
        }

        [Fact]
        public void RegisterWhitespaceModelRejectsVehicle()
        {
            Assert.Throws<DomainException>(() => Vehicle.Register(
                Guid.NewGuid(),
                "1234ABC",
                "Example Brand",
                "  ",
                new DateOnly(2024, 2, 1),
                Today));
        }

        [Fact]
        public void IsEligibleForRentalExcludesVehicleAfterItAgesOut()
        {
            var vehicle = NewVehicle(new DateOnly(2021, 10, 19));

            Assert.False(vehicle.IsEligibleForRental(Today.AddDays(1)));
        }

        [Fact]
        public void IsEligibleForRentalRejectsDateBeforeManufacture()
        {
            var vehicle = NewVehicle(new DateOnly(2024, 2, 1));

            Assert.False(vehicle.IsEligibleForRental(new DateOnly(2024, 1, 31)));
        }

        private static Vehicle NewVehicle(DateOnly manufacturedOn)
        {
            return Vehicle.Register(
                Guid.NewGuid(),
                "1234ABC",
                "Example Brand",
                "Example Model",
                manufacturedOn,
                Today);
        }
    }
}
