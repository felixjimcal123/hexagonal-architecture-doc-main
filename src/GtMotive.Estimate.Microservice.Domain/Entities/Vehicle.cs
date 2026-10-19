#nullable enable
using System;
using System.Linq;

namespace GtMotive.Estimate.Microservice.Domain.Entities
{
    /// <summary>
    /// A vehicle registered in the renting fleet.
    /// </summary>
    public sealed class Vehicle
    {
        private Vehicle()
        {
            // Required by EF Core when materializing an existing vehicle.
            LicensePlate = string.Empty;
            Brand = string.Empty;
            Model = string.Empty;
        }

        /// <summary>
        /// Gets the vehicle identifier.
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// Gets the normalized license plate.
        /// </summary>
        public string LicensePlate { get; private set; }

        /// <summary>
        /// Gets the vehicle brand.
        /// </summary>
        public string Brand { get; private set; }

        /// <summary>
        /// Gets the vehicle model.
        /// </summary>
        public string Model { get; private set; }

        /// <summary>
        /// Gets the vehicle manufacturing date.
        /// </summary>
        public DateOnly ManufacturedOn { get; private set; }

        /// <summary>
        /// Registers a vehicle if it is no more than five calendar years old.
        /// </summary>
        /// <param name="id">Vehicle identifier.</param>
        /// <param name="licensePlate">Vehicle license plate.</param>
        /// <param name="brand">Vehicle brand.</param>
        /// <param name="model">Vehicle model.</param>
        /// <param name="manufacturedOn">Manufacturing date.</param>
        /// <param name="today">Current calendar date, provided by the caller.</param>
        /// <returns>A valid vehicle.</returns>
        /// <exception cref="DomainException">When registration violates a fleet rule.</exception>
        public static Vehicle Register(
            Guid id,
            string licensePlate,
            string brand,
            string model,
            DateOnly manufacturedOn,
            DateOnly today)
        {
            if (id == Guid.Empty)
            {
                throw new DomainException("The vehicle identifier is required.");
            }

            if (string.IsNullOrWhiteSpace(licensePlate))
            {
                throw new DomainException("The license plate is required.");
            }

            if (string.IsNullOrWhiteSpace(brand))
            {
                throw new DomainException("The vehicle brand is required.");
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                throw new DomainException("The vehicle model is required.");
            }

            if (manufacturedOn > today)
            {
                throw new DomainException("The manufacturing date cannot be in the future.");
            }

            if (manufacturedOn.AddYears(5) < today)
            {
                throw new DomainException("The vehicle cannot be more than five years old.");
            }

            return new Vehicle
            {
                Id = id,
                LicensePlate = string.Concat(licensePlate.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant(),
                Brand = brand.Trim(),
                Model = model.Trim(),
                ManufacturedOn = manufacturedOn,
            };
        }

        /// <summary>
        /// Checks whether the vehicle is within the fleet age limit on a given date.
        /// This does not check whether there is an active rental.
        /// </summary>
        /// <param name="onDate">Date on which eligibility is checked.</param>
        /// <returns>True if the vehicle is within the age limit.</returns>
        public bool IsEligibleForRental(DateOnly onDate)
        {
            return ManufacturedOn <= onDate && ManufacturedOn.AddYears(5) >= onDate;
        }
    }
}
