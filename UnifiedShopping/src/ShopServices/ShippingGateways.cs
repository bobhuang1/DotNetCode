using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShopCommon;
using ShopData;

namespace ShopServices;

/// <summary>
/// One seam every carrier plugs into: rate quotes, label purchase, tracking.
/// The sample ships a deterministic mock; real deployments implement this per carrier.
/// </summary>
public interface ICarrierClient
{
    CarrierCode Code { get; }
    Task<IReadOnlyList<ShippingRate>> QuoteAsync(RateRequest request, CarrierSetting settings, CancellationToken ct);
    Task<LabelResult> CreateLabelAsync(LabelRequest request, Address shipTo, decimal declaredValue, CarrierSetting settings, CancellationToken ct);
    Task<IReadOnlyList<TrackingUpdate>> GetTrackingAsync(string trackingNumber, CarrierSetting settings, CancellationToken ct);
}

/// <summary>
/// Deterministic mock carrier used for local development, demos, and tests.
/// Rates and tracking progress are computed from the input so behavior is reproducible.
/// </summary>
public sealed class MockCarrierClient(CarrierCode code) : ICarrierClient
{
    public CarrierCode Code { get; } = code;

    public Task<IReadOnlyList<ShippingRate>> QuoteAsync(RateRequest request, CarrierSetting settings, CancellationToken ct)
    {
        var kg = Math.Max(0.5m, request.TotalWeightGrams / 1000m);
        var baseRate = settings.FallbackBaseRate;
        var perKg = settings.FallbackPerKgRate;

        var ground = baseRate + (perKg * kg);
        var expedited = (ground * 1.6m) + 4m;

        IReadOnlyList<ShippingRate> rates =
        [
            new ShippingRate(Code, settings.DisplayName, Code switch
            {
                CarrierCode.Ups => "UPS Ground",
                CarrierCode.FedEx => "FedEx Ground",
                CarrierCode.Usps => "USPS Ground Advantage",
                _ => "Standard",
            }, Math.Round(ground, 2), EstimatedDays(request)),
            new ShippingRate(Code, settings.DisplayName, Code switch
            {
                CarrierCode.Ups => "UPS 2nd Day Air",
                CarrierCode.FedEx => "FedEx 2Day",
                CarrierCode.Usps => "USPS Priority Mail",
                _ => "Expedited",
            }, Math.Round(expedited, 2), 2),
        ];

        return Task.FromResult(rates);
    }

    public Task<LabelResult> CreateLabelAsync(LabelRequest request, Address shipTo, decimal declaredValue, CarrierSetting settings, CancellationToken ct)
    {
        // Production shape: POST to the carrier's shipping API (UPS OAuth2 + /shipments,
        // FedEx /ship/v1/shipments, USPS Stamps.com/Endicia) and base64-encode the returned PDF.
        var tracking = Code switch
        {
            CarrierCode.Ups => $"1Z999AA1{Random.Shared.Next(10000000, 99999999)}",
            CarrierCode.FedEx => $"7946{Random.Shared.Next(10000000, 99999999)}",
            CarrierCode.Usps => $"9400 1{Random.Shared.Next(100000000, 999999999)}",
            _ => $"CSTM{DateTime.UtcNow.Ticks % 1_000_000_000:000000000}",
        };

        var labelPdf = $"%PDF-1.4 mock-label {request.OrderId} {tracking}";
        var bytes = Encoding.UTF8.GetBytes(labelPdf);

        return Task.FromResult(new LabelResult(
            Succeeded: true,
            Error: null,
            TrackingNumber: tracking,
            TrackingUrl: BuildTrackingUrl(tracking, settings),
            LabelPdfBase64: Convert.ToBase64String(bytes),
            Cost: Math.Round(settings.FallbackBaseRate + (settings.FallbackPerKgRate * 0.5m), 2)));
    }

    public Task<IReadOnlyList<TrackingUpdate>> GetTrackingAsync(string trackingNumber, CarrierSetting settings, CancellationToken ct)
    {
        // Deterministic "progress" derived from the tracking number so demos are stable.
        var stage = (Math.Abs(trackingNumber.GetHashCode()) % 4) switch
        {
            0 => ShipmentStatus.LabelCreated,
            1 => ShipmentStatus.InTransit,
            2 => ShipmentStatus.OutForDelivery,
            _ => ShipmentStatus.Delivered,
        };

        IReadOnlyList<TrackingUpdate> updates =
        [
            new TrackingUpdate(Code, trackingNumber, stage, "Mock carrier status", DateTime.UtcNow),
        ];
        return Task.FromResult(updates);
    }

    private static int EstimatedDays(RateRequest request) =>
        request.ShipTo.Country == "US" ? 4 : 8;

    public static string BuildTrackingUrl(string trackingNumber, CarrierSetting settings) =>
        string.IsNullOrWhiteSpace(settings.TrackingUrlTemplate)
            ? $"https://example.com/track/{Uri.EscapeDataString(trackingNumber)}"
            : settings.TrackingUrlTemplate.Replace("{0}", Uri.EscapeDataString(trackingNumber));
}

/// <summary>
/// Custom-carrier client: calls any REST endpoint that answers the same JSON shape
/// (quote/label/track), so new carriers are configuration + one small adapter, not a fork.
/// </summary>
public sealed class CustomCarrierClient(HttpClient http) : ICarrierClient
{
    public CarrierCode Code => CarrierCode.Custom;

    public async Task<IReadOnlyList<ShippingRate>> QuoteAsync(RateRequest request, CarrierSetting settings, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync($"{settings.ApiBaseUrl?.TrimEnd('/')}/quote", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ShippingRate>>(cancellationToken: ct) ?? [];
    }

    public async Task<LabelResult> CreateLabelAsync(LabelRequest request, Address shipTo, decimal declaredValue, CarrierSetting settings, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync($"{settings.ApiBaseUrl?.TrimEnd('/')}/label", new { request, shipTo, declaredValue }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LabelResult>(cancellationToken: ct)
            ?? new LabelResult(false, "Empty response from custom carrier", "", "", "", 0m);
    }

    public async Task<IReadOnlyList<TrackingUpdate>> GetTrackingAsync(string trackingNumber, CarrierSetting settings, CancellationToken ct)
    {
        var response = await http.GetAsync($"{settings.ApiBaseUrl?.TrimEnd('/')}/track/{Uri.EscapeDataString(trackingNumber)}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TrackingUpdate>>(cancellationToken: ct) ?? [];
    }
}

/// <summary>Seam for resolving carrier clients - mock in the sample, real APIs in production.</summary>
public interface ICarrierGatewayFactory
{
    Task<ICarrierClient> ResolveAsync(CarrierCode code, string? customName, CancellationToken ct = default);
}

/// <summary>Resolves the right client for a carrier, consulting admin-configured settings.</summary>
public sealed class CarrierGatewayFactory(ShopDbContext db, HttpClient http) : ICarrierGatewayFactory
{
    public async Task<ICarrierClient> ResolveAsync(CarrierCode code, string? customName, CancellationToken ct = default)
    {
        var setting = await db.CarrierSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Code == code && s.IsActive, ct)
            ?? new CarrierSetting { Code = code, DisplayName = customName ?? code.ToString(), FallbackBaseRate = 7m, FallbackPerKgRate = 3m };

        if (code == CarrierCode.Custom && !string.IsNullOrWhiteSpace(setting.ApiBaseUrl))
        {
            return new CustomCarrierClient(http);
        }

        return new MockCarrierClient(code);
    }
}
