using System;
using GtMotive.Estimate.Microservice.Domain;

namespace GtMotive.Estimate.Microservice.ApplicationCore.Exceptions
{
    /// <summary>
    /// An entity referenced by an operation does not exist.
    /// </summary>
    public sealed class NotFoundException : DomainException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotFoundException"/> class.
        /// </summary>
        public NotFoundException()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NotFoundException"/> class with an explanatory message.
        /// </summary>
        /// <param name="message">Description of the missing resource.</param>
        public NotFoundException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NotFoundException"/> class with a message and underlying exception.
        /// </summary>
        /// <param name="message">Description of the missing resource.</param>
        /// <param name="innerException">The exception that caused the resource lookup failure.</param>
        public NotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
