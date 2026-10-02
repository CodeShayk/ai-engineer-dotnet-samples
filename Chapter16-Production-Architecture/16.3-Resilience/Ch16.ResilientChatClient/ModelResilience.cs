using System.ClientModel;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Ch16.Resilience;

/// <summary>
/// The resilience pipeline for one model deployment (Chapter 16.3): an overall time budget,
/// retries with exponential backoff and jitter for transient failures, a circuit breaker and a
/// per-attempt timeout. The defaults are the values from the book; the demo shortens them.
/// </summary>
public static class ModelResilience
{
    public static bool IsTransient(Exception ex) =>
        ex is HttpRequestException || ex is ClientResultException { Status: 429 or >= 500 };

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
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransient),
                OnOpened = _ =>
                {
                    onEvent?.Invoke("circuit opened");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(attemptTimeout ?? TimeSpan.FromSeconds(25))                   // Per attempt
            .Build();
}
