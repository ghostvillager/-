using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace SimpleMultiAudio
{
    public partial class App : Application
    {
        private const string MutexName = "SimpleMultiAudio_SingleInstance_Mutex";
        private const string EventName = "SimpleMultiAudio_ShowWindow_Event";

        private static Mutex? _mutex;
        private EventWaitHandle? _showEvent;
        private Thread? _showListenerThread;
        private volatile bool _exitListener;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // 既に起動中 → 既存プロセスに「ウィンドウを表示」を通知して終了
                try
                {
                    using var showEvent = EventWaitHandle.OpenExisting(EventName);
                    showEvent.Set();
                }
                catch
                {
                    // イベントが取れなくても二重起動は防ぐ
                }

                Shutdown();
                return;
            }

            // 表示要求を待つ名前付きイベント
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            _exitListener = false;
            _showListenerThread = new Thread(ShowListenerLoop)
            {
                IsBackground = true,
                Name = "ShowWindowListener"
            };
            _showListenerThread.Start();

            base.OnStartup(e);
        }

        private void ShowListenerLoop()
        {
            while (!_exitListener && _showEvent != null)
            {
                try
                {
                    if (_showEvent.WaitOne(500))
                    {
                        // UI スレッドで設定ウィンドウを表示
                        Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                        {
                            if (Current.MainWindow is MainWindow mw)
                            {
                                mw.RequestShowFromTray();
                            }
                        }));
                    }
                }
                catch
                {
                    break;
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _exitListener = true;
            try { _showEvent?.Set(); } catch { }
            try { _showEvent?.Dispose(); } catch { }
            _showEvent = null;

            try
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
            catch { }
            _mutex = null;

            base.OnExit(e);
        }
    }
}
