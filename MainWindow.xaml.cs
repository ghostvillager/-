using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace SimpleMultiAudio
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<DeviceItem> _devices = new();
        private WasapiLoopbackCapture? _capture;
        private readonly List<WasapiOut> _outputs = new();
        private readonly List<BufferedWaveProvider> _buffers = new();
        private NotifyIcon? _trayIcon;
        private bool _isRunning;
        private readonly string _settingsPath;

        private bool _isReallyClosing = false;

        // ===== 音割れ対策で変更した値 =====
        // Bluetooth機器でも安定するようにレイテンシとバッファを大きくしています
        private const int LatencyMs = 200;          // 元: 80
        private const int BufferDurationMs = 1000;  // 元: 300

        public MainWindow()
        {
            InitializeComponent();
            DeviceList.ItemsSource = _devices;

            // 最初からタスクバーに出さず、非表示状態で開始（Minimized にはしない）
            this.ShowInTaskbar = false;
            this.Visibility = Visibility.Hidden;
            this.WindowState = WindowState.Normal;

            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimpleMultiAudio");
            Directory.CreateDirectory(appData);
            _settingsPath = Path.Combine(appData, "settings.json");

            SetupTray();
            LoadDevices();
            LoadSettings();

            // 起動時に自動開始（保存された選択があれば）
            if (_devices.Any(d => d.IsSelected))
            {
                Start();
            }

            // トレイアイコンを表示
            this.Loaded += (s, e) =>
            {
                if (_trayIcon != null)
                    _trayIcon.Visible = true;
            };
        }

        private void SetupTray()
        {
            System.Drawing.Icon? trayIconImage = null;
            try
            {
                // 複数の場所から icon.ico を探す
                string[] candidates = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico"),
                    Path.Combine(Directory.GetCurrentDirectory(), "icon.ico"),
                    "icon.ico"
                };
                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        trayIconImage = new System.Drawing.Icon(path);
                        break;
                    }
                }
            }
            catch { }

            _trayIcon = new NotifyIcon
            {
                Icon = trayIconImage ?? System.Drawing.SystemIcons.Application,
                Text = "複数デバイス同時再生 (Fixed)",
                Visible = false
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("再起動", null, (s, e) => RestartApp());
            menu.Items.Add("終了", null, (s, e) =>
            {
                _isReallyClosing = true;
                Stop();
                if (_trayIcon != null) _trayIcon.Visible = false;
                Application.Current.Shutdown();
            });
            _trayIcon.ContextMenuStrip = menu;

            // 左クリック1回で設定ウィンドウを開く
            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    ShowSettingsWindow();
            };
        }

        /// <summary>二重起動時に既存インスタンスへ表示要求が来たときに呼ばれる</summary>
        public void RequestShowFromTray()
        {
            ShowSettingsWindow();
        }

        private void ShowSettingsWindow()
        {
            // 最小化状態を経由せず、直接 Normal で表示する
            // （タスクバーに一度収納されてから開く挙動を防ぐ）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 開くたびにデバイス一覧を最新化（選択状態は維持）
                RefreshDeviceListKeepSelection();

                // 先に Normal にしてから表示する（Minimized のまま ShowInTaskbar を true にしない）
                if (this.WindowState != WindowState.Normal)
                    this.WindowState = WindowState.Normal;

                this.ShowInTaskbar = true;
                this.Visibility = Visibility.Visible;
                this.Show();
                this.Activate();
                this.Topmost = true;
                this.Topmost = false;
                this.Focus();
            }));
        }

        private void RefreshDeviceListKeepSelection()
        {
            var selectedIds = _devices.Where(d => d.IsSelected).Select(d => d.Id).ToHashSet();
            LoadDevices();
            foreach (var item in _devices)
            {
                item.IsSelected = selectedIds.Contains(item.Id);
            }
        }

        /// <summary>
        /// トレイに隠す。ShowInTaskbar=false を先に行い、Minimized にしないことで
        /// 次回表示時にタスクバー復元アニメーションが出ないようにする。
        /// </summary>
        private void HideToTray()
        {
            this.ShowInTaskbar = false;
            this.Visibility = Visibility.Hidden;
            // WindowState は Minimized にしない（復元時のタスクバー経由を防ぐ）
            if (this.WindowState != WindowState.Normal)
                this.WindowState = WindowState.Normal;
            if (_trayIcon != null)
                _trayIcon.Visible = true;
        }

        private void RestartApp()
        {
            _isReallyClosing = true;
            Stop();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });
            }
            Application.Current.Shutdown();
        }

        private void LoadDevices()
        {
            _devices.Clear();
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            foreach (var dev in devices)
            {
                _devices.Add(new DeviceItem
                {
                    Id = dev.ID,
                    Name = dev.FriendlyName,
                    IsSelected = false
                });
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return;
                var json = File.ReadAllText(_settingsPath);
                var saved = JsonSerializer.Deserialize<List<string>>(json);
                if (saved == null) return;

                foreach (var item in _devices)
                {
                    item.IsSelected = saved.Contains(item.Id);
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                var selected = _devices.Where(d => d.IsSelected).Select(d => d.Id).ToList();
                var json = JsonSerializer.Serialize(selected);
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            // 未再生時は「開始」、再生中は「更新」（現在の選択で再適用）
            Start();
        }

        private void Start()
        {
            var selected = _devices.Where(d => d.IsSelected).ToList();
            if (selected.Count == 0)
            {
                if (_isRunning)
                {
                    Stop();
                    BtnStart.Content = "開始";
                    StatusText.Text = "停止しました（デバイス未選択）";
                }
                else
                {
                    MessageBox.Show("出力デバイスを1つ以上選択してください。");
                }
                return;
            }

            try
            {
                Stop(); // 念のため（更新時はいったん止めてから再開始）

                var enumerator = new MMDeviceEnumerator();

                // 選択されたデバイスすべてに出力（1台だけでも可）
                // デフォルトデバイスはシステムが既に鳴らしているため、
                // ループバックの二重再生（エコー）を避けるため出力対象から除外する
                var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                string defaultId = defaultDevice.ID;

                var targetDevices = selected.Where(d => d.Id != defaultId).ToList();
                bool defaultSelected = selected.Any(d => d.Id == defaultId);

                // デフォルト以外が1台も無い場合：
                // デフォルトのみ選択なら「1デバイス」として扱う（追加コピーは不要）
                if (targetDevices.Count == 0)
                {
                    if (defaultSelected)
                    {
                        // デフォルトのみ → 追加出力はせず、総数1として記録
                        SaveSettings();
                        BtnStart.Content = "更新";
                        StatusText.Text = "再生中（1デバイス）";
                        _isRunning = true; // 設定上は「動作中」扱い（再更新可能）
                        if (this.Visibility != Visibility.Visible)
                            HideToTray();
                        return;
                    }

                    BtnStart.Content = "開始";
                    StatusText.Text = "準備完了";
                    MessageBox.Show("出力デバイスを1つ以上選択してください。");
                    return;
                }

                _capture = new WasapiLoopbackCapture();
                var format = _capture.WaveFormat;

                foreach (var item in targetDevices)
                {
                    var device = enumerator.GetDevice(item.Id);

                    // ===== 修正ポイント =====
                    // レイテンシを大きくしてBluetooth機器でも音割れしにくくする
                    var outDevice = new WasapiOut(device, AudioClientShareMode.Shared, true, LatencyMs);

                    var buffer = new BufferedWaveProvider(format)
                    {
                        DiscardOnBufferOverflow = true,
                        BufferDuration = TimeSpan.FromMilliseconds(BufferDurationMs)  // バッファも大きく
                    };

                    outDevice.Init(buffer);
                    outDevice.Play();

                    _outputs.Add(outDevice);
                    _buffers.Add(buffer);
                }

                _capture.DataAvailable += (s, a) =>
                {
                    foreach (var buf in _buffers)
                    {
                        buf.AddSamples(a.Buffer, 0, a.BytesRecorded);
                    }
                };

                _capture.StartRecording();
                _isRunning = true;

                SaveSettings();
                BtnStart.Content = "更新";
                // 総再生デバイス数 = 追加出力 + （デフォルトを選んでいれば1）
                int totalCount = targetDevices.Count + (defaultSelected ? 1 : 0);
                StatusText.Text = $"再生中（{totalCount}デバイス） [Latency={LatencyMs}ms]";

                // ウィンドウを開いているときは閉じない（更新を一発で完了させる）
                // 起動時の自動開始など、もともと非表示のときだけトレイに隠す
                if (this.Visibility != Visibility.Visible)
                    HideToTray();
            }
            catch (Exception ex)
            {
                MessageBox.Show("開始に失敗しました：\n" + ex.Message);
                Stop();
                BtnStart.Content = "開始";
                StatusText.Text = "開始に失敗しました";
            }
        }

        private void Stop()
        {
            try
            {
                _capture?.StopRecording();
                _capture?.Dispose();
                _capture = null;

                foreach (var o in _outputs)
                {
                    o.Stop();
                    o.Dispose();
                }
                _outputs.Clear();
                _buffers.Clear();
            }
            catch { }

            _isRunning = false;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isReallyClosing)
            {
                // 本当に終了する場合
                base.OnClosing(e);
                return;
            }

            // ×ボタンで終了せずトレイに隠す
            e.Cancel = true;
            HideToTray();
        }

        protected override void OnClosed(EventArgs e)
        {
            Stop();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            base.OnClosed(e);
        }
    }

    public class DeviceItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
