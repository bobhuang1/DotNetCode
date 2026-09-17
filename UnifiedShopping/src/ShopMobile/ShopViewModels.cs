using System.Collections.ObjectModel;
using System.Windows.Input;
using ShopCommon;

#if MAUI
using Microsoft.Maui.Controls;
using Microsoft.Maui.Essentials;
#endif

namespace ShopMobile;

/// <summary>Client-side cart for the app (kept in memory; persist with Preferences/SQLite as needed).</summary>
public static class MobileCart
{
    public static Cart Current { get; } = new();

    public static event Action? Changed;

    public static void Add(VariantSummary variant, ProductSummary product)
    {
        var existing = Current.Items.FirstOrDefault(i => i.VariantId == variant.VariantId);
        if (existing is not null)
        {
            existing.Quantity++;
        }
        else
        {
            Current.Items.Add(new CartItem
            {
                VariantId = variant.VariantId,
                Sku = variant.Sku,
                Name = product.Name,
                Color = variant.Color,
                Size = variant.Size,
                UnitPrice = variant.Price,
                Quantity = 1,
                ImageUrl = product.ImageUrl,
            });
        }

        Changed?.Invoke();
    }
}

public sealed class BrowseViewModel
{
    private readonly ShopApiClient _api = ShopApiClient.CreateDefault();

    public ObservableCollection<ProductRow> Products { get; } = [];

    public ICommand AddToCartCommand { get; }

    public BrowseViewModel()
    {
#if MAUI
        AddToCartCommand = new Command<ProductRow>(async row =>
        {
            if (row.Variants.Count > 0)
            {
                MobileCart.Add(row.Variants[0], row.Product);
                await Application.Current!.MainPage!.DisplayAlertAsync(
                    "Added", $"{row.Product.Name} added to your cart.", "OK");
            }
        });
#else
        AddToCartCommand = new DelegateCommand<ProductRow>(row =>
        {
            if (row.Variants.Count > 0)
            {
                MobileCart.Add(row.Variants[0], row.Product);
            }
        });
#endif

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var products = await _api.GetProductsAsync();
            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(new ProductRow(product));
            }
        }
        catch (Exception)
        {
#if MAUI
            // Demo-mode surface: show a friendly retry message in production code.
            await Application.Current!.MainPage!.DisplayAlertAsync(
                "Offline", "Could not reach the shop API. Is ShopApi running?", "OK");
#endif
        }
    }
}

#if !MAUI
/// <summary>Minimal ICommand so view models compile outside MAUI (demo mode / unit tests).</summary>
public sealed class DelegateCommand<T>(Action<T> execute) : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute((T)parameter!);
#pragma warning disable CS0067 // sample stub: raise when CanExecute becomes dynamic
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
}
#endif

/// <summary>Wraps a ProductSummary for XAML binding (adds display strings).</summary>
public sealed class ProductRow(ProductSummary product)
{
    public ProductSummary Product { get; } = product;

    public string Name => Product.Name;
    public string Material => Product.Material;
    public string PriceText => PricingCalculator.FormatMoney(Product.Price);

    public List<VariantSummary> Variants { get; } = [.. product.Variants];
}

public sealed class CartViewModel
{
    private readonly ShopApiClient _api = ShopApiClient.CreateDefault();

    public ObservableCollection<CartItem> Items { get; } = [];

    public string TotalsText =>
        $"Subtotal {PricingCalculator.FormatMoney(MobileCart.Current.Totals.Subtotal)}\n" +
        $"Total    {PricingCalculator.FormatMoney(MobileCart.Current.Totals.GrandTotal)}";

    public ICommand CheckoutCommand { get; }

    public CartViewModel()
    {
#if MAUI
        CheckoutCommand = new Command(async () => _ = CheckoutAsync());
#else
        CheckoutCommand = new DelegateCommand<object>(_ => _ = CheckoutAsync());
#endif
        MobileCart.Changed += () => _ = RefreshAsync();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        Items.Clear();
        foreach (var item in MobileCart.Current.Items)
        {
            Items.Add(item);
        }

        if (MobileCart.Current.Items.Count > 0)
        {
            try
            {
                MobileCart.Current.Totals = (await _api.QuoteCartAsync(MobileCart.Current)).Totals;
            }
            catch (Exception)
            {
                // Offline: keep last known totals.
            }
        }
    }

    private async Task CheckoutAsync()
    {
        if (MobileCart.Current.Items.Count == 0)
        {
#if MAUI
            await Application.Current!.MainPage!.DisplayAlertAsync("Cart is empty", "Add a scarf first.", "OK");
#endif
            return;
        }

        var result = await _api.CheckoutAsync(new CheckoutRequest(
            MobileCart.Current,
            new Address
            {
                Name = "Sample Buyer",
                Line1 = "1 Demo Street",
                City = "New York",
                State = "NY",
                PostalCode = "10001",
                Country = "US",
            },
            PaymentProvider.Stripe));

#if MAUI
        await Application.Current!.MainPage!.DisplayAlertAsync(
            result.Succeeded ? "Order placed" : "Checkout failed",
            result.Succeeded
                ? $"Order {result.OrderNumber} - total {PricingCalculator.FormatMoney(MobileCart.Current.Totals.GrandTotal)}"
                : result.Error ?? "Unknown error",
            "OK");
#endif
    }
}
