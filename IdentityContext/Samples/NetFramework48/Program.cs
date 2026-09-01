using System;
using IdentityContext;

namespace ConsoleSample.NetFramework48
{
    internal static class Program
    {
        private static void Main()
        {
            Console.WriteLine($"IsDevMachine() = {DomainIdentity.IsDevMachine()}");

            var windowsLogon = DomainIdentity.GetCurrentWindowsLogonName();
            Console.WriteLine($"GetCurrentWindowsLogonName() = {windowsLogon}");
            Console.WriteLine($"GetCurrentUserLogonName()    = {DomainIdentity.GetCurrentUserLogonName()}");
            Console.WriteLine($"EscapeLogonForUrl(...)       = {DomainIdentity.EscapeLogonForUrl(windowsLogon)}");

            var (domain, userName) = DomainIdentity.SplitDomainLogon(windowsLogon);
            Console.WriteLine($"SplitDomainLogon(...)        = Domain=\"{domain}\", UserName=\"{userName}\"");

            // GetCurrentUserDomainEmail is a best-effort guess unless you configure it for your
            // own organization's domains - shown here with a made-up mapping.
            DomainIdentity.DomainToEmailSuffixMap["CONTOSO"] = "@contoso.example.com";
            Console.WriteLine($"GetCurrentUserDomainEmail()  = {DomainIdentity.GetCurrentUserDomainEmail()}");

            // Web-context methods need an IWebRequestContext. There's no real HttpContext in a
            // console app, so this fakes one - see README.md for real System.Web /
            // Microsoft.AspNetCore.Http adapters to use in an actual web project.
            var fakeRequest = new FakeWebRequestContext
            {
                UserIdentityName = @"CONTOSO\jane.doe",
                ForwardedForHeader = "203.0.113.7, 10.0.0.1",
                RemoteAddress = "10.0.0.1",
                Scheme = "https",
                Authority = "shop.example.com",
                ApplicationPath = "/store"
            };

            Console.WriteLine($"GetClientIpAddress(fake) = {DomainIdentity.GetClientIpAddress(fakeRequest)}");
            Console.WriteLine($"GetSiteBaseUrl(fake)     = {DomainIdentity.GetSiteBaseUrl(fakeRequest)}");
        }

        private sealed class FakeWebRequestContext : IWebRequestContext
        {
            public string? UserIdentityName { get; set; }
            public string? ForwardedForHeader { get; set; }
            public string? RemoteAddress { get; set; }
            public string? Scheme { get; set; }
            public string? Authority { get; set; }
            public string? ApplicationPath { get; set; }
        }
    }
}
