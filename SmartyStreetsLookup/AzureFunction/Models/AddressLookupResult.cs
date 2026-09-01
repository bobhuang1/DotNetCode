#nullable enable

namespace SmartyStreetsLookup.AzureFunction.Models
{
    public class AddressLookupResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public int CandidateCount { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }
}
