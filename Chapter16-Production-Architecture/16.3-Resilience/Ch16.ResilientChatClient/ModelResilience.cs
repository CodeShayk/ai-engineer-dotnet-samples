using System.ClientModel;
using System.Globalization;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Ch16.Resilience;

/// <summary>
/// The resilience pipeline for one model deployment (Chapter 16.3): an overall time budget,
/// retries with exponential backoff and jitter for transient failures (honoring Retry-After),
/// a circuit breaker that also counts timeouts, and a per-attempt timeout. The defaults are the
/// values from the book; the demo shortens them. Create the deployment's client with
/// AIClientFactory.CreateChatClient(options, sdkRetries: false), so that this is the only retry layer.
/// </summary>
public static class ModelResilience
{
    public static bool IsTransient(Exception ex) =>
        ex is HttpRequestException || ex is ClientResultException { Status: 429 or >= 500 };

    /// <summary>
    /// The delay a rate-limited response asks for, from its retry-after-ms or Retry-After header.
    /// Null means the response did not say, so the retry uses exponential backoff with jitter.
    /// </summary>
    public static TimeSpan? RetryAfter(Exception? ex)
    {
        if (ex is ClientResultException { Status: 429 } rateLimited && rateLimited.GetRawResponse() is { } response)
        {
            if (response.Headers.TryGetValue("retry-after-ms", out string? ms)
                && double.TryParse(ms, CultureInfo.InvariantCulture, out double milliseconds))
            {
                return TimeSpan.FromMilliseconds(milliseconds);
            }

            if (response.Headers.TryGetValue("Retry-After", out string? seconds) && int.TryParse(seconds, out int s))
            {
                return TimeSpan.FromSeconds(s);
            }
        }

        return null;
    }

    public static ResiliencePipeline CreatePipeline(
        TimeSpan? overallTimeout = null,
        TimeSpan? attemptTimeout = null,
        TimeSpan? retryDelay = null,
        int minimumThroughput = 10,
        Action<string>? onEvent = null) =>
        new ResiliencePipelineBuilder()
            .AddTimeout(overallTimeout ?? TimeSpan.FromSeconds(60))                   // Overall budget, including retries
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = retryDelay ?? TimeSpan.FromSeconds(1),
                DelayGenerator = args => ValueTask.FromResult(RetryAfter(args.Outcome.Exception)),
                // Timed-out attempts are not retried: the fallback deployment is a better bet than waiting again.
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransient),
                OnRetry = args =>
                {
                    onEvent?.Invoke($"retry {args.AttemptNumber + 1} after {args.Outcome.Exception?.Message}");
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = minimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                // Timeouts count as failures, so a deployment that hangs is soon skipped altogether.
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransient).Handle<TimeoutRejectedException>(),
                OnOpened = _ =>
                {
                    onEvent?.Invoke("circuit opened");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(attemptTimeout ?? TimeSpan.FromSeconds(25))                   // Per attempt
            .Build();
}
