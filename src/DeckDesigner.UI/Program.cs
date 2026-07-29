using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;

namespace DeckDesigner.UI;

internal static class Program
{
    // Crash log lives next to the executable and in the temp folder for easy access.
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "crash.log");

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogException(e.ExceptionObject as Exception, "AppDomain.UnhandledException");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogException(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogException(ex, "Main");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    internal static void LogException(Exception? ex, string source)
    {
        if (ex is null)
        {
            return;
        }

        var message = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] ({source}){Environment.NewLine}{ex}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";

        try
        {
            File.AppendAllText(LogPath, message);
        }
        catch
        {
            // Never let logging itself crash the process.
        }

        System.Diagnostics.Trace.WriteLine(message);
    }
}

