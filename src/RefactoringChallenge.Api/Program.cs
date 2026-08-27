using System.Text.Json.Serialization;
using RefactoringChallenge.Api;
using RefactoringChallenge.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter());
});

builder.Services.AddSingleton<OrderStore>();
builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddSingleton<OrderService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet(
    "/api/orders",
    (OrderService service) =>
        Results.Ok(service.GetAll()));

app.MapGet(
    "/api/orders/{id:int}",
    (int id, OrderService service) =>
        service.Get(id) is { } order
            ? Results.Ok(order)
            : Results.NotFound(
                new ApiError("Order not found.")));

app.MapPost(
    "/api/orders",
    (CreateOrderRequest request, OrderService service) =>
    {
        try
        {
            var order = service.Create(request);

            return Results.Created(
                $"/api/orders/{order.Id}",
                order);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(
                new ApiError(exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(
                new ApiError(exception.Message));
        }
    });

app.MapPost(
    "/api/orders/{id:int}/confirm",
    async (int id, OrderService service) =>
    {
        try
        {
            var order =
                await service.Confirm(id);

            return Results.Ok(order);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(
                new ApiError("Order not found."));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(
                new ApiError(exception.Message));
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                "The confirmation notification could not be sent.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    });

app.MapPost(
    "/api/orders/{id:int}/cancel",
    (int id, OrderService service) =>
    {
        var order = service.Cancel(id);

        if (order is null)
        {
            return Results.NotFound(
                new ApiError("Order not found."));
        }

        if (order.Status == OrderStatus.Confirmed)
        {
            return Results.Conflict(
                new ApiError(
                    "A confirmed order cannot be cancelled."));
        }

        return Results.Ok(order);
    });

app.Run();

public partial class Program;