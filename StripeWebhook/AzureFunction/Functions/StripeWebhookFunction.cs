#nullable enable
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using StripeWebhook.AzureFunction.Services;

namespace StripeWebhook.AzureFunction.Functions
{
    /// <summary>
    /// A single generic endpoint that receives Stripe webhook events - this mirrors
    /// how Stripe itself works: you register ONE webhook URL in the Stripe Dashboard
    /// (or via the API) and choose which event types get delivered to it, rather
    /// than having a different URL per event type.
    ///
    /// Verifies the Stripe-Signature header, then dispatches by event-type category
    /// purely for logging/demonstration - no business logic runs here (see README,
    /// "What this does and doesn't do").
    /// </summary>
    public class StripeWebhookFunction(ILogger<StripeWebhookFunction> logger)
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        [Function("StripeWebhook")]
        public async Task<HttpResponseData> RunAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "stripe/webhook")] HttpRequestData req,
            CancellationToken cancellationToken)
        {
            // Authorization.Anonymous is intentional, not an oversight: Stripe's
            // webhook sender can't attach an Azure Functions key, so the
            // Stripe-Signature check below IS this endpoint's authentication -
            // see README, "Authentication."
            var webhookSecret = Environment.GetEnvironmentVariable("StripeWebhookSecret");
            if (string.IsNullOrWhiteSpace(webhookSecret))
            {
                logger.LogError("StripeWebhookSecret is not configured.");
                return await WriteTextResponseAsync(req, HttpStatusCode.InternalServerError, "Server is missing the Stripe webhook secret.");
            }

            // Must be the exact raw bytes Stripe sent - signature verification fails
            // if this is re-serialized/reformatted in any way before hashing.
            var rawBody = await req.ReadAsStringAsync() ?? string.Empty;
            if (string.IsNullOrEmpty(rawBody))
            {
                return await WriteTextResponseAsync(req, HttpStatusCode.BadRequest, "Request body is empty.");
            }

            req.Headers.TryGetValues("Stripe-Signature", out var signatureValues);
            var signatureHeader = signatureValues?.FirstOrDefault();

            var (isValid, reason) = StripeSignatureVerifier.Verify(rawBody, signatureHeader, webhookSecret);
            if (!isValid)
            {
                logger.LogWarning("Stripe webhook signature verification failed: {Reason}", reason);
                return await WriteTextResponseAsync(req, HttpStatusCode.BadRequest, $"Signature verification failed: {reason}");
            }

            string eventId;
            string eventType;
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                eventId = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
                eventType = doc.RootElement.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? string.Empty : string.Empty;
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Signature was valid but the body is not valid JSON.");
                return await WriteTextResponseAsync(req, HttpStatusCode.BadRequest, "Body is not valid JSON.");
            }

            logger.LogInformation("Verified Stripe event {EventId} of type {EventType}.", eventId, eventType);
            DispatchByCategory(eventType);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { received = true, id = eventId, type = eventType }, JsonOptions));
            return response;
        }

        // Every branch below is a no-op placeholder marking where you'd plug in your
        // own handling for that event category - see README for the full event list
        // this groups together. This function does not implement any business logic
        // itself, per this repo's "generic, no proprietary code" scope.
        private void DispatchByCategory(string eventType)
        {
            switch (eventType)
            {
                case "charge.succeeded":
                case "charge.failed":
                case "charge.pending":
                case "charge.updated":
                case "charge.captured":
                case "charge.expired":
                    logger.LogInformation("Charge event: {EventType}", eventType);
                    break;

                case "charge.refunded":
                case "refund.created":
                case "refund.updated":
                case "refund.failed":
                case "charge.refund.updated":
                    logger.LogInformation("Refund event: {EventType}", eventType);
                    break;

                case "charge.dispute.created":
                case "charge.dispute.updated":
                case "charge.dispute.closed":
                case "charge.dispute.funds_withdrawn":
                case "charge.dispute.funds_reinstated":
                    logger.LogInformation("Dispute event: {EventType}", eventType);
                    break;

                case "payment_intent.created":
                case "payment_intent.processing":
                case "payment_intent.requires_action":
                case "payment_intent.succeeded":
                case "payment_intent.payment_failed":
                case "payment_intent.canceled":
                case "payment_intent.amount_capturable_updated":
                case "payment_intent.partially_funded":
                    logger.LogInformation("PaymentIntent event: {EventType}", eventType);
                    break;

                case "setup_intent.created":
                case "setup_intent.succeeded":
                case "setup_intent.setup_failed":
                case "setup_intent.canceled":
                case "setup_intent.requires_action":
                    logger.LogInformation("SetupIntent event: {EventType}", eventType);
                    break;

                case "checkout.session.completed":
                case "checkout.session.expired":
                case "checkout.session.async_payment_succeeded":
                case "checkout.session.async_payment_failed":
                    logger.LogInformation("Checkout Session event: {EventType}", eventType);
                    break;

                case "customer.created":
                case "customer.updated":
                case "customer.deleted":
                    logger.LogInformation("Customer event: {EventType}", eventType);
                    break;

                case "customer.subscription.created":
                case "customer.subscription.updated":
                case "customer.subscription.deleted":
                case "customer.subscription.paused":
                case "customer.subscription.resumed":
                case "customer.subscription.trial_will_end":
                case "customer.subscription.pending_update_applied":
                case "customer.subscription.pending_update_expired":
                    logger.LogInformation("Subscription event: {EventType}", eventType);
                    break;

                case "invoice.created":
                case "invoice.finalized":
                case "invoice.finalization_failed":
                case "invoice.paid":
                case "invoice.payment_failed":
                case "invoice.payment_succeeded":
                case "invoice.payment_action_required":
                case "invoice.upcoming":
                case "invoice.updated":
                case "invoice.voided":
                case "invoice.marked_uncollectible":
                case "invoice.sent":
                    logger.LogInformation("Invoice event: {EventType}", eventType);
                    break;

                case "payment_method.attached":
                case "payment_method.detached":
                case "payment_method.updated":
                case "payment_method.automatically_updated":
                    logger.LogInformation("PaymentMethod event: {EventType}", eventType);
                    break;

                case "payout.created":
                case "payout.updated":
                case "payout.paid":
                case "payout.failed":
                case "payout.canceled":
                    logger.LogInformation("Payout event: {EventType}", eventType);
                    break;

                case "product.created":
                case "product.updated":
                case "product.deleted":
                case "price.created":
                case "price.updated":
                case "price.deleted":
                    logger.LogInformation("Product/Price catalog event: {EventType}", eventType);
                    break;

                case "customer.source.created":
                case "customer.source.updated":
                case "customer.source.deleted":
                case "customer.source.expiring":
                    logger.LogInformation("Customer payment source event: {EventType}", eventType);
                    break;

                default:
                    // Not necessarily an error - Stripe adds new event types over
                    // time, and your endpoint's Stripe Dashboard configuration may
                    // send types this sample's switch doesn't enumerate. Acknowledge
                    // with 200 either way (see README) so Stripe doesn't retry.
                    logger.LogInformation("Unhandled/uncategorized Stripe event type: {EventType}", eventType);
                    break;
            }
        }

        private static async Task<HttpResponseData> WriteTextResponseAsync(HttpRequestData req, HttpStatusCode statusCode, string message)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            await response.WriteStringAsync(message);
            return response;
        }
    }
}
