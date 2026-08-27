using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace RefactoringChallenge.E2E;

public class OrderApiE2ETests
{
    [Fact]
    public async Task CreateOrder_WithValidRequest_ReturnsCreatedOrder()
    {
        var baseUrl =
            Environment.GetEnvironmentVariable("API_BASE_URL")
            ?? "http://127.0.0.1:5000";

        using var playwright =
            await Playwright.CreateAsync();

        var api = await playwright.APIRequest.NewContextAsync(
            new APIRequestNewContextOptions
            {
                BaseURL = baseUrl,
                ExtraHTTPHeaders =
                    new Dictionary<string, string>
                    {
                        ["Accept"] = "application/json"
                    }
            });

        try
        {
            var requestBody = new
            {
                customerName = "E2E User",
                customerEmail = "e2e@example.com",
                items = new[]
                {
                    new
                    {
                        sku = "BOOK",
                        quantity = 4
                    }
                },
                couponCode = "SAVE10"
            };

            var response = await api.PostAsync(
                "/api/orders",
                new APIRequestContextOptions
                {
                    DataObject = requestBody
                });

            Assert.Equal(201, response.Status);

            var json = await response.TextAsync();

            using var document =
                JsonDocument.Parse(json);

            var root = document.RootElement;

            Assert.Equal(
                "E2E User",
                root.GetProperty("customerName").GetString());

            Assert.Equal(
                100m,
                root.GetProperty("subtotal").GetDecimal());

            Assert.Equal(
                10m,
                root.GetProperty("discount").GetDecimal());

            Assert.Equal(
                12.50m,
                root.GetProperty("shippingCost").GetDecimal());

            Assert.Equal(
                102.50m,
                root.GetProperty("total").GetDecimal());

            Assert.Equal(
                "Pending",
                root.GetProperty("status").GetString());
        }
        finally
        {
            await api.DisposeAsync();
        }
    }
}
