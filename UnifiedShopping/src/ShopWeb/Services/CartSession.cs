using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ShopCommon;

namespace ShopWeb.Services;

/// <summary>Session-backed cart for the web storefront. The MAUI app keeps its cart client-side.</summary>
public sealed class CartSession(ISession session)
{
    private static readonly string Key = "ShopWeb.Cart";

    public Cart Current
    {
        get
        {
            var raw = session.GetString(Key);
            return raw is null ? new Cart() : JsonSerializer.Deserialize<Cart>(raw) ?? new Cart();
        }
    }

    public async Task AddItemAsync(CartItem item, CancellationToken ct = default)
    {
        var cart = Current;
        var existing = cart.Items.FirstOrDefault(i => i.VariantId == item.VariantId);
        if (existing is not null)
        {
            existing.Quantity += item.Quantity;
        }
        else
        {
            cart.Items.Add(item);
        }

        await SaveAsync(cart);
    }

    public async Task UpdateQuantityAsync(int variantId, int quantity)
    {
        var cart = Current;
        var item = cart.Items.FirstOrDefault(i => i.VariantId == variantId);
        if (item is null)
        {
            return;
        }

        if (quantity <= 0)
        {
            cart.Items.Remove(item);
        }
        else
        {
            item.Quantity = quantity;
        }

        await SaveAsync(cart);
    }

    public Task SaveAsync(Cart cart)
    {
        session.SetString(Key, JsonSerializer.Serialize(cart));
        return Task.CompletedTask;
    }

    public Task ClearAsync() => SaveAsync(new Cart());
}
