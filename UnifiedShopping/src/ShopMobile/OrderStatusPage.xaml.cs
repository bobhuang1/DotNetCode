#if MAUI
using ShopCommon;

namespace ShopMobile;

public partial class OrderStatusPage : ContentPage
{
    private readonly ShopApiClient _api = ShopApiClient.CreateDefault();

    public OrderStatusPage()
    {
        InitializeComponent();
    }

    private async void OnLookUpClicked(object? sender, EventArgs e)
    {
        var orderNumber = OrderNumberEntry.Text?.Trim();
        if (string.IsNullOrEmpty(orderNumber))
        {
            return;
        }

        try
        {
            var order = await _api.GetOrderAsync(orderNumber);
            if (order is null)
            {
                ResultLabel.Text = "Order not found.";
                return;
            }

            var shipping = order.Shipments.Count == 0
                ? "Not shipped yet."
                : string.Join("\n", order.Shipments.Select(s => $"{s.CarrierName}: {s.TrackingNumber} ({s.Status})"));

            ResultLabel.Text =
                $"Order {order.OrderNumber} - {order.Status}\n" +
                $"Total {PricingCalculator.FormatMoney(order.GrandTotal)}\n\n" +
                $"Items:\n{string.Join("\n", order.Lines.Select(l => $" {l.Quantity} x {l.Name}"))}\n\n" +
                $"Shipping: {shipping}";
        }
        catch (Exception)
        {
            ResultLabel.Text = "Could not reach the shop API.";
        }
    }
}
#endif
