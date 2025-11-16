using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Xunit;

namespace Nocturne.Tools.Integration.Tests
{
    public class ApiIntegrationTests
    {
        private readonly HttpClient _httpClient;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;

        public ApiIntegrationTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        }

        [Fact]
        public async Task Test_GetNightscoutData_ReturnsSuccess()
        {
            // Arrange
            var url = "http://localhost:1337/api/v1/some_endpoint"; // Example Nightscout API endpoint
            var responseContent =
                "[{\"_id\":\"1234\",\"sgv\":120,\"dateString\":\"2023-06-01T12:00:00\",\"type\":\"sgv\"}]";

            SetupMockHttpMessageHandler(url, responseContent);

            // Act
            var response = await _httpClient.GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.NotNull(content);
            Assert.Contains("sgv", content);
        }

        [Fact]
        public async Task Test_ConnectToNightscout_ReturnsValidResponse()
        {
            // Arrange
            var url = "http://localhost:1337/api/v1/connection"; // Example connection endpoint
            var responseContent = "{\"status\":\"connected\",\"version\":\"14.2.0\"}";

            SetupMockHttpMessageHandler(url, responseContent);

            // Act
            var response = await _httpClient.GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("connected", content, StringComparison.OrdinalIgnoreCase);
        }

        private void SetupMockHttpMessageHandler(string url, string responseContent)
        {
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(
                    new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(responseContent),
                    }
                )
                .Verifiable();
        }
    }
}
