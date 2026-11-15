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
    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions) => true;

    [Export("application:configurationForConnectingSceneSession:options:")]
    public UISceneConfiguration ConfigureScene(UIApplication application, UISceneSession session, UISceneConnectionOptions options)
        => new("PDF regression", session.Role) { DelegateType = typeof(PdfSceneDelegate) };
}

[Register("PdfSceneDelegate")]
public sealed class PdfSceneDelegate : UIWindowSceneDelegate
{
    public override UIWindow? Window { get; set; }
    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions options)
    {
        if (scene is not UIWindowScene windowScene) return;
        Window = new UIWindow(windowScene) { RootViewController = new UIViewController() };
        Window.MakeKeyAndVisible();
        BeginInvokeOnMainThread(() =>
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var result = "OK";
            try
            {
                var chat = RecordedChat.Create(File.ReadAllBytes(Path.Combine(NSBundle.MainBundle.BundlePath, "recorded-graph.png")));
                File.WriteAllBytes(Path.Combine(folder, "native.pdf"), IntelligencePdfRenderer.Render(chat));
            }
            catch (Exception error) { result = error.ToString(); }
            File.WriteAllText(Path.Combine(folder, "result.partial"), result);
            File.Move(Path.Combine(folder, "result.partial"), Path.Combine(folder, "result.txt"), overwrite: true);
        });
    }
}
