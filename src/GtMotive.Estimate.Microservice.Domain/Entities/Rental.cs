#nullable enable
using System;

namespace GtMotive.Estimate.Microservice.Domain.Entities
{
    /// <summary>
    /// A rental record. Active rentals have no return timestamp.
    /// </summary>
    public sealed class Rental
    {
        private Rental()
        {
            // Required by EF Core when materializing an existing rental.
            CustomerId = string.Empty;
        }

        /// <summary>
        /// Gets the rental identifier.
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// Gets the rented vehicle identifier.
        /// </summary>
        public Guid VehicleId { get; private set; }

        /// <summary>
        /// Gets the normalized customer identifier.
        /// </summary>
        public string CustomerId { get; private set; }

        /// <summary>
        /// Gets the UTC date and time at which the rental started.
        /// </summary>
        public DateTime StartedAtUtc { get; private set; }

        /// <summary>
        /// Gets the UTC date and time of return, or null if still active.
        /// </summary>
        public DateTime? ReturnedAtUtc { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the rental is active.
        /// </summary>
        public bool IsActive => !ReturnedAtUtc.HasValue;

        /// <summary>
        /// Creates a new active rental. Cross-rental uniqueness is enforced by persistence.
        /// </summary>
        /// <param name="id">Rental identifier.</param>
        /// <param name="vehicleId">Vehicle identifier.</param>
        /// <param name="customerId">Stable customer identifier.</param>
        /// <param name="startedAtUtc">Start instant in UTC.</param>
        /// <returns>An active rental.</returns>
        /// <exception cref="DomainException">When the rental data is invalid.</exception>
        public static Rental Start(Guid id, Guid vehicleId, string customerId, DateTime startedAtUtc)
        {
            if (id == Guid.Empty)
            {
                throw new DomainException("The rental identifier is required.");
            }

            if (vehicleId == Guid.Empty)
            {
                throw new DomainException("The vehicle identifier is required.");
            }

            if (string.IsNullOrWhiteSpace(customerId))
            {
                throw new DomainException("The customer identifier is required.");
            }

            if (startedAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new DomainException("The rental start must be provided in UTC.");
            }

            return new Rental
            {
                Id = id,
                VehicleId = vehicleId,
                CustomerId = customerId.Trim().ToUpperInvariant(),
                StartedAtUtc = startedAtUtc,
            };
        }

        /// <summary>
        /// Returns an active rental without deleting its history.
        /// </summary>
        /// <param name="returnedAtUtc">Return instant in UTC.</param>
        /// <exception cref="DomainException">When the rental is already returned or the instant is invalid.</exception>
        public void Return(DateTime returnedAtUtc)
        {
            if (!IsActive)
            {
                throw new DomainException("The rental has already been returned.");
            }

            if (returnedAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new DomainException("The rental return must be provided in UTC.");
            }

            if (returnedAtUtc < StartedAtUtc)
            {
                throw new DomainException("The return cannot precede the rental start.");
            }

            ReturnedAtUtc = returnedAtUtc;
        }
    }
}
