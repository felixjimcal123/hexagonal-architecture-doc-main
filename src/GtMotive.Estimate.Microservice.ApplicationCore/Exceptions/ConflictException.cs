using System;
using GtMotive.Estimate.Microservice.Domain;

namespace GtMotive.Estimate.Microservice.ApplicationCore.Exceptions
{
    /// <summary>
    /// A business operation conflicts with existing persisted data.
    /// </summary>
    public sealed class ConflictException : DomainException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConflictException"/> class.
        /// </summary>
        public ConflictException()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConflictException"/> class with an explanatory message.
        /// </summary>
        /// <param name="message">Description of the conflict.</param>
        public ConflictException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConflictException"/> class with a message and underlying exception.
        /// </summary>
        /// <param name="message">Description of the conflict.</param>
        /// <param name="innerException">The exception that caused the conflict.</param>
        public ConflictException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
