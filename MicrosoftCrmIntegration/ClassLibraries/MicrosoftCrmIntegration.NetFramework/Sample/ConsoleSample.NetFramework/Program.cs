using System;
using System.Threading.Tasks;
using MicrosoftCrmIntegration.NetFramework;

namespace ConsoleSample.NetFramework
{
    internal static class Program
    {
        private static async Task<int> Main()
        {
            var resourceUrl = Environment.GetEnvironmentVariable("DATAVERSE_URL") ?? "https://yourorg.crm.dynamics.com";
            var tenantId = Environment.GetEnvironmentVariable("DATAVERSE_TENANT_ID") ?? "REPLACE_WITH_YOUR_TENANT_ID";
            var clientId = Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_ID") ?? "REPLACE_WITH_YOUR_CLIENT_ID";
            var clientSecret = Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_SECRET") ?? "REPLACE_WITH_YOUR_CLIENT_SECRET";

            var client = new DataverseClient(resourceUrl, tenantId, clientId, clientSecret);

            // GET (list) - a handful of active accounts.
            var list = await client.ListAsync("accounts", "$select=name,accountnumber&$filter=statecode eq 0&$top=5");
            Console.WriteLine("[List accounts] HTTP {0}", list.StatusCode);
            Console.WriteLine(list.Body);

            // POST (create) - request body is exact Dataverse column/value JSON.
            const string newContactJson = @"{
                ""firstname"": ""Jane"",
                ""lastname"": ""Doe"",
                ""emailaddress1"": ""jane.doe@example.com""
            }";
            var created = await client.CreateAsync("contacts", newContactJson);
            Console.WriteLine("[Create contact] HTTP {0}, new id: {1}", created.StatusCode, created.CreatedEntityId);
            Console.WriteLine(created.Body);

            // The rest (GetAsync/UpdateAsync/DeleteAsync) all need a real id from your
            // environment - shown here as a reference, not run against a live id:
            //
            // var one = await client.GetAsync("contacts", someContactId, "$select=fullname,emailaddress1");
            // var updated = await client.UpdateAsync("contacts", someContactId, @"{ ""lastname"": ""Smith"" }");
            // var deleted = await client.DeleteAsync("contacts", someContactId);

            return 0;
        }
    }
}
