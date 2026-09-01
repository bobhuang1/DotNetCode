namespace SmartyStreetsLookup.NetCore.Models;

public class AddressLookupRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>Required when using <see cref="SmartyStreetsInternationalAddressClient"/>; unused by the US client.</summary>
    public string Country { get; set; } = string.Empty;
}
