#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Graph.Models;

namespace SendingEmailViaMicrosoftGraph.EmailSender
{
    /// <summary>
    /// Local-debug safety net: rewrites a message so it goes only to one test mailbox.
    /// To becomes the redirect address, Cc and Bcc are dropped, and the original
    /// recipients are listed in the subject and at the top of the body so the tester
    /// can see who would have received it.
    ///
    /// This file is shared (linked) by GraphEmailSender.NetCore, GraphEmailSender.NetFramework
    /// and the Azure Function, so every send path in this folder uses the same rules.
    /// </summary>
    public static class DebugEmailRedirect
    {
        public static void Apply(Message message, string redirectAddress)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrWhiteSpace(redirectAddress) || !redirectAddress.Contains("@"))
                throw new ArgumentException("A redirect address is required.", nameof(redirectAddress));

            var to = Describe(message.ToRecipients);
            var cc = Describe(message.CcRecipients);
            var bcc = Describe(message.BccRecipients);

            message.Subject = $"[DEBUG to: {to}] {message.Subject}";

            var note = $"Local debug redirect. Original To: {to}; Cc: {cc}; Bcc: {bcc}";
            message.Body ??= new ItemBody { ContentType = BodyType.Text };
            message.Body.Content = message.Body.ContentType == BodyType.Html
                ? $"<p><b>{WebUtility.HtmlEncode(note)}</b></p><hr />{message.Body.Content}"
                : $"{note}\r\n\r\n{message.Body.Content}";

            message.ToRecipients = new List<Recipient>
            {
                new Recipient { EmailAddress = new EmailAddress { Address = redirectAddress.Trim() } }
            };
            message.CcRecipients = new List<Recipient>();
            message.BccRecipients = new List<Recipient>();
            message.ReplyTo = new List<Recipient>();
        }

        private static string Describe(IEnumerable<Recipient>? recipients)
        {
            var addresses = (recipients ?? Enumerable.Empty<Recipient>())
                .Select(r => r.EmailAddress?.Address?.Trim())
                .Where(a => !string.IsNullOrEmpty(a))
                .ToList();
            return addresses.Count == 0 ? "(none)" : string.Join(", ", addresses);
        }
    }
}
