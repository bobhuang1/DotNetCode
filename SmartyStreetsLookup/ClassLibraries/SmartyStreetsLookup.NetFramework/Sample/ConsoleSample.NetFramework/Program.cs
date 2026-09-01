using System;
using System.Threading.Tasks;
using SmartyStreetsLookup.NetFramework;
using SmartyStreetsLookup.NetFramework.Models;

namespace ConsoleSample.NetFramework
{
    internal static class Program
    {
        private static async Task<int> Main()
        {
            var usAuthId = Environment.GetEnvironmentVariable("SMARTYSTREETS_US_AUTH_ID") ?? "REPLACE_WITH_YOUR_AUTH_ID";
            var usAuthToken = Environment.GetEnvironmentVariable("SMARTYSTREETS_US_AUTH_TOKEN") ?? "REPLACE_WITH_YOUR_AUTH_TOKEN";
            var intlAuthId = Environment.GetEnvironmentVariable("SMARTYSTREETS_INTL_AUTH_ID") ?? "REPLACE_WITH_YOUR_AUTH_ID";
            var intlAuthToken = Environment.GetEnvironmentVariable("SMARTYSTREETS_INTL_AUTH_TOKEN") ?? "REPLACE_WITH_YOUR_AUTH_TOKEN";

            var usClient = new SmartyStreetsUsAddressClient(usAuthId, usAuthToken);
            var usResult = await usClient.LookupAsync(new AddressLookupRequest
            {
                CompanyName = "Acme Corp",
                AddressLine1 = "1600 Amphitheatre Pkwy",
                City = "Mountain View",
                State = "CA",
                PostalCode = "94043"
            });
            Console.WriteLine("[US] IsValid={0} Message={1}", usResult.IsValid, usResult.Message);
            Console.WriteLine("[US] {0}, {1}, {2}, {3} {4}", usResult.AddressLine1, usResult.AddressLine2, usResult.City, usResult.State, usResult.PostalCode);

            var intlClient = new SmartyStreetsInternationalAddressClient(intlAuthId, intlAuthToken);
            var intlResult = await intlClient.LookupAsync(new AddressLookupRequest
            {
                CompanyName = "Royal Mail",
                AddressLine1 = "100 Victoria Embankment",
                City = "London",
                PostalCode = "EC4Y 0HQ",
                Country = "GB"
            });
            Console.WriteLine("[International] IsValid={0} Message={1}", intlResult.IsValid, intlResult.Message);
            Console.WriteLine("[International] {0}, {1}, {2} {3}", intlResult.AddressLine1, intlResult.City, intlResult.PostalCode, intlResult.Country);

            return 0;
        }
    }
}
