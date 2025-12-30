using Ai.Tlbx.MiddleManager.Host.Ipc;
using Ai.Tlbx.MiddleManager.Host.Services;

namespace Ai.Tlbx.MiddleManager.Host;

public static class Program
{
    public const string Version = "2.0.0";

    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--version") || args.Contains("-v"))
        {
            Console.WriteLine($"mm-host {Version}");
            return 0;
        }

        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        Console.WriteLine($"mm-host {Version} starting...");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nShutdown requested...");
            cts.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            cts.Cancel();
        };

        try
        {
            using var sessionManager = new SessionManager();

            // Load runAs settings from environment (passed by web server or install script)
            sessionManager.RunAsUser = Environment.GetEnvironmentVariable("MM_RUN_AS_USER");
            sessionManager.RunAsUserSid = Environment.GetEnvironmentVariable("MM_RUN_AS_USER_SID");
            if (int.TryParse(Environment.GetEnvironmentVariable("MM_RUN_AS_UID"), out var uid))
            {
                sessionManager.RunAsUid = uid;
            }
            if (int.TryParse(Environment.GetEnvironmentVariable("MM_RUN_AS_GID"), out var gid))
            {
                sessionManager.RunAsGid = gid;
            }

            await using var server = new SidecarServer(sessionManager);
            await server.StartAsync(cts.Token);

            // Wait until cancelled
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
            }

            Console.WriteLine("Shutting down...");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine($"""
            mm-host {Version} - MiddleManager Terminal Host

            Usage: mm-host [options]

            Options:
              -h, --help       Show this help message
              -v, --version    Show version information

            Environment Variables:
              MM_RUN_AS_USER       Username to run terminals as
              MM_RUN_AS_USER_SID   Windows SID for user de-elevation
              MM_RUN_AS_UID        Unix UID for user de-elevation
              MM_RUN_AS_GID        Unix GID for user de-elevation

            IPC Endpoint:
              {IpcServerFactory.GetEndpointDescription()}

            The host process manages terminal sessions and communicates with
            the mm web server via IPC. It keeps sessions alive across web
            server restarts.
            """);
    }
}
