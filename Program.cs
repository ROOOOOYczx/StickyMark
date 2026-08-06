using System.Threading;
using Forms = System.Windows.Forms;

namespace StickyMarkNative;

internal static class Program
{
    private const string MutexName = "Local\\StickyMark.SingleInstance";
    private const string ShowEventName = "Local\\StickyMark.ShowMainWindow";

    [STAThread]
    private static void Main(string[] args)
    {
        Forms.Application.EnableVisualStyles();
        Forms.Application.SetCompatibleTextRenderingDefault(false);
        Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
        var startupLaunch = args.Any(arg => string.Equals(arg, "--startup", StringComparison.OrdinalIgnoreCase));

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            if (!startupLaunch)
            {
                try
                {
                    using var signal = EventWaitHandle.OpenExisting(ShowEventName);
                    signal.Set();
                }
                catch
                {
                    // The existing process may be closing.
                }
            }
            return;
        }

        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        using var mainForm = new MainForm(startupLaunch);
        using var context = new StickyMarkApplicationContext(mainForm, showEvent);
        Forms.Application.Run(context);
    }
}

internal sealed class StickyMarkApplicationContext : Forms.ApplicationContext
{
    private readonly MainForm _mainForm;
    private readonly EventWaitHandle _showEvent;
    private readonly Thread _eventThread;
    private volatile bool _stopping;

    public StickyMarkApplicationContext(MainForm mainForm, EventWaitHandle showEvent)
    {
        _mainForm = mainForm;
        _showEvent = showEvent;
        MainForm = mainForm;
        _eventThread = new Thread(WaitForShowSignal) { IsBackground = true, Name = "StickyMark.ShowSignal" };
        _eventThread.Start();

        if (!mainForm.StartMinimizedToTray)
        {
            mainForm.Show();
        }
    }

    private void WaitForShowSignal()
    {
        try
        {
            while (!_stopping && _showEvent.WaitOne())
            {
                if (_mainForm.IsDisposed) break;
                _mainForm.BeginInvoke(_mainForm.ShowFromTray);
            }
        }
        catch (ObjectDisposedException)
        {
            // Application is exiting.
        }
        catch (InvalidOperationException)
        {
            // The form handle has already been destroyed.
        }
    }

    protected override void Dispose(bool disposing)
    {
        _stopping = true;
        if (disposing)
        {
            try { _showEvent.Set(); } catch (ObjectDisposedException) { }
            if (_eventThread.IsAlive) _eventThread.Join(500);
        }
        base.Dispose(disposing);
    }
}
