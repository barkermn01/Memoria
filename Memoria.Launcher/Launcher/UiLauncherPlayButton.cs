﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Memoria.Launcher
{
    public sealed class UiLauncherPlayButton : UiLauncherButton
    {
        public SettingsGrid_Vanilla GameSettings { get; set; }
        public SettingsGrid_VanillaDisplay GameSettingsDisplay { get; set; }

        public UiLauncherPlayButton()
        {
            SetResourceReference(LabelProperty, "Launcher.Launch");
        }

        protected override async Task DoAction()
        {
            SetResourceReference(LabelProperty, "Launcher.Launching");

            ApplyDebugSettingsSafe();

            int monitor = GetActiveMonitorIndex();
            if (monitor < 0 || DisplayInfo.Displays == null || monitor >= DisplayInfo.Displays.Count)
            {
                MessageBox.Show((Window)this.GetRootElement(), $"Selected monitor ({monitor}) does not appear available.\nDisplaying to monitor 0.", "Information", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                monitor = 0;
            }

            GetScreenResolution(out int width, out int height, monitor);

            String workingDirectory = Path.GetFullPath(".\\" + (GameSettings.IsX64 ? "x64" : "x86"));
            String executablePath = PrepareExecutableAndData(workingDirectory);
            String arguments = $"-runbylauncher -single-instance -monitor {monitor.ToString(CultureInfo.InvariantCulture)} -screen-width {width.ToString(CultureInfo.InvariantCulture)} -screen-height {height.ToString(CultureInfo.InvariantCulture)} -screen-fullscreen {(GameSettingsDisplay.WindowMode == 1 ? "1" : "0")} {(GameSettingsDisplay.WindowMode >= 2 ? "-popupwindow" : "")}";

            SetResourceReference(LabelProperty, "Launcher.Launch");
            StartGameProcess(executablePath, arguments);

            Application.Current.Shutdown();
        }

        // Try to update debug ini settings. Ignore exceptions.
        private void ApplyDebugSettingsSafe()
        {
            try
            {
                IniFile iniFile = IniFile.MemoriaIni;
                if (LaunchModelViewer)
                {
                    iniFile.SetSetting("Debug", "Enabled", "1");
                    iniFile.SetSetting("Debug", "StartModelViewer", "1");
                }
                else
                {
                    iniFile.SetSetting("Debug", "StartModelViewer", "0");
                }
                iniFile.Save();
            }
            catch { }
        }

        // Parse the selected monitor index from settings.
        private int GetActiveMonitorIndex()
        {
            if (!string.IsNullOrEmpty(GameSettingsDisplay?.ActiveMonitor))
            {
                int spaceIndex = GameSettingsDisplay.ActiveMonitor.IndexOf(' ');
                if (spaceIndex > 0)
                {
                    string num = GameSettingsDisplay.ActiveMonitor.Substring(0, spaceIndex);
                    if (int.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res))
                        return res;
                }
            }
            return -1;
        }

        // Get the screen resolution
        private void GetScreenResolution(out int width, out int height, int monitor)
        {
            width = height = 0;
            string res = IniFile.SettingsIni.GetSetting("Settings", "ScreenResolution", GameSettingsDisplay.ScreenResolution);
            if (!string.IsNullOrWhiteSpace(res))
            {

                var resString = res.Split(' ')[0];
                var strArray = resString.Split('x');
                if (strArray.Length < 2
                    || !int.TryParse(strArray[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
                    || !int.TryParse(strArray[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height))
                {
                    width = height = 0;
                }
            }

            // Ensure we have monitor information
            if (monitor < 0 || DisplayInfo.Displays == null || monitor >= DisplayInfo.Displays.Count)
            {
                if (width == 0 || height == 0)
                {
                    // Couldn't get any display information, default to 1080p
                    width = 1920;
                    height = 1080;
                }
                return;
            }

            // Adjust the screen size to the monitor
            var display = DisplayInfo.Displays[monitor];

            int monitorWidth = display.monitorArea.right - display.monitorArea.left;
            int monitorHeight = display.monitorArea.bottom - display.monitorArea.top;

            if (GameSettingsDisplay.WindowMode == 2 || width == 0 || height == 0)
            {
                width = monitorWidth;
                height = monitorHeight;
            }
            else
            {
                width = Math.Min(width, monitorWidth);
                height = Math.Min(height, monitorHeight);
            }
        }

        // Handles Unity/Debug shenanigans, returns the executable path to run.
        private string PrepareExecutableAndData(string workingDirectory)
        {
            string executablePath = Path.Combine(workingDirectory, "FF9.exe");
            if (GameSettings.IsDebugMode)
            {
                string unityPath = Path.Combine(workingDirectory, "Unity.exe");

                // Copy Unity.exe if missing or outdated.
                if (!File.Exists(unityPath) || !IsFileIdentical(unityPath, executablePath))
                {
                    File.Copy(executablePath, unityPath, true);
                    File.SetLastWriteTimeUtc(unityPath, File.GetLastWriteTimeUtc(executablePath));
                }
                executablePath = unityPath;

                string ff9DataPath = Path.Combine(workingDirectory, "FF9_Data");
                string unityDataPath = Path.Combine(workingDirectory, "Unity_Data");

                if (!Directory.Exists(unityDataPath))
                {
                    JunctionPoint.Create(unityDataPath, ff9DataPath, true);
                }
                else
                {
                    try
                    {
                        // Check directory accessibility.
                        foreach (string item in Directory.EnumerateFileSystemEntries(unityDataPath))
                            break;
                    }
                    catch
                    {
                        JunctionPoint.Delete(unityDataPath);
                        JunctionPoint.Create(unityDataPath, ff9DataPath, true);
                    }
                }
            }
            return executablePath;
        }

        // Compare files by length and last write time.
        private bool IsFileIdentical(string path1, string path2)
        {
            FileInfo f1 = new FileInfo(path1);
            FileInfo f2 = new FileInfo(path2);
            return f1.Length == f2.Length && f1.LastWriteTimeUtc == f2.LastWriteTimeUtc;
        }

        // Launch the game process with given args.
        private void StartGameProcess(string exePath, string args)
        {
            ProcessStartInfo gameStartInfo = new ProcessStartInfo(exePath, args) { UseShellExecute = false };
            if (GameSettings.IsDebugMode)
                gameStartInfo.EnvironmentVariables["UNITY_GIVE_CHANCE_TO_ATTACH_DEBUGGER"] = "1";

            Process gameProcess = new Process { StartInfo = gameStartInfo };
            gameProcess.Start();

            if (!GameSettings.IsDebugMode)
                return;

            Process debuggerProcess = Process.GetProcesses().FirstOrDefault(p => p.ProcessName.StartsWith("Memoria.Debugger"));
            if (debuggerProcess != null)
                return;

            try
            {
                String debuggerDirectory = Path.Combine(Path.GetFullPath("Debugger"), (GameSettings.IsX64 ? "x64" : "x86"));
                String debuggerPath = Path.Combine(debuggerDirectory, "Memoria.Debugger.exe");
                String debuggerArgs = "10000"; // Timeout: 10 seconds
                if (Directory.Exists(debuggerDirectory) && File.Exists(debuggerPath))
                {
                    ProcessStartInfo debuggerStartInfo = new ProcessStartInfo(debuggerPath, debuggerArgs) { WorkingDirectory = debuggerDirectory };
                    debuggerProcess = new Process { StartInfo = debuggerStartInfo };
                    debuggerProcess.Start();
                }
            }
            catch { }
        }

        internal static async Task<Boolean> CheckUpdates(Window rootElement, ManualResetEvent cancelEvent, SettingsGrid_Vanilla gameSettings)
        {
            String applicationDirectory = Path.GetFullPath("./");
            String applicationPath = Path.Combine(applicationDirectory, Path.GetFileName(Assembly.GetExecutingAssembly().Location));
            LinkedList<HttpFileInfo> updateInfo = await FindUpdatesInfo(applicationDirectory, cancelEvent, gameSettings);
            if (updateInfo.Count == 0)
                return false;

            StringBuilder messageSb = new StringBuilder(256);
            messageSb.AppendLine((String)Lang.Res["Launcher.NewVersionIsAvailable"]);
            Int64 size = 0;
            foreach (HttpFileInfo info in updateInfo)
            {
                size += info.ContentLength;
                messageSb.AppendLine($"{info.TargetName} - {info.LastModified} ({UiProgressWindow.FormatValue(info.ContentLength)})");
            }

            if (MessageBox.Show(rootElement, messageSb.ToString(), (String)Lang.Res["Launcher.QuestionTitle"], MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                List<String> success = new List<String>(updateInfo.Count);
                List<String> failed = new List<String>();

                using (UiProgressWindow progress = new UiProgressWindow("Downloading...")) // TODO language?
                {
                    progress.SetTotal(size);
                    progress.Show();

                    Downloader downloader = new Downloader(cancelEvent);
                    downloader.DownloadProgress += progress.Incremented;

                    foreach (HttpFileInfo info in updateInfo)
                    {
                        String filePath = info.TargetPath;

                        try
                        {
                            await downloader.Download(info.Url, filePath);
                            File.SetLastWriteTime(filePath, info.LastModified);

                            success.Add(filePath);
                        }
                        catch
                        {
                            failed.Add(filePath);
                        }
                    }
                    progress.Close();
                }

                Boolean runPatcher = false;
                if (failed.Count > 0)
                {
                    MessageBox.Show(rootElement,
                        "Failed to download:" + Environment.NewLine + String.Join(Environment.NewLine, failed),
                        (String)Lang.Res["Launcher.ErrorTitle"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                if (success.Count > 0)
                {
                    runPatcher = MessageBox.Show(rootElement,
                        "Download successful!\nRun the patcher?",
                        (String)Lang.Res["Launcher.QuestionTitle"],
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) == MessageBoxResult.Yes;
                }

                if (runPatcher)
                {
                    String main = success.First();
                    if (success.Count > 1)
                    {
                        StringBuilder sb = new StringBuilder(256);
                        foreach (String path in success.Skip(1))
                        {
                            sb.Append('"');
                            sb.Append(path);
                            sb.Append('"');
                        }

                        Process.Start(main, $@"-update ""{applicationPath}"" ""{Process.GetCurrentProcess().Id}"" {sb}");
                    }
                    else
                    {
                        Process.Start(main, $@"-update ""{applicationPath}"" ""{Process.GetCurrentProcess().Id}""");
                    }

                    Environment.Exit(2);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }


        private static async Task<LinkedList<HttpFileInfo>> FindUpdatesInfo(String applicationDirectory, ManualResetEvent cancelEvent, SettingsGrid_Vanilla gameSettings)
        {
            Downloader downloader = new Downloader(cancelEvent);
            String[] urls = gameSettings.DownloadMirrors;

            LinkedList<HttpFileInfo> list = new LinkedList<HttpFileInfo>();
            Dictionary<String, LinkedListNode<HttpFileInfo>> dic = new Dictionary<String, LinkedListNode<HttpFileInfo>>(urls.Length);

            foreach (String url in urls)
            {
                try
                {
                    HttpFileInfo fileInfo = await downloader.GetRemoteFileInfo(url);
                    if (fileInfo == null)
                        continue;

                    Int32 separatorIndex = url.LastIndexOf('/');
                    String remoteFileName = url.Substring(separatorIndex + 1);
                    fileInfo.TargetName = remoteFileName;
                    fileInfo.TargetPath = Path.Combine(applicationDirectory, remoteFileName);

                    LinkedListNode<HttpFileInfo> node;
                    if (!dic.TryGetValue(fileInfo.TargetPath, out node) && File.Exists(fileInfo.TargetPath) && File.GetLastWriteTime(fileInfo.TargetPath) >= fileInfo.LastModified)
                        continue;

                    if (node != null)
                    {
                        if (node.Value.LastModified >= fileInfo.LastModified)
                            continue;

                        LinkedListNode<HttpFileInfo> newNode = list.AddBefore(node, fileInfo);
                        list.Remove(node);
                        dic[fileInfo.TargetPath] = newNode;
                    }
                    else
                    {
                        LinkedListNode<HttpFileInfo> newNode = list.AddLast(fileInfo);
                        dic.Add(fileInfo.TargetPath, newNode);
                    }
                }
                catch
                {
                    // Do nothing
                }
            }

            return list; ;
        }
    }

    public sealed class HttpFileInfo
    {
        public string Url;
        public long ContentLength = -1;
        public DateTime LastModified;
        public String TargetName;
        public String TargetPath;

        public void ReadFromResponse(string url, WebResponse response)
        {
            Url = url;
            ContentLength = response.ContentLength;
            LastModified = ((HttpWebResponse)response).LastModified;
        }
    }

    public sealed class Downloader
    {
        private readonly ManualResetEvent _cancelEvent;

        public event Action<long> DownloadProgress;

        public Downloader(ManualResetEvent cancelEvent)
        {
            _cancelEvent = cancelEvent;
        }

        public async Task<HttpFileInfo> GetRemoteFileInfo(string url)
        {
            HttpFileInfo result = new HttpFileInfo();

            if (_cancelEvent.WaitOne(0))
                return result;

            WebRequest request = WebRequest.Create(url);
            request.Method = "HEAD";

            using (WebResponse resp = await request.GetResponseAsync())
            {
                if (_cancelEvent.WaitOne(0))
                    return result;

                result.ReadFromResponse(url, resp);
                return result;
            }
        }

        public async Task Download(string url, string fileName)
        {
            if (_cancelEvent.WaitOne(0))
                return;

            using (Stream output = File.Create(fileName))
                await Download(url, output);
        }

        private async Task Download(String url, Stream output)
        {
            if (_cancelEvent.WaitOne(0))
                return;

            using (HttpClient client = new HttpClient())
            using (Stream input = await client.GetStreamAsync(url))
                await CopyAsync(input, output, _cancelEvent, DownloadProgress);
        }

        private static async Task CopyAsync(Stream input, Stream output, ManualResetEvent cancelEvent, Action<long> progress)
        {
            byte[] buff = new byte[32 * 1024];

            int read;
            while ((read = await input.ReadAsync(buff, 0, buff.Length)) != 0)
            {
                if (cancelEvent.WaitOne(0))
                    return;

                await output.WriteAsync(buff, 0, read);
                progress?.Invoke(read);
            }
        }
    }

    public sealed class UiProgressWindow : UiWindow, IDisposable
    {
        public UiProgressWindow(string title)
        {
            #region Construct

            Height = 72;
            Width = 320;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;

            UiGrid root = UiGridFactory.Create(3, 1);
            root.SetRowsHeight(GridLength.Auto);
            root.Margin = new Thickness(5);

            TextBlock titleTextBlock = UiTextBlockFactory.Create(title);
            {
                titleTextBlock.VerticalAlignment = VerticalAlignment.Center;
                titleTextBlock.HorizontalAlignment = HorizontalAlignment.Center;
                root.AddUiElement(titleTextBlock, 0, 0);
            }

            _progressBar = UiProgressBarFactory.Create();
            {
                root.AddUiElement(_progressBar, 1, 0);
            }

            _progressTextBlock = UiTextBlockFactory.Create("100%");
            {
                _progressTextBlock.VerticalAlignment = VerticalAlignment.Center;
                _progressTextBlock.HorizontalAlignment = HorizontalAlignment.Center;
                root.AddUiElement(_progressTextBlock, 1, 0);
            }

            _elapsedTextBlock = UiTextBlockFactory.Create((String)Lang.Res["Measurement.Elapsed"] + ": 00:00");
            {
                _elapsedTextBlock.HorizontalAlignment = HorizontalAlignment.Left;
                root.AddUiElement(_elapsedTextBlock, 2, 0);
            }

            _processedTextBlock = UiTextBlockFactory.Create("0 / 0");
            {
                _processedTextBlock.HorizontalAlignment = HorizontalAlignment.Center;
                root.AddUiElement(_processedTextBlock, 2, 0);
            }

            _remainingTextBlock = UiTextBlockFactory.Create((String)Lang.Res["Measurement.Remaining"] + ": 00:00");
            {
                _remainingTextBlock.HorizontalAlignment = HorizontalAlignment.Right;
                root.AddUiElement(_remainingTextBlock, 2, 0);
            }

            Content = root;

            #endregion

            Loaded += OnLoaded;
            Closing += OnClosing;

            _timer = new System.Timers.Timer(500);
            _timer.Elapsed += OnTimer;
        }

        private readonly UiProgressBar _progressBar;
        private readonly TextBlock _progressTextBlock;
        private readonly TextBlock _elapsedTextBlock;
        private readonly TextBlock _processedTextBlock;
        private readonly TextBlock _remainingTextBlock;

        private readonly System.Timers.Timer _timer;

        private long _processedCount, _totalCount;
        private DateTime _begin;

        public void Dispose()
        {
            _timer.Dispose();
        }

        public void SetTotal(long totalCount)
        {
            Interlocked.Exchange(ref _totalCount, totalCount);
        }

        public void Incremented(long processedCount)
        {
            if (Interlocked.Add(ref _processedCount, processedCount) < 0)
                throw new ArgumentOutOfRangeException(nameof(processedCount));
        }

        #region Internal Logic

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _begin = DateTime.Now;
            _timer.Start();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            _timer.Stop();
            _timer.Elapsed -= OnTimer;
        }

        private void OnTimer(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            Dispatcher.Invoke(DispatcherPriority.DataBind, (Action)(UpdateProgress));
        }

        private void UpdateProgress()
        {
            _timer.Elapsed -= OnTimer;

            _progressBar.Maximum = _totalCount;
            _progressBar.Value = _processedCount;

            double percents = (_totalCount == 0) ? 0.0 : 100 * _processedCount / (double)_totalCount;
            TimeSpan elapsed = DateTime.Now - _begin;
            double speed = _processedCount / Math.Max(elapsed.TotalSeconds, 1);
            if (speed < 1) speed = 1;
            TimeSpan left = TimeSpan.FromSeconds((_totalCount - _processedCount) / speed);

            _progressTextBlock.Text = $"{percents:F2}%";
            _elapsedTextBlock.Text = String.Format("{1}: {0:mm\\:ss}", elapsed, (String)Lang.Res["Measurement.Elapsed"]);
            _processedTextBlock.Text = $"{FormatValue(_processedCount)} / {FormatValue(_totalCount)}";
            _remainingTextBlock.Text = String.Format("{1}: {0:mm\\:ss}", left, (String)Lang.Res["Measurement.Remaining"]);

            _timer.Elapsed += OnTimer;
        }

        public static String FormatValue(Int64 value)
        {
            Int32 i = 0;
            Decimal dec = value;
            while ((dec > 1024))
            {
                dec /= 1024;
                i++;
            }

            switch (i)
            {
                case 0:
                    return String.Format("{0:F2} " + (String)Lang.Res["Measurement.ByteAbbr"], dec);
                case 1:
                    return String.Format("{0:F2} " + (String)Lang.Res["Measurement.KByteAbbr"], dec);
                case 2:
                    return String.Format("{0:F2} " + (String)Lang.Res["Measurement.MByteAbbr"], dec);
                case 3:
                    return String.Format("{0:F2} " + (String)Lang.Res["Measurement.GByteAbbr"], dec);
                case 4:
                    return String.Format("{0:F2} " + (String)Lang.Res["Measurement.TByteAbbr"], dec);
                case 5:
                    return String.Format("{0:F2} " + (String)Lang.Res["Measurement.PByteAbbr"], dec);
                case 6:
                    return String.Format("{0:F2} " + (String)Lang.Res["Measurement.EByteAbbr"], dec);
                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        #endregion

        public static void Execute(string title, IProgressSender progressSender, Action action)
        {
            using (UiProgressWindow window = new UiProgressWindow(title))
            {
                progressSender.ProgressTotalChanged += window.SetTotal;
                progressSender.ProgressIncremented += window.Incremented;
                Task.Run(() => ExecuteAction(window, action));
                window.ShowDialog();
            }
        }

        public static T Execute<T>(string title, IProgressSender progressSender, Func<T> func)
        {
            using (UiProgressWindow window = new UiProgressWindow(title))
            {
                progressSender.ProgressTotalChanged += window.SetTotal;
                progressSender.ProgressIncremented += window.Incremented;
                Task<T> task = Task.Run(() => ExecuteFunction(window, func));
                window.ShowDialog();
                return task.Result;
            }
        }

        public static void Execute(string title, Action<Action<long>, Action<long>> action)
        {
            using (UiProgressWindow window = new UiProgressWindow(title))
            {
                Task.Run(() => ExecuteAction(window, action));
                window.ShowDialog();
            }
        }

        public static T Execute<T>(string title, Func<Action<long>, Action<long>, T> action)
        {
            using (UiProgressWindow window = new UiProgressWindow(title))
            {
                Task<T> task = Task.Run(() => ExecuteFunction(window, action));
                window.ShowDialog();
                return task.Result;
            }
        }

        #region Internal Static Logic

        private static void ExecuteAction(UiProgressWindow window, Action action)
        {
            try
            {
                action();
            }
            finally
            {
                window.Dispatcher.Invoke(window.Close);
            }
        }

        private static void ExecuteAction(UiProgressWindow window, Action<Action<long>, Action<long>> action)
        {
            try
            {
                action(window.SetTotal, window.Incremented);
            }
            finally
            {
                window.Dispatcher.Invoke(window.Close);
            }
        }

        private static T ExecuteFunction<T>(UiProgressWindow window, Func<T> func)
        {
            try
            {
                return func();
            }
            finally
            {
                window.Dispatcher.Invoke(window.Close);
            }
        }

        private static T ExecuteFunction<T>(UiProgressWindow window, Func<Action<long>, Action<long>, T> action)
        {
            try
            {
                return action(window.SetTotal, window.Incremented);
            }
            finally
            {
                window.Dispatcher.Invoke(window.Close);
            }
        }

        #endregion
    }

    public class UiProgressBar : ProgressBar
    {
    }

    public class UiWindow : Window
    {
    }

    public static class UiGridFactory
    {
        public static UiGrid Create(int rows, int cols)
        {
            UiGrid grid = new UiGrid();

            if (rows > 1) while (rows-- > 0) grid.RowDefinitions.Add(new RowDefinition());
            if (cols > 1) while (cols-- > 0) grid.ColumnDefinitions.Add(new ColumnDefinition());

            return grid;
        }
    }

    public interface IProgressSender
    {
        event Action<long> ProgressTotalChanged;
        event Action<long> ProgressIncremented;
    }

    public static class UiProgressBarFactory
    {
        public static UiProgressBar Create()
        {
            return new UiProgressBar();
        }
    }
}
