using Microsoft.Graph.Models;
using SendingEmailViaMicrosoftGraph.EmailSender;
using Xunit;

namespace GraphEmailSender.Tests;

public sealed class DebugEmailRedirectTests
{
    private const string Redirect = "bob@ibegroup.com";

    private static Recipient R(string address) => new() { EmailAddress = new EmailAddress { Address = address } };

    private static Message Sample(BodyType type, string content) => new()
    {
        Subject = "Invoice 42",
        Body = new ItemBody { ContentType = type, Content = content },
        ToRecipients = [R("customer@example.com"), R("second@example.com")],
        CcRecipients = [R("sales@example.com")],
        BccRecipients = [R("audit@example.com")],
        ReplyTo = [R("reply@example.com")],
    };

    [Fact]
    public void Sends_only_to_the_redirect_address_and_drops_cc_bcc()
    {
        var message = Sample(BodyType.Text, "Hello");

        DebugEmailRedirect.Apply(message, Redirect);

        var to = Assert.Single(message.ToRecipients!);
        Assert.Equal(Redirect, to.EmailAddress!.Address);
        Assert.Empty(message.CcRecipients!);
        Assert.Empty(message.BccRecipients!);
        Assert.Empty(message.ReplyTo!);
    }

    [Fact]
    public void Shows_original_recipients_in_subject_and_text_body()
    {
        var message = Sample(BodyType.Text, "Hello");

        DebugEmailRedirect.Apply(message, Redirect);

        Assert.Equal("[DEBUG to: customer@example.com, second@example.com] Invoice 42", message.Subject);
        Assert.StartsWith(
            "Local debug redirect. Original To: customer@example.com, second@example.com; Cc: sales@example.com; Bcc: audit@example.com",
            message.Body!.Content);
        Assert.EndsWith("Hello", message.Body.Content);
    }

    [Fact]
    public void Html_body_gets_an_encoded_banner()
    {
        var message = Sample(BodyType.Html, "<p>Hi</p>");
        message.ToRecipients = [R("a<b>@example.com")];

        DebugEmailRedirect.Apply(message, Redirect);

        Assert.StartsWith("<p><b>Local debug redirect. Original To: a&lt;b&gt;@example.com;", message.Body!.Content);
        Assert.EndsWith("<p>Hi</p>", message.Body.Content);
    }

    [Fact]
    public async Task Sender_redirects_before_validating_or_sending()
    {
        // The credential refuses to issue a token, so nothing reaches Graph; the message
        // object shows what would have been sent.
        var sender = new SendingEmailViaMicrosoftGraph.EmailSender.GraphEmailSender(new NoTokenCredential(), "sender@example.com")
        {
            DebugRedirectAddress = Redirect,
        };
        var message = Sample(BodyType.Text, "Hello");
        message.CcRecipients = [R("not-an-address")]; // would fail validation if it were still there

        var result = await sender.SendAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Contains("no token in tests", result.Message);
        Assert.Equal(Redirect, Assert.Single(message.ToRecipients!).EmailAddress!.Address);
        Assert.Empty(message.CcRecipients!);
    }

    private sealed class NoTokenCredential : Azure.Core.TokenCredential
    {
        public override Azure.Core.AccessToken GetToken(Azure.Core.TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no token in tests");

        public override ValueTask<Azure.Core.AccessToken> GetTokenAsync(Azure.Core.TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no token in tests");
    }
}
