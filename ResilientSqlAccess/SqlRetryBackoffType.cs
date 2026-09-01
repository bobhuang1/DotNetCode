namespace ResilientSqlAccess
{
    /// <summary>
    /// How the delay between retry attempts grows as attempts increase.
    /// The delay for a given attempt is always based on <see cref="SqlRetryOptions.RetryDelay"/>.
    /// </summary>
    public enum SqlRetryBackoffType
    {
        /// <summary>Every retry waits the same amount of time: <c>RetryDelay</c>.</summary>
        Constant,

        /// <summary>Delay grows linearly with the attempt number: <c>RetryDelay * attempt</c>.
        /// Attempt 1 waits RetryDelay, attempt 2 waits 2x RetryDelay, attempt 3 waits 3x RetryDelay, etc.</summary>
        Linear,

        /// <summary>Delay doubles each attempt: <c>RetryDelay * 2^(attempt - 1)</c>.
        /// Attempt 1 waits RetryDelay, attempt 2 waits 2x RetryDelay, attempt 3 waits 4x RetryDelay, etc.</summary>
        Exponential
    }
}
