using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Doorstep;

internal static class InstanceManager
{
    private const string PipeName = "Doorstep.Command";
    private static Mutex? _mutex;

    public static bool TryStart(Action<string> onCommand)
    {
        var created = false;
        try
        {
            _mutex = new Mutex(true, @"Local\Doorstep.SingleInstance", out created);
        }
        catch (AbandonedMutexException)
        {
            created = true;
        }

        if (!created)
        {
            _mutex?.Dispose();
            _mutex = null;
            return false;
        }

        var ready = new ManualResetEventSlim(false);
        var thread = new Thread(() => Listen(onCommand, ready))
        {
            IsBackground = true,
            Name = "Doorstep.Instance"
        };
        thread.Start();
        ready.Wait(TimeSpan.FromSeconds(2));
        return true;
    }

    public static void Signal(string command)
    {
        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
        client.Connect(1500);
        using var writer = new StreamWriter(client) { AutoFlush = true };
        writer.WriteLine(command);
    }

    public static void Release()
    {
        _mutex?.Dispose();
        _mutex = null;
    }

    private static void Listen(Action<string> onCommand, ManualResetEventSlim ready)
    {
        while (true)
        {
            try
            {
                using var server = CreateServer();
                ready.Set();
                server.WaitForConnection();
                using var reader = new StreamReader(server);
                var command = reader.ReadLine()?.Trim();
                if (command is "show" or "test-toast")
                    onCommand(command);
            }
            catch (Exception ex)
            {
                StatusLog.Write("instance pipe: " + ex.Message);
                Thread.Sleep(200);
            }
        }
    }

    private static NamedPipeServerStream CreateServer()
    {
        var security = new PipeSecurity();
        var user = WindowsIdentity.GetCurrent().User;
        if (user != null)
        {
            security.AddAccessRule(new PipeAccessRule(
                user,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));
        }

        return NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.In,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.None,
            inBufferSize: 0,
            outBufferSize: 0,
            security);
    }
}
