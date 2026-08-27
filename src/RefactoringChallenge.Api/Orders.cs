using System.Collections.Concurrent;
using RefactoringChallenge.Api.Models;

namespace RefactoringChallenge.Api;

public record CreateOrderRequest(
    string CustomerName,
    string CustomerEmail,
    List<CreateOrderItemRequest> Items,
    string? CouponCode);

public record CreateOrderItemRequest(
    string Sku,
    int Quantity);

public record OrderItem(
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record ApiError(string Error);

public class Order
{
    public int Id { get; set; }

    public string CustomerName { get; set; } = "";

    public string CustomerEmail { get; set; } = "";

    public List<OrderItem> Items { get; set; } = [];

    public decimal Subtotal { get; set; }

    public decimal Discount { get; set; }

    public decimal ShippingCost { get; set; }

    public decimal Total { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public DateTime CreatedAtUtc { get; set; }
}

public class Product
{
    public string Sku { get; set; } = "";

    public string Name { get; set; } = "";

    public decimal Price { get; set; }

    public int Stock { get; set; }
}

public class OrderStore
{
    public ConcurrentDictionary<int, Order> Orders { get; } = new();

    public List<Product> Products { get; } =
    [
        new()
        {
            Sku = "BOOK",
            Name = "Clean Code Book",
            Price = 25m,
            Stock = 30
        },
        new()
        {
            Sku = "KEYBOARD",
            Name = "Mechanical Keyboard",
            Price = 50m,
            Stock = 12
        },
        new()
        {
            Sku = "HEADPHONES",
            Name = "USB Headphones",
            Price = 80m,
            Stock = 8
        }
    ];

    public int LastId;
}

public class OrderService
{
    private const int MaximumItemLines = 10;

    private const int MinimumItemQuantity = 1;
    private const int MaximumItemQuantity = 20;

    private const decimal FreeShippingThreshold = 150m;
    private const decimal StandardShippingCost = 12.50m;

    private const string Save10Coupon = "SAVE10";
    private const decimal Save10MinimumSubtotal = 100m;
    private const decimal Save10DiscountRate = 0.10m;

    private const string Vip20Coupon = "VIP20";
    private const decimal Vip20MinimumSubtotal = 200m;
    private const decimal Vip20DiscountRate = 0.20m;

    private readonly OrderStore _store;
    private readonly IEmailSender _emailSender;

    public OrderService(
        OrderStore store,
        IEmailSender emailSender)
    {
        _store = store;
        _emailSender = emailSender;
    }

    public IEnumerable<Order> GetAll()
    {
        return _store.Orders.Values
            .OrderBy(order => order.Id);
    }

    public Order? Get(int id)
    {
        _store.Orders.TryGetValue(id, out var order);

        return order;
    }

    public Order Create(CreateOrderRequest request)
    {
        ValidateRequest(request);

        var resolvedItems = ResolveOrderItems(request.Items);

        var subtotal = CalculateSubtotal(resolvedItems);

        var discount = CalculateDiscount(
            request.CouponCode,
            subtotal);

        var shippingCost = CalculateShipping(subtotal);

        UpdateStock(resolvedItems);

        var order = BuildOrder(
            request,
            resolvedItems,
            subtotal,
            discount,
            shippingCost);

        _store.Orders[order.Id] = order;

        return order;
    }

    public async Task<Order> Confirm(int id)
    {
        if (!_store.Orders.TryGetValue(id, out var order))
        {
            throw new KeyNotFoundException();
        }

        if (order.Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only pending orders can be confirmed.");
        }

        order.Status = OrderStatus.Confirmed;

        await _emailSender.Send(
            order.CustomerEmail,
            order.Id);

        return order;
    }

    public Order? Cancel(int id)
    {
        if (!_store.Orders.TryGetValue(id, out var order))
        {
            return null;
        }

        if (order.Status is
            OrderStatus.Confirmed or
            OrderStatus.Cancelled)
        {
            return order;
        }

        order.Status = OrderStatus.Cancelled;

        RestoreStock(order.Items);

        return order;
    }

    private static void ValidateRequest(
        CreateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            throw new ArgumentException(
                "Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerEmail) ||
            !request.CustomerEmail.Contains('@'))
        {
            throw new ArgumentException(
                "A valid customer email is required.");
        }

        if (request.Items is null ||
            request.Items.Count == 0)
        {
            throw new ArgumentException(
                "At least one item is required.");
        }

        if (request.Items.Count > MaximumItemLines)
        {
            throw new ArgumentException(
                $"An order cannot contain more than {MaximumItemLines} item lines.");
        }
    }

    private List<ResolvedOrderItem> ResolveOrderItems(
        IEnumerable<CreateOrderItemRequest> requestedItems)
    {
        var resolvedItems = new List<ResolvedOrderItem>();

        foreach (var requestedItem in requestedItems)
        {
            ValidateItem(requestedItem);

            var product = FindProduct(requestedItem.Sku);

            ValidateStock(product, requestedItem.Quantity);

            var lineTotal =
                product.Price * requestedItem.Quantity;

            var orderItem = new OrderItem(
                product.Sku,
                product.Name,
                requestedItem.Quantity,
                product.Price,
                lineTotal);

            resolvedItems.Add(
                new ResolvedOrderItem(
                    product,
                    orderItem));
        }

        return resolvedItems;
    }

    private static void ValidateItem(
        CreateOrderItemRequest requestedItem)
    {
        if (string.IsNullOrWhiteSpace(requestedItem.Sku))
        {
            throw new ArgumentException(
                "Item SKU is required.");
        }

        if (requestedItem.Quantity < MinimumItemQuantity ||
            requestedItem.Quantity > MaximumItemQuantity)
        {
            throw new ArgumentException(
                $"Item quantity must be between " +
                $"{MinimumItemQuantity} and {MaximumItemQuantity}.");
        }
    }

    private Product FindProduct(string sku)
    {
        var product = _store.Products.FirstOrDefault(
            product =>
                product.Sku.Equals(
                    sku,
                    StringComparison.OrdinalIgnoreCase));

        if (product is null)
        {
            throw new ArgumentException(
                $"Product '{sku}' does not exist.");
        }

        return product;
    }

    private static void ValidateStock(
        Product product,
        int requestedQuantity)
    {
        if (product.Stock < requestedQuantity)
        {
            throw new InvalidOperationException(
                $"Insufficient stock for product '{product.Sku}'.");
        }
    }

    private static decimal CalculateSubtotal(
        IEnumerable<ResolvedOrderItem> items)
    {
        return items.Sum(
            item => item.OrderItem.LineTotal);
    }

    private static decimal CalculateDiscount(
        string? couponCode,
        decimal subtotal)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
        {
            return 0m;
        }

        var normalizedCoupon =
            couponCode.ToUpperInvariant();

        return normalizedCoupon switch
        {
            Save10Coupon
                when subtotal >= Save10MinimumSubtotal
                => subtotal * Save10DiscountRate,

            Vip20Coupon
                when subtotal >= Vip20MinimumSubtotal
                => subtotal * Vip20DiscountRate,

            Save10Coupon => 0m,

            Vip20Coupon => 0m,

            _ => throw new ArgumentException(
                "Coupon code is not valid.")
        };
    }

    private static decimal CalculateShipping(
        decimal subtotal)
    {
        return subtotal >= FreeShippingThreshold
            ? 0m
            : StandardShippingCost;
    }

    private static void UpdateStock(
        IEnumerable<ResolvedOrderItem> items)
    {
        foreach (var item in items)
        {
            item.Product.Stock -=
                item.OrderItem.Quantity;
        }
    }

    private void RestoreStock(
        IEnumerable<OrderItem> items)
    {
        foreach (var item in items)
        {
            var product = FindProduct(item.Sku);

            product.Stock += item.Quantity;
        }
    }

    private Order BuildOrder(
        CreateOrderRequest request,
        IEnumerable<ResolvedOrderItem> resolvedItems,
        decimal subtotal,
        decimal discount,
        decimal shippingCost)
    {
        return new Order
        {
            Id = Interlocked.Increment(
                ref _store.LastId),

            CustomerName =
                request.CustomerName.Trim(),

            CustomerEmail =
                request.CustomerEmail.Trim(),

            Items = resolvedItems
                .Select(item => item.OrderItem)
                .ToList(),

            Subtotal = subtotal,

            Discount = discount,

            ShippingCost = shippingCost,

            Total =
                subtotal -
                discount +
                shippingCost,

            Status = OrderStatus.Pending,

            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private sealed record ResolvedOrderItem(
        Product Product,
        OrderItem OrderItem);
}