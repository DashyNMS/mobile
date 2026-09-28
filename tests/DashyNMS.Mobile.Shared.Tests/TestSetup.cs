using System.Runtime.CompilerServices;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Tests;

internal static class TestSetup
{
    /// <summary>
    /// Search boxes filter at once in tests, so a test can set SearchText and
    /// check the list straight after. SearchDelayTests covers the pause itself.
    /// </summary>
    [ModuleInitializer]
    internal static void FilterOnEveryKeystroke() => ViewModelBase.DefaultSearchDelay = TimeSpan.Zero;
}
