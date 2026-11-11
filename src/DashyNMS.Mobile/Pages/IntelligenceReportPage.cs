using System.ComponentModel;
using DashyNMS.Mobile.Intelligence;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Report this answer (#574): a modal preview of exactly what the email will
/// carry, with the question and answer editable so names and addresses can be
/// removed first. The fixed action only opens the person's email app; nothing
/// is sent from here. Like web sign-in it stacks a 44pt Cancel above a wrapping
/// title and owns its own safe area, because it's a modal with no glass owner.
/// </summary>
public sealed class IntelligenceReportPage : ContentPage
{
    private readonly IntelligenceReportViewModel _report;
    private readonly Editor _question = new() { AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 48, BackgroundColor = Colors.Transparent };
    private readonly Editor _answer = new() { AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 96, BackgroundColor = Colors.Transparent };
    private readonly VerticalStackLayout _form = new() { Spacing = 8 };
    private readonly Label _status = new() { LineBreakMode = LineBreakMode.WordWrap };
    private readonly Button _close = new() { Text = "Cancel", MinimumHeightRequest = 44, MinimumWidthRequest = 88, HorizontalOptions = LayoutOptions.Start };
    private readonly Button _copy = new() { Text = "Copy report", MinimumHeightRequest = 44, IsVisible = false };
    private readonly Button _open = new() { Text = "Open in email app", MinimumHeightRequest = 48 };
    private string _announced = "";

    public IntelligenceReportPage(IntelligenceReportViewModel report)
    {
        _report = report;
        Title = "Report this answer";
        SafeAreaEdges = SafeAreaEdges.All;
        this.SetAppThemeColor(BackgroundColorProperty, ThemeColour("BackgroundLight"), ThemeColour("BackgroundDark"));

        _close.Style = ResourceStyle("SecondaryButton");
        _copy.Style = ResourceStyle("SecondaryButton");
        SemanticProperties.SetDescription(_close, "Cancel report. Nothing is sent");
        SemanticProperties.SetDescription(_question, "Question to send. Edit to remove names and addresses");
        SemanticProperties.SetDescription(_answer, "Answer to send. Edit to remove names and addresses");
        SemanticProperties.SetDescription(_open, "Open the report in your email app. You send it from there");
        _close.Clicked += async (_, _) => await CloseAsync();
        _open.Clicked += async (_, _) => await OpenAsync();
        _copy.Clicked += async (_, _) => await CopyAsync();
        _question.Text = report.Question;
        _answer.Text = report.Answer;
        _question.TextChanged += (_, e) => { if (!_report.IsExpired) _report.Question = e.NewTextValue ?? ""; };
        _answer.TextChanged += (_, e) => { if (!_report.IsExpired) _report.Answer = e.NewTextValue ?? ""; };

        var title = new Label { Text = "Report this answer", Style = ResourceStyle("PageTitle"), LineBreakMode = LineBreakMode.WordWrap };
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        var reminder = new Label
        {
            Text = "Remove device names, addresses and anything else that identifies your network before you send it.",
            LineBreakMode = LineBreakMode.WordWrap, FontFamily = "BodySemibold",
        };
        reminder.SetAppThemeColor(Label.TextColorProperty, ThemeColour("WarningTextLight"), ThemeColour("WarningTextDark"));
        _form.Children.Add(Caption("Check the report before it leaves the app. It opens in your email app, and nothing is sent until you send it there."));
        _form.Children.Add(reminder);
        _form.Children.Add(Fact("To", report.To));
        _form.Children.Add(Fact("Subject", report.Subject));
        _form.Children.Add(Fact("Reason", report.ReasonText));
        _form.Children.Add(Heading("Question"));
        _form.Children.Add(Field(_question));
        _form.Children.Add(Heading("Answer"));
        _form.Children.Add(Field(_answer));
        _form.Children.Add(Caption("The email contains only the reason, question and answer above, under those headings. No lookup results, other questions or attachments are included."));

        var body = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(16, 0, 16, 16) };
        body.Children.Add(title);
        body.Children.Add(_form);
        body.Children.Add(_status);
        body.Children.Add(_copy);
        var scroll = new ScrollView { Content = body };

        var grid = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)] };
        _close.Margin = new Thickness(16, 8);
        _open.Margin = new Thickness(16, 8, 16, 12);
        grid.Add(_close, 0, 0);
        grid.Add(scroll, 0, 1);
        grid.Add(_open, 0, 2);
        Content = grid;
        Update();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _report.PropertyChanged -= OnReportChanged;
        _report.PropertyChanged += OnReportChanged;
        _report.Attach();
        Update();
    }

    protected override void OnDisappearing()
    {
        _report.PropertyChanged -= OnReportChanged;
        _report.Detach();
        base.OnDisappearing();
    }

    private void OnReportChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void Update()
    {
        if (_report.IsExpired)
        {
            // Clear the editors too, so the previous chat's text isn't left readable.
            _question.Text = _answer.Text = "";
            _form.IsVisible = false;
        }
        _open.IsEnabled = _report.CanCompose && !_report.IsOpened;
        _open.IsVisible = !_report.IsExpired;
        _copy.IsVisible = _report.CanCopy;
        _close.Text = _report.IsOpened || _report.IsExpired ? "Done" : "Cancel";
        SemanticProperties.SetDescription(_close, _report.IsOpened || _report.IsExpired ? "Close report" : "Cancel report. Nothing is sent");
        _status.Text = _report.Status;
        _status.Style = _report.IsOpened ? ResourceStyle("Caption") : ResourceStyle("Error");
        _status.IsVisible = _report.Status.Length > 0;
        // Announce each new outcome once; edits that don't change it stay quiet.
        if (_report.Status.Length > 0 && _report.Status != _announced) SemanticScreenReader.Default.Announce(_report.Status);
        _announced = _report.Status;
    }

    private async Task OpenAsync()
    {
        try { await _report.ComposeAsync(); }
        finally { Update(); }
    }

    private async Task CopyAsync()
    {
        if (_report.CopyText() is not { } text) { Update(); return; }
        try
        {
            await Clipboard.Default.SetTextAsync(text);
            SemanticScreenReader.Default.Announce("Report copied");
        }
        catch { SemanticScreenReader.Default.Announce("Couldn't copy the report"); }
    }

    private async Task CloseAsync()
    {
        _report.Detach();
        if (Navigation.ModalStack.Contains(this)) await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }

    private static View Fact(string name, string value)
    {
        var label = new Label { LineBreakMode = LineBreakMode.WordWrap };
        label.FormattedText = new FormattedString
        {
            Spans = { new Span { Text = name + ": ", FontFamily = "BodySemibold" }, new Span { Text = value } },
        };
        return label;
    }

    private static Label Heading(string text)
    {
        var label = new Label { Text = text, FontFamily = "BodySemibold", Margin = new Thickness(0, 8, 0, 0) };
        SemanticProperties.SetHeadingLevel(label, SemanticHeadingLevel.Level2);
        return label;
    }

    private static Label Caption(string text) => new() { Text = text, Style = ResourceStyle("Caption"), LineBreakMode = LineBreakMode.WordWrap };

    private static Border Field(Editor editor) => new() { Style = ResourceStyle("Field"), Padding = new Thickness(10, 4), Content = editor };

    private static Color ThemeColour(string key) => (Color)Application.Current!.Resources[key];

    private static Style ResourceStyle(string key) => (Style)Application.Current!.Resources[key];
}
