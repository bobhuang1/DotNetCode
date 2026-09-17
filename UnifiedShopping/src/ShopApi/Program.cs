using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShopCommon;
using ShopData;
using ShopServices;

var builder = WebApplication.CreateBuilder(args);

// --- Data + services (shared with ShopWeb and ShopMobile) ---
builder.Services.AddShopData(builder.Configuration);
builder.Services.AddShopServices(builder.Configuration);

// --- API plumbing ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Entities have bidirectional navigations (Order <-> Shipment); null them out when serializing.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(
            "http://localhost:5080",     // ShopWeb
            "https://localhost:5081",
            "http://localhost:8080",     // MAUI dev handlers
            "https://localhost:8443")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddOutputCache();

// --- Demo auth: replace with Entra ID / Identity + cookies/JWT for production ---
builder.Services.AddAuthentication("Demo")
    .AddScheme<AuthenticationSchemeOptions, DemoAdminAuthHandler>("Demo", _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy
        .AddAuthenticationSchemes("Demo")
        .RequireAuthenticatedUser()
        .AddRequirements(new AdminRequirement()));
});
builder.Services.AddSingleton<IAuthorizationHandler, AdminRequirementHandler>();

var app = builder.Build();

// --- Migrate + seed (SQLite dev convenience; production uses CI/CD migrations) ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
    await db.Database.MigrateAsync();
    await ShopSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseOutputCache();
app.UseAuthentication();
app.UseAuthorization();

// ---------------- Storefront (shared by web + MAUI) ----------------

app.MapGet("/api/products", async (ProductService products, CancellationToken ct) =>
        Results.Ok(await products.GetActiveProductsAsync(ct)))
    .WithSummary("Catalog for the storefront and mobile app (cacheable).")
    .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(30)).Tag("catalog"));

app.MapGet("/api/products/{id:int}", async (int id, ProductService products, CancellationToken ct) =>
        await products.GetProductAsync(id, ct) is { } product ? Results.Ok(product) : Results.NotFound())
    .WithSummary("Single product with variants.");

app.MapPost("/api/cart/quote", async (Cart cart, CartService carts, CancellationToken ct) =>
        Results.Ok(await carts.ComputeTotalsAsync(cart, null, ct)))
    .WithSummary("Server-side cart totals + coupon validation (same shape for web and app).");

app.MapPost("/api/checkout", async (CheckoutRequest request, CheckoutService checkout, CancellationToken ct) =>
    {
        var result = await checkout.PlaceOrderAsync(request, OrderChannel.Web, ct);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    })
    .WithSummary("Place an order: reserves stock, prices server-side, hands off to Stripe/PayPal.");

app.MapPost("/api/checkout/mobile", async (CheckoutRequest request, CheckoutService checkout, CancellationToken ct) =>
    {
        var result = await checkout.PlaceOrderAsync(request, OrderChannel.Mobile, ct);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    })
    .WithSummary("Same checkout from the MAUI app (tags the order channel).");

app.MapGet("/api/orders/{orderNumber}", async (string orderNumber, ShopDbContext db, CancellationToken ct) =>
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.Shipments)
            .Include(o => o.Return)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);
        return order is null ? Results.NotFound() : Results.Ok(order);
    })
    .WithSummary("Customer order lookup: lines, shipments/tracking, return status.");

app.MapGet("/api/carriers/quote", async (string postalCode, string country, decimal subtotal, ShippingService shipping, CancellationToken ct) =>
    {
        var quote = await shipping.QuoteAllCarriersAsync(
            new ShippingQuoteRequest(new Address { PostalCode = postalCode, Country = country }, subtotal, 500), ct);
        return Results.Ok(quote);
    })
    .WithSummary("Live rates from UPS / FedEx / USPS / custom carriers.");

// ---------------- Admin sub-site API ----------------

var admin = app.MapGroup("/api/admin").RequireAuthorization("AdminOnly")
    .WithTags("Admin");

admin.MapGet("/orders", async (ShopDbContext db, string? status, int skip, int take, CancellationToken ct) =>
    {
        var query = db.Orders.AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.Shipments)
            .Include(o => o.Payment)
            .Include(o => o.Return)
            .OrderByDescending(o => o.CreatedUtc)
            .AsQueryable();

        if (Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(o => o.Status == parsed);
        }

        var orders = await query.Skip(skip).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
        return Results.Ok(orders);
    })
    .WithSummary("Paged order queue (optionally filtered by status).");

admin.MapPost("/orders/{id:int}/label", async (int id, LabelRequest request, ShippingService shipping, CancellationToken ct) =>
        Results.Ok(await shipping.CreateLabelAsync(request with { OrderId = id }, ct)))
    .WithSummary("Buy a shipping label from the chosen carrier; stores tracking + PDF.");

admin.MapPost("/shipments/{shipmentId:int}/ship", async (int shipmentId, ShippingService shipping, CancellationToken ct) =>
        await shipping.MarkShippedAsync(shipmentId, ct) ? Results.Ok() : Results.NotFound())
    .WithSummary("Mark shipped: flips order status and settles reserved inventory.");

admin.MapPost("/shipments/{shipmentId:int}/tracking", async (int shipmentId, ShippingService shipping, CancellationToken ct) =>
        Results.Ok(await shipping.SyncTrackingAsync(shipmentId, ct)))
    .WithSummary("Pull the latest tracking status from the carrier.");

admin.MapPost("/orders/{id:int}/return", async (int id, ApproveReturnRequest request, AdminService adminService, CancellationToken ct) =>
        await adminService.ApproveReturnAsync(id, request.Reason, request.Resolution, request.Quantity, ct) is { } r
            ? Results.Ok(r)
            : Results.BadRequest("Order not found or already has a return."))
    .WithSummary("Approve a return (refund or replacement).");

admin.MapPost("/returns/{returnId:int}/complete", async (int returnId, CompleteReturnRequest request, AdminService adminService, CancellationToken ct) =>
        await adminService.CompleteReturnAsync(returnId, request.RefundAmount, request.Restock, request.ClaimReference, ct)
            ? Results.Ok()
            : Results.NotFound())
    .WithSummary("Complete a return: refund via gateway, restock, record claim.");

admin.MapPost("/returns/{returnId:int}/claim", async (int returnId, ClaimRequest request, AdminService adminService, CancellationToken ct) =>
        await adminService.FileInsuranceClaimAsync(returnId, request.ClaimReference, ct)
            ? Results.Ok()
            : Results.NotFound())
    .WithSummary("File a lost-package / insurance claim with the carrier.");

admin.MapGet("/coupons", async (ShopDbContext db, CancellationToken ct) =>
        Results.Ok(await db.Coupons.AsNoTracking().OrderByDescending(c => c.CreatedUtc).Take(100).ToListAsync(ct)))
    .WithSummary("Recent coupons.");

admin.MapPost("/coupons/generate", async (CouponGenerationOptions options, AdminService adminService, CancellationToken ct) =>
        Results.Ok(await adminService.GenerateCouponsAsync(options, ct)))
    .WithSummary("Generate 16-char alphanumeric coupon codes with a percentage discount.");

admin.MapPost("/products/{id:int}/inventory", async (int id, AdjustInventoryRequest request, ProductService products, CancellationToken ct) =>
        await products.AdjustInventoryAsync(id, request.Delta, request.ReorderPoint, ct) ? Results.Ok() : Results.NotFound())
    .WithSummary("Adjust on-hand inventory for a variant.");

admin.MapPost("/products", async (Product product, ProductService products, CancellationToken ct) =>
        Results.Ok(await products.AddProductAsync(product, ct)))
    .WithSummary("Create a product (with variants + inventory).");

admin.MapPut("/products/{id:int}", async (int id, Product updated, ProductService products, CancellationToken ct) =>
        await products.UpdateProductAsync(id, p =>
        {
            p.Name = updated.Name;
            p.Material = updated.Material;
            p.Price = updated.Price;
            p.CompareAtPrice = updated.CompareAtPrice;
            p.IsActive = updated.IsActive;
        }) ? Results.Ok() : Results.NotFound())
    .WithSummary("Update product fields.");

admin.MapGet("/carriers", async (AdminService adminService, CancellationToken ct) =>
        Results.Ok(await adminService.GetCarrierSettingsAsync(ct)))
    .WithSummary("Carrier settings (UPS/FedEx/USPS/custom).");

admin.MapPut("/carriers", async (CarrierSetting setting, AdminService adminService, CancellationToken ct) =>
        Results.Ok(await adminService.UpsertCarrierSettingAsync(setting, ct)))
    .WithSummary("Add or edit a carrier (including custom carriers).");

// ---------------- Payment webhooks ----------------

app.MapPost("/api/webhooks/stripe", async (HttpRequest http, CheckoutService checkout, IConfiguration config, CancellationToken ct) =>
    {
        // Production: verify the Stripe-Signature header before trusting the body.
        var payload = await new StreamReader(http.Body).ReadToEndAsync(ct);
        var (orderNumber, reference) = DemoWebhookParser.ParseStripe(payload, config);
        if (orderNumber is null)
        {
            return Results.BadRequest();
        }

        await checkout.MarkPaidAsync(orderNumber, reference, PaymentProvider.Stripe, ct);
        return Results.Ok();
    })
    .WithSummary("Stripe webhook: signature-verified in production; flips order to Paid.");

app.MapPost("/api/webhooks/paypal", async (HttpRequest http, CheckoutService checkout, CancellationToken ct) =>
    {
        var payload = await new StreamReader(http.Body).ReadToEndAsync(ct);
        var (orderNumber, reference) = DemoWebhookParser.ParsePayPal(payload);
        if (orderNumber is null)
        {
            return Results.BadRequest();
        }

        await checkout.MarkPaidAsync(orderNumber, reference, PaymentProvider.PayPal, ct);
        return Results.Ok();
    })
    .WithSummary("PayPal webhook: flip order to Paid after gateway verification.");

// ---------------- Health (for Front Door / App Service probes) ----------------

app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow }));

app.Run();

// ---------------- request/response shapes ----------------

public sealed record ApproveReturnRequest(string Reason, ReturnResolution Resolution, int Quantity);
public sealed record CompleteReturnRequest(decimal RefundAmount, bool Restock, string? ClaimReference = null);
public sealed record ClaimRequest(string ClaimReference);
public sealed record AdjustInventoryRequest(int Delta, int? ReorderPoint = null);

/// <summary>Demo-only auth: an "X-Admin-Key" header gates the admin API. Swap for real identity.</summary>
public sealed class AdminRequirement : IAuthorizationRequirement;

public sealed class AdminRequirementHandler : AuthorizationHandler<AdminRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        if (context.User.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == "Admin"))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public sealed class DemoAdminAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = Request.Headers["X-Admin-Key"].ToString();
        if (string.IsNullOrEmpty(expected))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Demo comparison only! Production: Entra ID / Identity + real secret storage.
        if (expected == "demo-admin-key")
        {
            var claims = new[] { new Claim(ClaimTypes.Name, "demo-admin"), new Claim(ClaimTypes.Role, "Admin") };
            var identity = new ClaimsIdentity(claims, "Demo");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Demo")));
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid admin key."));
    }
}

/// <summary>Extracts order references from webhook payloads without a vendor SDK (demo mode).</summary>
public static class DemoWebhookParser
{
    public static (string? OrderNumber, string Reference) ParseStripe(string payload, IConfiguration config)
    {
        // Real implementation verifies HMAC per StripeWebhook sample in this repo, then
        // reads event.data.object.metadata.orderNumber for checkout.session.completed.
        if (payload.Contains("\"status\": \"succeeded\"") || payload.Contains("\"status\":\"succeeded\""))
        {
            var start = payload.IndexOf("pi_", StringComparison.Ordinal);
            return ("SO-DEMO", start >= 0 ? payload[start..(start + 24)] : "pi_unknown");
        }
        return (null, "");
    }

    public static (string? OrderNumber, string Reference) ParsePayPal(string payload) =>
        payload.Contains("COMPLETED") ? ("SO-DEMO", "PAYID-DEMO") : (null, "");
}
