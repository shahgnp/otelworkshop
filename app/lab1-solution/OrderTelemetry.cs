using System.Diagnostics.Metrics;

namespace Workshop.Orders;

// Native .NET instruments. They are harmless until a listener such as dotnet-counters subscribes.
public sealed class OrderTelemetry
{
    public const string MeterName = "Workshop.Orders";
    private readonly Meter meter = new(MeterName);
    private readonly Counter<long> ordersPlaced;
    private readonly Histogram<double> orderProcessingDuration;

    public OrderTelemetry()
    {
        ordersPlaced = meter.CreateCounter<long>("orders.placed", unit: "{order}", description: "Orders accepted by the API.");
        orderProcessingDuration = meter.CreateHistogram<double>("orders.processing.duration", unit: "ms", description: "Time spent accepting an order.");
    }

    public void RecordOrder(string paymentMethod, double durationMilliseconds)
    {
        ordersPlaced.Add(1, new KeyValuePair<string, object?>("payment.method", paymentMethod));
        orderProcessingDuration.Record(durationMilliseconds, new KeyValuePair<string, object?>("payment.method", paymentMethod));
    }
}