using System;
using GtMotive.Estimate.Microservice.Domain;
using GtMotive.Estimate.Microservice.Domain.Entities;
using Xunit;

namespace GtMotive.Estimate.Microservice.UnitTests.Renting
{
    public class RentalTests
    {
        private static readonly DateTime StartTime = new(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void StartCreatesActiveRentalWithNormalizedCustomerId()
        {
            var rentalId = Guid.NewGuid();
            var vehicleId = Guid.NewGuid();

            var rental = Rental.Start(rentalId, vehicleId, " customer-42 ", StartTime);

            Assert.Equal(rentalId, rental.Id);
            Assert.Equal(vehicleId, rental.VehicleId);
            Assert.Equal("CUSTOMER-42", rental.CustomerId);
            Assert.Equal(StartTime, rental.StartedAtUtc);
            Assert.Null(rental.ReturnedAtUtc);
            Assert.True(rental.IsActive);
        }

        [Fact]
        public void StartEmptyRentalIdRejectsRental()
        {
            Assert.Throws<DomainException>(() => Rental.Start(Guid.Empty, Guid.NewGuid(), "customer-42", StartTime));
        }

        [Fact]
        public void StartEmptyVehicleIdRejectsRental()
        {
            Assert.Throws<DomainException>(() => Rental.Start(Guid.NewGuid(), Guid.Empty, "customer-42", StartTime));
        }

        [Fact]
        public void StartBlankCustomerIdRejectsRental()
        {
            Assert.Throws<DomainException>(() => Rental.Start(Guid.NewGuid(), Guid.NewGuid(), " ", StartTime));
        }

        [Fact]
        public void StartNonUtcTimestampRejectsRental()
        {
            var nonUtc = DateTime.SpecifyKind(StartTime, DateTimeKind.Local);

            Assert.Throws<DomainException>(() => Rental.Start(Guid.NewGuid(), Guid.NewGuid(), "customer-42", nonUtc));
        }

        [Fact]
        public void ReturnClosesRentalWithoutErasingHistory()
        {
            var rental = NewRental();
            var returnTime = StartTime.AddHours(3);

            rental.Return(returnTime);

            Assert.False(rental.IsActive);
            Assert.Equal(returnTime, rental.ReturnedAtUtc);
            Assert.Equal(StartTime, rental.StartedAtUtc);
        }

        [Fact]
        public void ReturnSameInstantAsStartIsAllowed()
        {
            var rental = NewRental();

            rental.Return(StartTime);

            Assert.False(rental.IsActive);
        }

        [Fact]
        public void ReturnAlreadyReturnedRentalRejectsSecondReturn()
        {
            var rental = NewRental();
            rental.Return(StartTime.AddMinutes(1));

            Assert.Throws<DomainException>(() => rental.Return(StartTime.AddMinutes(2)));
        }

        [Fact]
        public void ReturnBeforeStartRejectsReturn()
        {
            var rental = NewRental();

            Assert.Throws<DomainException>(() => rental.Return(StartTime.AddTicks(-1)));
            Assert.True(rental.IsActive);
        }

        [Fact]
        public void ReturnNonUtcTimestampRejectsReturn()
        {
            var rental = NewRental();
            var nonUtc = DateTime.SpecifyKind(StartTime.AddHours(1), DateTimeKind.Unspecified);

            Assert.Throws<DomainException>(() => rental.Return(nonUtc));
            Assert.True(rental.IsActive);
        }

        private static Rental NewRental()
        {
            return Rental.Start(Guid.NewGuid(), Guid.NewGuid(), "customer-42", StartTime);
        }
    }
}
