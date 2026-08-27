using Moq;
using RefactoringChallenge.Api;
using RefactoringChallenge.Api.Models;

namespace RefactoringChallenge.Tests;

public class OrderServiceTests
{
    private static OrderService CreateService(
        OrderStore store,
        Mock<IEmailSender>? emailSenderMock = null)
    {
        emailSenderMock ??= new Mock<IEmailSender>();

        return new OrderService(
            store,
            emailSenderMock.Object);
    }

    [Fact]
    public void Create_WithValidRequest_CreatesOrder()
    {
        // Arrange
        var store = new OrderStore();
        var service = CreateService(store);

        var request = new CreateOrderRequest(
            "Emmanuel",
            "emmanuel@example.com",
            [
                new CreateOrderItemRequest(
                    "BOOK",
                    4)
            ],
            "SAVE10");

        // Act
        var order = service.Create(request);

        // Assert
        Assert.Equal("Emmanuel", order.CustomerName);
        Assert.Equal("emmanuel@example.com", order.CustomerEmail);

        Assert.Equal(100m, order.Subtotal);
        Assert.Equal(10m, order.Discount);
        Assert.Equal(12.50m, order.ShippingCost);
        Assert.Equal(102.50m, order.Total);

        Assert.Equal(
            OrderStatus.Pending,
            order.Status);

        Assert.Single(order.Items);
    }

    [Fact]
    public void Create_WithEmptyCustomerName_ThrowsArgumentException()
    {
        // Arrange
        var store = new OrderStore();
        var service = CreateService(store);

        var request = new CreateOrderRequest(
            "",
            "emmanuel@example.com",
            [
                new CreateOrderItemRequest(
                    "BOOK",
                    1)
            ],
            null);

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => service.Create(request));

        // Assert
        Assert.Equal(
            "Customer name is required.",
            exception.Message);
    }

    [Fact]
    public void Create_WithQuantityAtMaximumBoundary_CreatesOrder()
    {
        // Arrange
        var store = new OrderStore();

        var product = store.Products.First(
            product => product.Sku == "BOOK");

        product.Stock = 20;

        var service = CreateService(store);

        var request = new CreateOrderRequest(
            "Emmanuel",
            "emmanuel@example.com",
            [
                new CreateOrderItemRequest(
                    "BOOK",
                    20)
            ],
            null);

        // Act
        var order = service.Create(request);

        // Assert
        Assert.Equal(
            20,
            order.Items.Single().Quantity);
    }

    [Fact]
    public void Create_WithQuantityAboveMaximum_ThrowsArgumentException()
    {
        // Arrange
        var store = new OrderStore();
        var service = CreateService(store);

        var request = new CreateOrderRequest(
            "Emmanuel",
            "emmanuel@example.com",
            [
                new CreateOrderItemRequest(
                    "BOOK",
                    21)
            ],
            null);

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => service.Create(request));

        // Assert
        Assert.Equal(
            "Item quantity must be between 1 and 20.",
            exception.Message);
    }

    [Fact]
    public void Create_WhenSubtotalReachesFreeShippingThreshold_HasNoShippingCost()
    {
        // Arrange
        var store = new OrderStore();
        var service = CreateService(store);

        var request = new CreateOrderRequest(
            "Emmanuel",
            "emmanuel@example.com",
            [
                new CreateOrderItemRequest(
                    "KEYBOARD",
                    3)
            ],
            null);

        // Act
        var order = service.Create(request);

        // Assert
        Assert.Equal(150m, order.Subtotal);
        Assert.Equal(0m, order.ShippingCost);
    }

    [Fact]
    public async Task Confirm_WithPendingOrder_SendsEmailOnce()
    {
        // Arrange
        var store = new OrderStore();

        var emailSenderMock =
            new Mock<IEmailSender>();

        emailSenderMock
            .Setup(sender => sender.Send(
                It.IsAny<string>(),
                It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(
            store,
            emailSenderMock);

        var order = service.Create(
            new CreateOrderRequest(
                "Emmanuel",
                "emmanuel@example.com",
                [
                    new CreateOrderItemRequest(
                        "BOOK",
                        1)
                ],
                null));

        // Act
        var confirmedOrder =
            await service.Confirm(order.Id);

        // Assert
        Assert.Equal(
            OrderStatus.Confirmed,
            confirmedOrder.Status);

        emailSenderMock.Verify(
            sender => sender.Send(
                order.CustomerEmail,
                order.Id),
            Times.Once);
    }
}