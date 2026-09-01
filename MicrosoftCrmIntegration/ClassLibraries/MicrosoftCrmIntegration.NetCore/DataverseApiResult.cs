namespace MicrosoftCrmIntegration.NetCore;

/// <summary>
/// The outcome of one call to the Dataverse Web API. <see cref="Body"/> is the
/// raw JSON Dataverse returned (or, on a non-2xx response, Dataverse's own JSON
/// error body) - this client does not parse or reshape it, so you can
/// deserialize it into whatever model fits your app.
/// </summary>
public sealed class DataverseApiResult
{
    public bool IsSuccess { get; init; }

    /// <summary>The HTTP status code Dataverse returned, or 0 if the request never reached Dataverse.</summary>
    public int StatusCode { get; init; }

    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// The new record's id, parsed from the <c>OData-EntityId</c> response
    /// header Dataverse returns on a successful create - null for any other
    /// operation, or if the header was absent.
    /// </summary>
    public Guid? CreatedEntityId { get; init; }

    /// <summary>Set when <see cref="IsSuccess"/> is false; null on success.</summary>
    public string? ErrorMessage { get; init; }
}
