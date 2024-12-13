using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Text.Json;

static class Program
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    private class WindowState
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public uint ProcessId { get; set; }
    }

    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [STAThread]
    static void Main(string[] args)
    {
        // Get the target monitor (default to primary monitor)
        int targetMonitorIndex = args.Length > 0 && int.TryParse(args[0], out int index) ? index : Array.FindIndex(Screen.AllScreens, s => s.Primary);
        
        if (targetMonitorIndex >= Screen.AllScreens.Length)
        {
            return;
        }

        Screen targetScreen = Screen.AllScreens[targetMonitorIndex];
        Thread.Sleep(100);

        IntPtr hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return;

        // Get process ID for the window
        GetWindowThreadProcessId(hWnd, out uint processId);
        string stateFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowMaximizer",
            $"window_state_{processId}.json"
        );

        // Get current window placement
        WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
        placement.length = Marshal.SizeOf(placement);
        GetWindowPlacement(hWnd, ref placement);
        bool isMaximized = placement.showCmd == SW_MAXIMIZE;

        if (isMaximized)
        {
            // Restore from saved position if available
            if (File.Exists(stateFile))
            {
                try
                {
                    string json = File.ReadAllText(stateFile);
                    WindowState savedState = JsonSerializer.Deserialize<WindowState>(json);

                    if (savedState.ProcessId == processId)
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                        SetWindowPos(hWnd, IntPtr.Zero,
                            savedState.X, savedState.Y,
                            savedState.Width, savedState.Height,
                            SWP_NOZORDER | SWP_SHOWWINDOW);
                        
                        File.Delete(stateFile);
                        return;
                    }
                }
                catch { }
            }
            
            // If no saved state, just restore
            ShowWindow(hWnd, SW_RESTORE);
        }
        else
        {
            // Save current position before maximizing
            RECT windowRect;
            if (GetWindowRect(hWnd, out windowRect))
            {
                WindowState currentState = new WindowState
                {
                    X = windowRect.left,
                    Y = windowRect.top,
                    Width = windowRect.right - windowRect.left,
                    Height = windowRect.bottom - windowRect.top,
                    ProcessId = processId
                };

                Directory.CreateDirectory(Path.GetDirectoryName(stateFile));
                string json = JsonSerializer.Serialize(currentState);
                File.WriteAllText(stateFile, json);
            }

            // Move and maximize
            SetWindowPos(hWnd, IntPtr.Zero,
                targetScreen.WorkingArea.X,
                targetScreen.WorkingArea.Y,
                0, 0,
                SWP_NOSIZE | SWP_NOZORDER);
            ShowWindow(hWnd, SW_MAXIMIZE);
        }
    }
}
