using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Help;

namespace DashyNMS.Mobile.Help;

/// <summary>A page of the guide was shown: its blocks, and the heading to start at.</summary>
public sealed record HelpArticleShown(HelpPage Page, string? Anchor, bool IsNewText);

/// <summary>One page of the guide (#668), drawn by the app from its Markdown.</summary>
public sealed partial class HelpArticleViewModel : ViewModelBase
{
    private readonly HelpGuideService _guides;
    private readonly INavigationService _navigation;
    private readonly ILauncherService _launcher;
    private readonly HelpHistory? _history;
    private SynchronizationContext? _ui;
    private HelpGuide _guide;
    private HelpTopic? _topic;
    private bool _attached;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(IsMissing))]
    private HelpPage? _page;

    public HelpArticleViewModel(HelpGuideService guides, INavigationService navigation, ILauncherService launcher, HelpHistory? history = null)
    {
        _history = history;
        _guides = guides;
        _navigation = navigation;
        _launcher = launcher;
        _guide = guides.Current;
    }

    /// <summary>A page is ready to draw, or the one showing got newer text.</summary>
    public event EventHandler<HelpArticleShown>? Shown;

    public HelpTopic? Topic => _topic;

    public string Title => Page?.Title ?? "Help";

    /// <summary>The guide has no such page and no front page to fall back to.</summary>
    public bool IsMissing => _topic is not null && Page is null;

    /// <summary>Opens a page. One the guide doesn't have opens its front page instead.</summary>
    public void Open(HelpTopic topic)
    {
        _topic = topic;
        if (_history is not null)
        {
            _history.LastRead = topic;
        }

        Show(isNewText: false);
    }

    public void Appearing()
    {
        _ui = SynchronizationContext.Current;
        if (_attached)
        {
            return;
        }

        _attached = true;
        _guides.Changed += OnGuideChanged;

        // It may have changed while this page was under another.
        if (!ReferenceEquals(_guide, _guides.Current))
        {
            Show(isNewText: true);
        }
    }

    public void Disappearing()
    {
        _attached = false;
        _guides.Changed -= OnGuideChanged;
    }

    /// <summary>Where a link in the page goes, for how it's shown and what it's called.</summary>
    public HelpLink Resolve(string target) => Page is { } page ? _guide.ResolveLink(page, target) : new HelpBrokenLink(target);

    /// <summary>An image in the page, or null to show its description instead.</summary>
    public HelpImage? Image(string target) => Page is { } page ? _guide.ResolveImage(page, target) : null;

    /// <summary>A linked page's title, so a link's accessible name says where it goes.</summary>
    public string? TitleOf(HelpPageLink link) => _guide.Find(link.PageId)?.Title;

    /// <summary>Whether a link does anything the app should offer. One that doesn't is only its words.</summary>
    public static bool Opens(HelpLink link)
        => link is HelpPageLink or HelpExternalLink { Address.Scheme: "http" or "https" or "mailto" };

    /// <summary>A link in the page was chosen: another page here, or the browser or email app.</summary>
    [RelayCommand]
    private async Task ActivateAsync(string target)
    {
        switch (Resolve(target))
        {
            case HelpPageLink link when string.Equals(link.PageId, Page?.Id, StringComparison.OrdinalIgnoreCase):
                // A heading on this page: go to it, rather than stack the same page on itself.
                Shown?.Invoke(this, new HelpArticleShown(Page!, link.Anchor, IsNewText: false));
                break;
            case HelpPageLink link:
                await _navigation.OpenHelpAsync(new HelpTopic(link.PageId, link.Anchor));
                break;
            case HelpExternalLink link when Opens(link):
                await _launcher.OpenAsync(link.Address);
                break;
        }
    }

    private void Show(bool isNewText)
    {
        _guide = _guides.Current;
        Page = _topic is null ? null : _guide.Find(_topic.PageId) ?? _guide.Home;
        OnPropertyChanged(nameof(IsMissing));
        if (Page is { } page)
        {
            Shown?.Invoke(this, new HelpArticleShown(page, isNewText ? null : page.FindHeading(_topic?.Anchor)?.Anchor, isNewText));
        }
    }

    private void OnGuideChanged(object? sender, EventArgs e)
    {
        if (_ui is { } ui)
        {
            ui.Post(_ => { if (_attached) Show(isNewText: true); }, null);
        }
        else if (_attached)
        {
            Show(isNewText: true);
        }
    }
}
