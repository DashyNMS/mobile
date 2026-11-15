using Foundation;
using UIKit;
using DashyNMS.Mobile.Platforms.iOS;

internal static class Program
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }
    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        Window = new UIWindow(UIScreen.MainScreen.Bounds) { RootViewController = new UIViewController() };
        Window.MakeKeyAndVisible();
        BeginInvokeOnMainThread(() =>
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            try
            {
                var chat = RecordedChat.Create(File.ReadAllBytes(Path.Combine(NSBundle.MainBundle.BundlePath, "recorded-graph.png")));
                File.WriteAllBytes(Path.Combine(folder, "native.pdf"), IntelligencePdfRenderer.Render(chat));
                File.WriteAllText(Path.Combine(folder, "result.txt"), "OK");
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(folder, "result.txt"), error.ToString()); }
        });
        return true;
    }
}
