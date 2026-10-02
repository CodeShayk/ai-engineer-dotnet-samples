using Ch03.PromptTemplates;
using Xunit;

namespace Ch03.PromptTests;

/// <summary>Deterministic tests: fast, free and run on every build (Chapter 3.7).</summary>
public sealed class PromptTemplateTests
{
    [Fact]
    public void Render_replaces_all_placeholders()
    {
        var template = new PromptTemplate("test", 1, "", null, "Hello {{name}}, your order is {{orderId}}.");

        string result = template.Render(new Dictionary<string, string>
        {
            ["name"] = "Thomas",
            ["orderId"] = "NW-10249"
        });

        Assert.Equal("Hello Thomas, your order is NW-10249.", result);
    }

    [Fact]
    public void Render_fails_fast_when_a_value_is_missing()
    {
        var template = new PromptTemplate("test", 1, "", null, "Question: {{question}}");

        var ex = Assert.Throws<ArgumentException>(() => template.Render(new Dictionary<string, string>()));
        Assert.Contains("question", ex.Message);
    }

    [Fact]
    public void Every_prompt_in_the_library_declares_its_variables_consistently()
    {
        IReadOnlyList<PromptTemplate> templates = PromptFiles.LoadAll("Prompts");
        Assert.NotEmpty(templates);

        foreach (PromptTemplate template in templates)
        {
            Assert.NotEmpty(template.Name);
            Assert.True(template.Version > 0, $"{template.Name} has an invalid version.");
            Assert.NotEmpty(template.Variables);

            // Rendering with every declared variable must succeed and leave no placeholders behind.
            string rendered = template.Render(template.Variables.ToDictionary(v => v, v => $"<{v}>"));
            Assert.DoesNotContain("{{", rendered);
        }
    }

    [Fact]
    public void Library_returns_the_latest_version_unless_pinned()
    {
        PromptTemplate v1 = new("grounded-answer", 1, "", null, "v1 {{question}}");
        PromptTemplate v2 = new("grounded-answer", 2, "", null, "v2 {{question}}");

        Assert.Equal(2, new PromptLibrary([v1, v2]).Get("grounded-answer").Version);
        Assert.Equal(1, new PromptLibrary([v1, v2], new Dictionary<string, int> { ["grounded-answer"] = 1 }).Get("grounded-answer").Version);
    }
}
