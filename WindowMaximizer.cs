using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using System.Diagnostics;

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
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    // Windows-idiomatic structs
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
        public string MonitorDeviceName { get; set; }
    }

    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowMaximizer"
    );

    private static readonly string LogPath = Path.Combine(AppDataPath, "maximizer.log");

    private static void Log(string message)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logMessage = $"[{timestamp}] {message}{Environment.NewLine}";
            Directory.CreateDirectory(AppDataPath);
            File.AppendAllText(LogPath, logMessage);
        }
        catch
        {
            // Fail silently for logging
        }
    }

    private static Screen GetCurrentScreen(IntPtr hWnd)
    {
        IntPtr monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTOPRIMARY);
        foreach (Screen screen in Screen.AllScreens)
        {
            if (screen.DeviceName == screen.DeviceName) // You'd need to get actual monitor handle to compare
            {
                return screen;
            }
        }
        return Screen.PrimaryScreen;
    }

    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Log("Application started");

            int targetMonitorIndex = args.Length > 0 && int.TryParse(args[0], out int index) 
                ? index 
                : Array.FindIndex(Screen.AllScreens, s => s.Primary);
            
            if (targetMonitorIndex >= Screen.AllScreens.Length)
            {
                Log($"Invalid monitor index: {targetMonitorIndex}");
                return;
            }

            Screen targetScreen = Screen.AllScreens[targetMonitorIndex];
            Log($"Available screens:");
            foreach (var screen in Screen.AllScreens)
            {
                Log($"  - {screen.DeviceName}: Primary={screen.Primary}, " +
                    $"Bounds={screen.Bounds}, " +
                    $"WorkingArea={screen.WorkingArea}, " +
                    $"BitsPerPixel={screen.BitsPerPixel}");
            }
            Log($"Target screen selected: {targetScreen.DeviceName} (Bounds: {targetScreen.Bounds})");
            
            Thread.Sleep(100);

            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
            {
                Log("No foreground window found");
                return;
            }

            GetWindowThreadProcessId(hWnd, out uint processId);
            string processName = Process.GetProcessById((int)processId).ProcessName;
            Log($"Found window for process: {processName} (PID: {processId})");

            string stateFile = Path.Combine(AppDataPath, $"window_state_{processId}.json");

            // Get current window placement and screen
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            GetWindowPlacement(hWnd, ref placement);
            Screen currentScreen = GetCurrentScreen(hWnd);

            RECT windowRect;
            bool windowRectObtained = GetWindowRect(hWnd, out windowRect);
            
            Screen actualScreen = null;
            if (windowRectObtained)
            {
                // Get center point of window
                int centerX = windowRect.left + (windowRect.right - windowRect.left) / 2;
                int centerY = windowRect.top + (windowRect.bottom - windowRect.top) / 2;
                
                Log($"Window center point: {centerX},{centerY}");
                
                foreach (Screen screen in Screen.AllScreens)
                {
                    if (centerX >= screen.Bounds.Left && centerX < screen.Bounds.Right &&
                        centerY >= screen.Bounds.Top && centerY < screen.Bounds.Bottom)
                    {
                        actualScreen = screen;
                        Log($"Window is actually on screen: {screen.DeviceName}");
                        break;
                    }
                }
            }

            if (actualScreen == null)
            {
                Log("Could not determine actual screen from coordinates");
                actualScreen = Screen.PrimaryScreen;
            }

            bool isMaximized = placement.showCmd == SW_MAXIMIZE;
            bool isOnTargetScreen = actualScreen.DeviceName == targetScreen.DeviceName;

            Log($"Window state:");
            Log($"  - Actual screen: {actualScreen.DeviceName} (Bounds: {actualScreen.Bounds})");
            Log($"  - Target screen: {targetScreen.DeviceName} (Bounds: {targetScreen.Bounds})");
            Log($"  - Window placement: showCmd={placement.showCmd}, " +
                $"flags={placement.flags}, " +
                $"normalPosition={placement.rcNormalPosition.left},{placement.rcNormalPosition.top}," +
                $"right={placement.rcNormalPosition.right},bottom={placement.rcNormalPosition.bottom}");
            Log($"  - Is maximized: {isMaximized}");
            Log($"  - Is on target screen: {isOnTargetScreen}");
            if (windowRectObtained)
            {
                Log($"  - Current window rect: {windowRect.left},{windowRect.top},{windowRect.right},{windowRect.bottom}");
            }

            // If maximized on wrong screen, first restore it
            if (isMaximized && !isOnTargetScreen)
            {
                Log("Window is maximized on wrong screen - restoring first");
                ShowWindow(hWnd, SW_RESTORE);
                isMaximized = false;
            }

            // Now handle based on state
            if (!isMaximized || !isOnTargetScreen)
            {
                HandleNormalWindow(hWnd, processId, stateFile, targetScreen);
            }
            else if (File.Exists(stateFile))
            {
                Log("Window is maximized on target screen and state file exists - restoring");
                RestoreFromState(hWnd, processId, stateFile);
            }
            else
            {
                Log("Window maximized on target screen - toggling simple restore");
                ShowWindow(hWnd, SW_RESTORE);
                Log("Window restored");
            }
            // if (File.Exists(stateFile))
            // {
            //     Log("Found saved state file - attempting restore");
            //     RestoreFromState(hWnd, processId, stateFile);
            // }
            // Only maximize if we're not already maximized on the target screen
            // else if (!isMaximized || !isOnTargetScreen)
            // {
            //     HandleNormalWindow(hWnd, processId, stateFile, targetScreen);
            // }
            // else
            // {
            //     Log("Window already maximized on target screen - toggling restore");
            //     ShowWindow(hWnd, SW_RESTORE);
            //     Log("Window restored");
            // }
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private static void RestoreFromState(IntPtr hWnd, uint processId, string stateFile)
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
                Log("Window restored to saved position");
            }
            else
            {
                Log("Saved state was for different process - performing normal restore");
                ShowWindow(hWnd, SW_RESTORE);
            }
        }
        catch (Exception ex)
        {
            Log($"Error restoring window state: {ex.Message}");
            ShowWindow(hWnd, SW_RESTORE);
        }
    }

    private static void HandleNormalWindow(IntPtr hWnd, uint processId, string stateFile, Screen targetScreen)
    {
        Log("Handling window maximization");
        
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
                ProcessId = processId,
                MonitorDeviceName = GetCurrentScreen(hWnd).DeviceName
            };

            Directory.CreateDirectory(Path.GetDirectoryName(stateFile));
            string json = JsonSerializer.Serialize(currentState);
            File.WriteAllText(stateFile, json);
            Log("Saved window state");
        }

        // Move and maximize
        SetWindowPos(hWnd, IntPtr.Zero,
            targetScreen.WorkingArea.X,
            targetScreen.WorkingArea.Y,
            0, 0,
            SWP_NOSIZE | SWP_NOZORDER);
        ShowWindow(hWnd, SW_MAXIMIZE);
        Log($"Window maximized on screen: {targetScreen.DeviceName}");
    }
}
