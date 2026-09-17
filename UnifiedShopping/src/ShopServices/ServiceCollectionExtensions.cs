using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopCommon;
using ShopServices;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShopServiceCollectionExtensions
{
    /// <summary>
    /// Registers all shop services. Payment gateways run in demo mode unless real
    /// credentials are present; add Stripe.net/PayPal SDK bindings in production.
    /// </summary>
    public static IServiceCollection AddShopServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<CartService>();
        services.AddScoped<ProductService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<ShippingService>();
        services.AddScoped<AdminService>();

        // Payment gateways: demo by default, real ones when configured.
        var stripeKey = configuration[StripePaymentGateway.ConfigKey];
        var paypalId = configuration[PayPalPaymentGateway.ConfigKey];
        var paypalSecret = configuration[PayPalPaymentGateway.SecretConfigKey];

        if (!string.IsNullOrWhiteSpace(stripeKey))
        {
            services.AddSingleton<IPaymentGateway>(new StripePaymentGateway(stripeKey));
        }
        else
        {
            services.AddSingleton<IPaymentGateway>(new DemoPaymentGateway(PaymentProvider.Stripe));
        }

        if (!string.IsNullOrWhiteSpace(paypalId) && !string.IsNullOrWhiteSpace(paypalSecret))
        {
            services.AddSingleton<IPaymentGateway>(new PayPalPaymentGateway(paypalId, paypalSecret));
        }
        else
        {
            services.AddSingleton<IPaymentGateway>(new DemoPaymentGateway(PaymentProvider.PayPal));
        }

        // Carriers: mock clients by default; set Carrier:CustomBaseUrl for the custom-carrier client.
        services.AddHttpClient("carriers");
        services.AddScoped<ICarrierGatewayFactory>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("carriers");
            var db = sp.GetRequiredService<ShopData.ShopDbContext>();
            return new CarrierGatewayFactory(db, http);
        });

        return services;
    }
}
