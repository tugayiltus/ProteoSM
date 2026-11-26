using Proteone.Domain.Notifications;

namespace Proteone.Domain.Tests;

public sealed class TemplateRendererTests
{
    [Fact]
    public void Render_replaces_known_variables_with_given_values()
    {
        var values = new Dictionary<string, string> { ["ticketNumber"] = "INC-1", ["subject"] = "Arıza" };

        var result = TemplateRenderer.Render("[{{ticketNumber}}] {{subject}}", values);

        Assert.Equal("[INC-1] Arıza", result);
    }

    [Fact]
    public void Render_treats_spacing_variants_of_the_same_placeholder_identically()
    {
        var values = new Dictionary<string, string> { ["ticketNumber"] = "INC-1" };

        Assert.Equal("INC-1", TemplateRenderer.Render("{{ticketNumber}}", values));
        Assert.Equal("INC-1", TemplateRenderer.Render("{{ ticketNumber }}", values));
        Assert.Equal("INC-1", TemplateRenderer.Render("{{ticketNumber }}", values));
        Assert.Equal("INC-1", TemplateRenderer.Render("{{ ticketNumber}}", values));
    }

    [Fact]
    public void Render_leaves_a_missing_value_as_empty_text()
    {
        var result = TemplateRenderer.Render("Hello {{recipientName}}!", new Dictionary<string, string>());

        Assert.Equal("Hello !", result);
    }

    [Fact]
    public void Render_does_not_recursively_expand_a_replacement_value_that_itself_contains_a_placeholder()
    {
        // A naive "replace, then re-scan the result" implementation would expand {{ticketNumber}} a second time.
        var values = new Dictionary<string, string> { ["subject"] = "{{ticketNumber}}", ["ticketNumber"] = "INC-1" };

        var result = TemplateRenderer.Render("{{subject}}", values);

        Assert.Equal("{{ticketNumber}}", result);
    }

    [Fact]
    public void Validate_returns_no_bad_names_when_every_variable_is_allowed_for_the_event()
    {
        var badNames = TemplateRenderer.Validate("[{{ticketNumber}}] {{subject}} - {{status}}", NotificationEventTypes.TicketCreated);

        Assert.Empty(badNames);
    }

    [Fact]
    public void Validate_flags_a_completely_unknown_variable_name()
    {
        var badNames = TemplateRenderer.Validate("{{doesNotExist}}", NotificationEventTypes.TicketCreated);

        Assert.Equal(["doesNotExist"], badNames);
    }

    [Fact]
    public void Validate_flags_a_known_variable_not_allowed_for_this_event()
    {
        var badNames = TemplateRenderer.Validate("{{actionUrl}}", NotificationEventTypes.TicketCreated);

        Assert.Equal(["actionUrl"], badNames);
    }

    [Fact]
    public void Validate_treats_spacing_variants_the_same_as_the_plain_form()
    {
        Assert.Equal(["actionUrl"], TemplateRenderer.Validate("{{ actionUrl }}", NotificationEventTypes.TicketCreated));
    }

    [Fact]
    public void Validate_reports_each_bad_name_once_even_if_it_appears_twice()
    {
        var badNames = TemplateRenderer.Validate("{{doesNotExist}} and {{doesNotExist}} again", NotificationEventTypes.TicketCreated);

        Assert.Equal(["doesNotExist"], badNames);
    }

    [Theory]
    [InlineData("{{ticket_number}}", "ticket_number")]
    [InlineData("{{ticket-number}}", "ticket-number")]
    [InlineData("{{ticketNumber.x}}", "ticketNumber.x")]
    [InlineData("{{1abc}}", "1abc")]
    public void Validate_flags_a_placeholder_that_is_not_a_valid_variable_name(string text, string expectedBadName)
    {
        var badNames = TemplateRenderer.Validate(text, NotificationEventTypes.TicketCreated);

        Assert.Equal([expectedBadName], badNames);
    }

    [Fact]
    public void Render_drops_a_line_whose_only_variable_is_line_optional_and_empty()
    {
        var empty = new Dictionary<string, string> { ["ticketUrl"] = "U", ["ratingUrl"] = string.Empty };

        Assert.Equal("a U\nU\nend", TemplateRenderer.Render("a {{ticketUrl}}\n{{ticketUrl}}\nLabel: {{ratingUrl}}\nend", empty));
        Assert.Equal("U\nend", TemplateRenderer.Render("U\nLabel: {{ratingUrl}}\nend", new Dictionary<string, string>()));
    }

    [Fact]
    public void Render_keeps_line_optional_lines_with_a_value_or_another_variable()
    {
        var values = new Dictionary<string, string> { ["ratingUrl"] = "R", ["ticketUrl"] = "T" };
        var empty = new Dictionary<string, string> { ["ratingUrl"] = string.Empty, ["ticketUrl"] = "T" };

        Assert.Equal("Label: R\nend", TemplateRenderer.Render("Label: {{ratingUrl}}\nend", values));
        Assert.Equal("Label: T / \nend", TemplateRenderer.Render("Label: {{ticketUrl}} / {{ratingUrl}}\nend", empty));
    }

    [Fact]
    public void Render_keeps_placeholders_with_line_breaks_inside_the_braces_when_no_line_is_dropped()
    {
        var values = new Dictionary<string, string> { ["subject"] = "Arıza" };

        Assert.Equal("Konu: Arıza", TemplateRenderer.Render("Konu: {{\nsubject\n}}", values));
    }

    [Fact]
    public void Render_does_not_leave_a_trailing_line_break_when_the_dropped_line_was_last()
    {
        var values = new Dictionary<string, string> { ["ticketUrl"] = "U" };

        Assert.Equal("Link: U", TemplateRenderer.Render("Link: {{ticketUrl}}\n\nLabel: {{ratingUrl}}", values));
        Assert.Equal("Link: U\n\n", TemplateRenderer.Render("Link: {{ticketUrl}}\n\nLabel: {{ratingUrl}}\n", values));
    }
}
