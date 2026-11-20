using Bunit;
using Grimoire_Studio.Components;
using Grimoire_Studio.Pages;
using Grimoire_Studio.Services;

namespace GrimoireStudio.App.Tests;

/// <summary>Bug reports and ideas without the publishing service: they stay in the (fake) browser.</summary>
public class ReportTests : AppTestContext
{
    public ReportTests()
    {
        LoadData();
    }

    [Fact]
    public void ReportPage_Should_SaveTheReportAndListItAsMine_When_Sent()
    {
        var page = Render<ReportPage>();

        page.Find(".report-form input[type=text]").Input("Wydruk ucina ostatnią rolę");
        page.Find(".report-form textarea").Input("Na skrypcie Trouble Brewing brakuje Chochlika.");
        page.Find(".report-form").Submit();

        page.WaitForAssertion(() => Assert.Contains("Dziękujemy!", page.Find(".notice").TextContent));
        var mine = page.Find(".report-list").TextContent;
        Assert.Contains("Wydruk ucina ostatnią rolę", mine);
        Assert.Contains("nowe", mine);
        Assert.Equal(string.Empty, page.Find(".report-form input[type=text]").GetAttribute("value") ?? string.Empty);
    }

    [Fact]
    public void ReportPage_Should_NotSend_When_TitleOrDescriptionMissing()
    {
        var page = Render<ReportPage>();

        page.Find(".report-form input[type=text]").Input("Tylko tytuł");

        Assert.True(page.Find(".report-form button[type=submit]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task ReportsPanel_Should_SaveStatusAndReply_When_AdminAnswers()
    {
        var reports = Service<Reports>();
        var sent = await reports.SubmitAsync(new Report { Id = string.Empty, Kind = Report.Idea, Title = "Ciemny wydruk", Description = "Opcja ciemnego tła." });

        var panel = Render<ReportsPanel>();
        panel.WaitForAssertion(() => Assert.Contains("Ciemny wydruk", panel.Markup));

        panel.Find(".report-answer select").Change(Report.Done);
        panel.Find(".report-answer input").Input("Dodane w ustawieniach wydruku.");
        panel.FindAll(".report-answer button").Single(b => b.TextContent.Trim() == "Zapisz").Click();

        // Closed reports leave the default "Otwarte" list.
        panel.WaitForAssertion(() => Assert.Contains("Brak otwartych zgłoszeń.", panel.Markup));
        var stored = (await reports.GetAllAsync()).Single(r => r.Id == sent.Id);
        Assert.Equal(Report.Done, stored.Status);
        Assert.Equal("Dodane w ustawieniach wydruku.", stored.Reply);
    }

    [Fact]
    public async Task ReportsPanel_Should_RemoveAReport_When_DeleteConfirmed()
    {
        var reports = Service<Reports>();
        await reports.SubmitAsync(new Report { Id = string.Empty, Kind = Report.Bug, Title = "Do usunięcia", Description = "Spam." });
        var panel = Render<ReportsPanel>();
        panel.WaitForAssertion(() => Assert.Contains("Do usunięcia", panel.Markup));

        var delete = () => panel.FindAll(".report-answer button").Single(b => b.TextContent.Contains("Usuń") || b.TextContent.Contains("Na pewno"));
        delete().Click();
        Assert.Contains("Na pewno usunąć?", delete().TextContent);
        delete().Click();

        panel.WaitForAssertion(() => Assert.DoesNotContain("Do usunięcia", panel.Markup));
        Assert.Empty(await reports.GetAllAsync());
    }
}
