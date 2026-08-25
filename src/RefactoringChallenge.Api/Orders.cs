using System.Collections.Concurrent;

namespace RefactoringChallenge.Api;

public record CreateOrderRequest(string CustomerName, string CustomerEmail, List<CreateOrderItemRequest> Items, string? CouponCode);
public record CreateOrderItemRequest(string Sku, int Quantity);
public record OrderItem(string Sku, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
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
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; }
}

public class Product { public string Sku { get; set; } = ""; public string Name { get; set; } = ""; public decimal Price { get; set; } public int Stock { get; set; } }

public class OrderStore
{
    public ConcurrentDictionary<int, Order> Orders { get; } = new();
    public List<Product> Products { get; } =
    [
        new() { Sku = "BOOK", Name = "Clean Code Book", Price = 25m, Stock = 30 },
        new() { Sku = "KEYBOARD", Name = "Mechanical Keyboard", Price = 50m, Stock = 12 },
        new() { Sku = "HEADPHONES", Name = "USB Headphones", Price = 80m, Stock = 8 }
    ];
    public int LastId;
}

public class EmailSender
{
    public virtual async Task Send(string email, int orderId)
    {
        await Task.Delay(20);
        if (email.EndsWith("@fail.test", StringComparison.OrdinalIgnoreCase)) throw new HttpRequestException("Notification provider unavailable.");
        Console.WriteLine($"Confirmation sent to {email} for order {orderId}");
    }
}

public class OrderService
{
    private readonly OrderStore db;
    public OrderService(OrderStore db) { this.db = db; }
    public IEnumerable<Order> GetAll() { return db.Orders.Values.OrderBy(x => x.Id); }
    public Order? Get(int id) { db.Orders.TryGetValue(id, out var x); return x; }

    public Order Create(CreateOrderRequest x)
    {
        if (string.IsNullOrWhiteSpace(x.CustomerName)) throw new ArgumentException("Customer name is required.");
        if (string.IsNullOrWhiteSpace(x.CustomerEmail) || !x.CustomerEmail.Contains('@')) throw new ArgumentException("A valid customer email is required.");
        if (x.Items == null || x.Items.Count == 0) throw new ArgumentException("At least one item is required.");
        if (x.Items.Count > 10) throw new ArgumentException("An order cannot contain more than 10 item lines.");
        var details = new List<OrderItem>(); decimal amount = 0;
        foreach (var i in x.Items)
        {
            if (string.IsNullOrWhiteSpace(i.Sku)) throw new ArgumentException("Item SKU is required.");
            if (i.Quantity < 1 || i.Quantity > 20) throw new ArgumentException("Item quantity must be between 1 and 20.");
            var p = db.Products.FirstOrDefault(p => p.Sku.Equals(i.Sku, StringComparison.OrdinalIgnoreCase));
            if (p == null) throw new ArgumentException($"Product '{i.Sku}' does not exist.");
            if (p.Stock < i.Quantity) throw new InvalidOperationException($"Insufficient stock for product '{p.Sku}'.");
            var line = p.Price * i.Quantity; amount += line; details.Add(new OrderItem(p.Sku, p.Name, i.Quantity, p.Price, line));
        }
        decimal discount = 0;
        if (!string.IsNullOrWhiteSpace(x.CouponCode))
        {
            if (x.CouponCode.ToUpperInvariant() == "SAVE10" && amount >= 100) discount = amount * 0.10m;
            else if (x.CouponCode.ToUpperInvariant() == "VIP20" && amount >= 200) discount = amount * 0.20m;
            else if (x.CouponCode.ToUpperInvariant() != "SAVE10" && x.CouponCode.ToUpperInvariant() != "VIP20") throw new ArgumentException("Coupon code is not valid.");
        }
        foreach (var i in x.Items) { var p = db.Products.First(p => p.Sku.Equals(i.Sku, StringComparison.OrdinalIgnoreCase)); p.Stock -= i.Quantity; }
        var shipping = amount >= 150 ? 0 : 12.50m;
        var order = new Order { Id = Interlocked.Increment(ref db.LastId), CustomerName = x.CustomerName.Trim(), CustomerEmail = x.CustomerEmail.Trim(), Items = details, Subtotal = amount, Discount = discount, ShippingCost = shipping, Total = amount - discount + shipping, Status = "Pending", CreatedAtUtc = DateTime.UtcNow };
        db.Orders[order.Id] = order; return order;
    }

    public async Task<Order> Confirm(int id)
    {
        if (!db.Orders.TryGetValue(id, out var order)) throw new KeyNotFoundException();
        if (order.Status != "Pending") throw new InvalidOperationException("Only pending orders can be confirmed.");
        order.Status = "Confirmed"; var email = new EmailSender(); await email.Send(order.CustomerEmail, order.Id); return order;
    }

    public Order? Cancel(int id)
    {
        if (!db.Orders.TryGetValue(id, out var order)) return null;
        if (order.Status == "Confirmed") return order;
        if (order.Status == "Cancelled") return order;
        order.Status = "Cancelled";
        foreach (var item in order.Items) { var product = db.Products.First(p => p.Sku == item.Sku); product.Stock += item.Quantity; }
        return order;
    }
}
