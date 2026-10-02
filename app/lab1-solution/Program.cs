using System.Diagnostics;
using Workshop.Orders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<OrderTelemetry>();

var app = builder.Build();

app.MapGet("/", (ILogger<Program> logger) =>
{
    logger.LogInformation("Health check requested");
    return Results.Ok(new { status = "ok", service = "workshop-orders" });
});

app.MapPost("/orders", async (CreateOrderRequest request, OrderTelemetry telemetry, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.Customer) || string.IsNullOrWhiteSpace(request.PaymentMethod))
    {
        logger.LogWarning("Invalid order request for customer {Customer}", request.Customer);
        return Results.BadRequest(new { error = "customer and paymentMethod are required" });
    }

    var orderId = Guid.NewGuid().ToString("N")[..8];
    var stopwatch = Stopwatch.StartNew();
    await Task.Delay(Random.Shared.Next(50, 301));
    stopwatch.Stop();
    telemetry.RecordOrder(request.PaymentMethod, stopwatch.Elapsed.TotalMilliseconds);
    logger.LogInformation("Order {OrderId} placed for {Customer}", orderId, request.Customer);
    return Results.Created($"/orders/{orderId}", new { orderId, request.Customer, request.PaymentMethod });
});

app.MapGet("/orders/{id}", (string id, ILogger<Program> logger) =>
{
    if (id.Equals("missing", StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("Order {OrderId} was not found", id);
        return Results.NotFound(new { error = "order not found" });
    }

    logger.LogInformation("Order {OrderId} was retrieved", id);
    return Results.Ok(new { orderId = id, customer = "Ada", paymentMethod = "card" });
});

app.MapGet("/slow", async (ILogger<Program> logger) =>
{
    logger.LogWarning("Slow endpoint requested");
    await Task.Delay(1200);
    return Results.Ok(new { status = "slow but successful" });
});

app.MapGet("/error", (ILogger<Program> logger) =>
{
    logger.LogError("Deliberate error endpoint requested");
    return Results.Problem("A deliberate workshop error", statusCode: StatusCodes.Status500InternalServerError);
});

app.Run();

public sealed record CreateOrderRequest(string Customer, string PaymentMethod);