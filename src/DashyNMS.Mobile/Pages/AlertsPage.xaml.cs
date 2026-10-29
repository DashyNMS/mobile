using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Alerts;
using DesktopNMS.Core.Models;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>The Alerts tab; the dashboard's counts come here with <see cref="Routes.AlertFilterParameter"/>.</summary>
public partial class AlertsPage : ContentPage, IQueryAttributable, IDetailHost
{
	private readonly AlertsViewModel _viewModel;
	private readonly ShortcutReturn _shortcut = new();

	/// <summary>The alert showing beside the list, if any - highlighted while the pane is there.</summary>
	private AlertKey? _shown;

	public AlertsPage(AlertsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading alerts…", "Refreshing alerts…");
		SearchReveal.Attach(List, SearchSlot, Search);
		Split.SplitChanged += (_, _) => _viewModel.SelectKey(Split.IsSplit ? _shown : null);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.AlertFilterParameter, out var value) && value is string kind)
		{
			_shortcut.Arrived();
			_viewModel.ShowOnly(kind);
		}

        if (query.TryGetValue(Routes.AlertGroupKeyParameter, out var group) && AlertTargets.ParseGroup(group.ToString()) is { } key)
        {
            _shortcut.Arrived();
            _viewModel.ShowGroup(key);
        }
        if (query.TryGetValue(Routes.AlertNoticeParameter, out var notice) && notice is string text)
        {
            _shortcut.Arrived();
            _viewModel.ShowNotice(text);
        }

		// Applied once: coming back to the tab later shouldn't reapply it.
		query.Clear();
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		_shortcut.Left(this, args);
	}

	protected override void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		if (_shortcut.OpenedAfresh())
		{
			_viewModel.ShowSavedFilter();
		}
	}

	/// <summary>
	/// An alert tapped with room beside the list (#88) opens there. Acknowledging
	/// it there changes its row here, as acknowledging from the list does.
	/// </summary>
	public bool TryShowDetail(string route, IDictionary<string, object>? parameters)
	{
		if (!Split.IsSplit || route != Routes.AlertDetail || Handler?.MauiContext?.Services is not { } services)
		{
			return false;
		}

		var view = new AlertDetailView(services.GetRequiredService<AlertDetailViewModel>());
		view.Bar.ShowsBack = false;
		view.ViewModel.AlertChanged += (_, change) => _viewModel.ShowChange(change);
		view.Load(parameters);
		Split.Detail = view;
		_shown = parameters?.TryGetValue(Routes.AlertKeyParameter, out var id) == true ? AlertTargets.ParseKey(id.ToString()) : null;
		_viewModel.SelectKey(_shown);
		return true;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		_viewModel.RefreshCommand.Execute(null);
	}
}
