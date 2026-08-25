using RefactoringChallenge.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<OrderStore>();
builder.Services.AddSingleton<OrderService>();
var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

app.MapGet("/api/orders", (OrderService service) => Results.Ok(service.GetAll()));
app.MapGet("/api/orders/{id:int}", (int id, OrderService service) => service.Get(id) is { } order ? Results.Ok(order) : Results.NotFound(new ApiError("Order not found.")));
app.MapPost("/api/orders", (CreateOrderRequest request, OrderService service) =>
{
    try { var order = service.Create(request); return Results.Created($"/api/orders/{order.Id}", order); }
    catch (ArgumentException ex) { return Results.BadRequest(new ApiError(ex.Message)); }
    catch (InvalidOperationException ex) { return Results.Conflict(new ApiError(ex.Message)); }
});
app.MapPost("/api/orders/{id:int}/confirm", async (int id, OrderService service) =>
{
    try { return Results.Ok(await service.Confirm(id)); }
    catch (KeyNotFoundException) { return Results.NotFound(new ApiError("Order not found.")); }
    catch (InvalidOperationException ex) { return Results.Conflict(new ApiError(ex.Message)); }
    catch (HttpRequestException) { return Results.Problem("The confirmation notification could not be sent.", statusCode: 502); }
});
app.MapPost("/api/orders/{id:int}/cancel", (int id, OrderService service) =>
{
    var result = service.Cancel(id);
    if (result == null) return Results.NotFound(new ApiError("Order not found."));
    if (result.Status == "Confirmed") return Results.Conflict(new ApiError("A confirmed order cannot be cancelled."));
    return Results.Ok(result);
});
app.Run();
public partial class Program;
