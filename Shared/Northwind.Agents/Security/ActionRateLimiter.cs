using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Northwind.Agents.Security;

/// <summary>
/// Function calling middleware that limits how many times each tool can be called in a
/// conversation (Chapter 13.5). It does not depend on the model behaving well: once the limit is
/// reached, the tool is simply not invoked, however the model was persuaded to ask.
/// </summary>
public sealed class ActionRateLimiter(int maxCallsPerToolPerRun)
{
    public async ValueTask<object?> EnforceAsync(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        // Count calls to this tool up to and including the current one. Counting every call in
        // the conversation would also count calls requested in the same response that have not
        // run yet, and block a model that asks for several calls at once too early.
        int callNumber = 0;
        foreach (FunctionCallContent call in context.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>())
        {
            if (call.Name == context.Function.Name)
            {
                callNumber++;
            }

            if (call.CallId == context.CallContent.CallId)
            {
                break;
            }
        }

        if (callNumber > maxCallsPerToolPerRun)
        {
            return $"The {context.Function.Name} action has reached its limit for this conversation. " +
                   "Tell the customer a member of staff will follow up.";
        }

        return await next(context, cancellationToken);
    }
}
