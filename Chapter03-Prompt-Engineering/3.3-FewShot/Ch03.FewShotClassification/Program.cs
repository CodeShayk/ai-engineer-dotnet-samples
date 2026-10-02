// Chapter 3, Section 3.3: Few-shot prompting.
// Classifies a small test set with and without examples and compares accuracy.

using Ch03.FewShotClassification;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 3.3: Zero-shot versus few-shot classification", options);

IChatClient chatClient = AIClientFactory.CreateChatClient(options, options.SmallChatDeployment);
var classifier = new TicketClassifier(chatClient);

(string Message, TicketCategory Expected)[] testSet =
[
    ("My parcel was meant to arrive on Tuesday and it's now Friday.", TicketCategory.OrderStatus),
    ("These boots rub my heel. Can I swap them for a bigger size?", TicketCategory.ReturnsAndRefunds),
    ("There's a charge of $18 on my card that I don't recognize.", TicketCategory.Billing),
    ("Is the teak bench okay to leave outside in winter?", TicketCategory.ProductQuestion),
    ("How do I change the email address on my account?", TicketCategory.AccountAndPrivacy),
    ("The courier left my parcel in the rain and the box is soaked. I want my money back.", TicketCategory.ReturnsAndRefunds),
    ("Do you sell gift cards?", TicketCategory.ProductQuestion),
    ("You still haven't refunded me for the jacket I sent back two weeks ago.", TicketCategory.ReturnsAndRefunds),
];

int zeroShotCorrect = 0, fewShotCorrect = 0;

Console.WriteLine($"{"Expected",-18} {"Zero-shot",-18} {"Few-shot",-18} Message");
Console.WriteLine(new string('-', 100));

foreach ((string message, TicketCategory expected) in testSet)
{
    TicketCategory zeroShot = await classifier.ClassifyZeroShotAsync(message);
    TicketCategory fewShot = await classifier.ClassifyAsync(message);

    if (zeroShot == expected) zeroShotCorrect++;
    if (fewShot == expected) fewShotCorrect++;

    Console.WriteLine($"{expected,-18} {Mark(zeroShot, expected),-18} {Mark(fewShot, expected),-18} {message}");
}

Console.WriteLine();
Console.WriteLine($"Zero-shot accuracy: {zeroShotCorrect}/{testSet.Length}");
Console.WriteLine($"Few-shot accuracy:  {fewShotCorrect}/{testSet.Length}");
SampleConsole.Note("Results vary by model. Small models usually benefit most from examples.");

static string Mark(TicketCategory actual, TicketCategory expected) =>
    actual == expected ? actual.ToString() : $"{actual} (x)";
