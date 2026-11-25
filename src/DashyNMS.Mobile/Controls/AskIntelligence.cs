using System.ComponentModel;
using System.Runtime.CompilerServices;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Controls;

/// <summary>Shared swipe, long-press and screen-reader entry, retaining the row's original connection.</summary>
public static class AskIntelligence
{
    public static readonly BindableProperty EnabledProperty = BindableProperty.CreateAttached("Enabled", typeof(bool), typeof(AskIntelligence), false,
        propertyChanged: (view, _, value) => { if (view is View target && (bool)value) States.GetValue(target, v => new State(v)); });
    public static bool GetEnabled(BindableObject view) => (bool)view.GetValue(EnabledProperty);
    public static void SetEnabled(BindableObject view, bool value) => view.SetValue(EnabledProperty, value);
    private static readonly ConditionalWeakTable<View, State> States = new();

    private sealed class State
    {
        private readonly View view;
        private AskIntelligenceService? service;
        private IntelligenceConversationViewModel? conversation;
        private SessionScope? scope;
        private object? row;
        private readonly SwipeItem swipe = new() { Text = "Ask Intelligence" };
        private readonly AccessibilityActions action;
        private bool watching;
        private bool hasOwnLongPress;
#if !IOS
        private Button? entry;
#endif
        private string? originalHint;
        public State(View view)
        {
            this.view = view;
            action = new(view, "Ask Intelligence", () => { _ = AskAsync(); return true; });
            swipe.Invoked += async (_, _) => await AskAsync();
            view.BindingContextChanged += (_, _) => Capture();
            view.Loaded += (_, _) => Attach();
            view.Unloaded += (_, _) => { if (conversation is not null) conversation.PropertyChanged -= Changed;
                if (row is INotifyPropertyChanged notify) notify.PropertyChanged -= Changed; watching = false; };
            view.HandlerChanged += (_, _) => Attach();
        }
        private void Attach()
        {
            if (view.Handler?.MauiContext?.Services is not { } services) return;
            service = services.GetRequiredService<AskIntelligenceService>();
            conversation = services.GetRequiredService<IntelligenceConversationViewModel>();
            if (!watching) { conversation.PropertyChanged += Changed; watching = true; }
#if !IOS
            if (view is SwipeView host && entry is null && host.Content is { } content)
            {
                entry = new Button { Text = "Ask", MinimumHeightRequest = 48, MinimumWidthRequest = 48,
                    Padding = new Thickness(8, 0), VerticalOptions = LayoutOptions.Center,
                    Style = (Style)Application.Current!.Resources["LinkButton"] };
                SemanticProperties.SetDescription(entry, "Ask Intelligence");
                entry.Clicked += async (_, _) => await AskAsync();
                host.Content = null;
                var rowHost = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], SafeAreaEdges = SafeAreaEdges.None };
                rowHost.Add(content, 0, 0); rowHost.Add(entry, 1, 0);
                host.Content = rowHost;
            }
#endif
            originalHint ??= SemanticProperties.GetHint(view) ?? "";
            Capture();
            if (row is INotifyPropertyChanged notify) { notify.PropertyChanged -= Changed; notify.PropertyChanged += Changed; }
            // Keep existing hold-to-select behaviour; its row still offers the swipe and rotor action.
#if IOS
            var holdTarget = view is SwipeView swipeHost && swipeHost.Content is { } body ? body : view;
            if (!hasOwnLongPress)
            {
                if (LongPress.GetCommand(view) is null && LongPress.GetCommand(holdTarget) is null)
                {
                    LongPress.SetCommand(holdTarget, new Command(async () => await AskAsync(), CanAsk));
                    hasOwnLongPress = true;
                }
            }
#else
            if (view is not SwipeView && LongPress.GetCommand(view) is null)
            {
                LongPress.SetCommand(view, new Command(async () => await AskAsync(), CanAsk));
                hasOwnLongPress = true;
            }
#endif
        }
        private void Capture()
        {
            if (service is null) return;
            if (!ReferenceEquals(row, view.BindingContext))
            {
                if (row is INotifyPropertyChanged old) old.PropertyChanged -= Changed;
                row = view.BindingContext;
                if (row is INotifyPropertyChanged current) current.PropertyChanged += Changed;
                scope = service?.CaptureScope();
            }
            Refresh();
        }
        private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
        private bool CanAsk() => service?.IsAvailable == true && scope is { } captured && service.IsCurrent(captured) && IntelligenceObjectTargets.From(row) is not null;
        private void Refresh()
        {
            var enabled = CanAsk();
            action.Refresh(enabled);
#if !IOS
            if (entry is not null) entry.IsVisible = enabled;
#endif
            if (originalHint is not null)
            {
#if IOS
                var hintSuffix = view is SwipeView
                    ? (hasOwnLongPress ? " Swipe or touch and hold for Ask Intelligence." : " Swipe for Ask Intelligence.")
                    : " Touch and hold to Ask Intelligence.";
#else
                var hintSuffix = view is SwipeView
                    ? " Swipe for Ask Intelligence, or use Ask."
                    : " Touch and hold to Ask Intelligence.";
#endif
                SemanticProperties.SetHint(view, originalHint + (enabled ? hintSuffix : ""));
            }
            if (view is SwipeView host)
            {
                if (enabled && !host.RightItems.Contains(swipe)) host.RightItems.Add(swipe);
                if (!enabled) host.RightItems.Remove(swipe);
            }
        }
        private async Task AskAsync()
        {
            if (!CanAsk() || scope is not { } captured || IntelligenceObjectTargets.From(row) is not { } identity) return;
            if (!await service!.StartAsync(captured, identity))
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Ask Intelligence", "The object could not be checked on this connection, or the chat changed. Your existing chat was kept.", "OK");
        }
    }
}
