using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using GtMotive.Estimate.Microservice.InfrastructureTests.Infrastructure;
using Xunit;

namespace GtMotive.Estimate.Microservice.InfrastructureTests.Renting
{
    [Collection("TestServer")]
    public sealed class RentalsHttpValidationTests(GenericInfrastructureTestServerFixture fixture)
    {
        [Fact]
        public async Task RentalWithoutCustomerIdReturnsBadRequest()
        {
            using var client = fixture.Server.CreateClient();
            using var response = await client.PostAsJsonAsync("/api/rentals", new
            {
                VehicleId = Guid.NewGuid(),
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        [Fact]
        public async Task RentalWithoutVehicleIdReturnsBadRequest()
        {
            using var client = fixture.Server.CreateClient();
            using var response = await client.PostAsJsonAsync("/api/rentals", new
            {
                CustomerId = "client-a",
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
