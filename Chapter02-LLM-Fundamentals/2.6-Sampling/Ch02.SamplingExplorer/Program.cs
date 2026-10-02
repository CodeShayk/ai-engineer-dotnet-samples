// Chapter 2, Section 2.6: Temperature and other sampling parameters.
// Sends the same prompt three times at three temperatures and prints the results side by side.

using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 2.6: How temperature reshapes the output", options);

IChatClient chatClient = AIClientFactory.CreateChatClient(options);

const string Prompt = "Write a one-sentence product tagline for the Brewmaster Electric Kettle.";
float[] temperatures = [0.0f, 0.7f, 1.3f];
const int RunsPerTemperature = 3;

foreach (float temperature in temperatures)
{
    SampleConsole.Section($"Temperature {temperature:0.0}");

    for (int run = 1; run <= RunsPerTemperature; run++)
    {
        try
        {
            ChatResponse response = await chatClient.GetResponseAsync(
                Prompt,
                new ChatOptions { Temperature = temperature, MaxOutputTokens = 60 });

            Console.WriteLine($"{run}. {response.Text.Trim()}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Some reasoning models reject sampling parameters such as temperature (Section 2.6).
            Console.WriteLine($"{run}. The model rejected this request: {ex.Message}");
            SampleConsole.Note("If your model does not support temperature, try a non-reasoning chat model for this sample.");
            break;
        }
    }
}

SampleConsole.Note("\nLow temperatures repeat themselves; high temperatures vary, sometimes wildly. " +
                   "Even at 0.0, identical output is likely but never guaranteed.");
