using System;
using GtMotive.Estimate.Microservice.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GtMotive.Estimate.Microservice.Infrastructure.Persistence
{
    /// <summary>
    /// Configures the database and the output adapters without depending on the HTTP host.
    /// </summary>
    public static class RentingPersistenceConfiguration
    {
        public static IServiceCollection AddRentingPersistence(this IServiceCollection services, string connectionString)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

            services.AddDbContext<RentingDbContext>(options => options.UseSqlite(connectionString));
            services.AddScoped<IVehicleRepository, VehicleRepository>();
            services.AddScoped<IRentalRepository, RentalRepository>();
            services.AddScoped<IUnitOfWork, RentingUnitOfWork>();

            return services;
        }
    }
}
