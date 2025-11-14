using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Drawing;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace MicVolumeMonitor;

/// <summary>
/// マイク音量監視アプリケーションのメインウィンドウ
/// </summary>
public partial class MainWindow : Window
{
    private AudioMonitor? audioMonitor;
    private NotifyIcon? notifyIcon;
    private bool isMonitoring = false;
    private float currentThreshold = 80f;
    private float currentWarningVolume = 0.8f;
    private AppSettings settings;
    private float peakLevel = 0f;

    public MainWindow()
    {
        InitializeComponent();
        Console.WriteLine("[MainWindow] アプリケーションを初期化します...");

        // ウィンドウアイコンを設定
        SetWindowIcon();

        // 設定を読み込み
        settings = AppSettings.Load();
        currentThreshold = settings.Threshold;
        currentWarningVolume = settings.WarningVolume;

        // システムトレイアイコンの設定
        SetupNotifyIcon();

        // 利用可能なマイクデバイスをロード
        LoadMicrophones();

        // UIを設定値で初期化
        InitializeUIFromSettings();

        // アクティビティインジケーターを初期化
        UpdateActivityIndicator(false);

        Console.WriteLine("[MainWindow] 初期化が完了しました");
    }

    /// <summary>
    /// ウィンドウアイコンを設定
    /// </summary>
    private void SetWindowIcon()
    {
        try
        {
            var iconSource = IconGenerator.CreateWindowIconSource();
            if (iconSource != null)
            {
                this.Icon = iconSource;
                Console.WriteLine("[MainWindow] ウィンドウアイコンを設定しました");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] エラー - ウィンドウアイコンの設定に失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// 設定値を基にUIを初期化
    /// </summary>
    private void InitializeUIFromSettings()
    {
        ThresholdSlider.Value = settings.Threshold;
        WarningVolumeSlider.Value = settings.WarningVolume * 100f;
        AutoStartCheckBox.IsChecked = settings.AutoStart;

        if (settings.SelectedDeviceIndex < MicrophoneComboBox.Items.Count)
        {
            MicrophoneComboBox.SelectedIndex = settings.SelectedDeviceIndex;
        }

        // 閾値説明を初期化
        UpdateThresholdDescription();

        Console.WriteLine($"[MainWindow] UI初期化完了: 閾値={settings.Threshold}dB, 音量={settings.WarningVolume * 100}%, 自動起動={settings.AutoStart}");
    }

    /// <summary>
    /// システムトレイアイコンの設定
    /// </summary>
    private void SetupNotifyIcon()
    {
        notifyIcon = new NotifyIcon
        {
            // カスタムマイクアイコンを使用
            Icon = IconGenerator.CreateMicrophoneIcon(32),
            Visible = false,
            Text = "マイク音量監視"
        };

        // ダブルクリックでウィンドウを表示
        notifyIcon.DoubleClick += (s, e) => ShowWindow();

        // 右クリックメニュー
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("開く", null, (s, e) => ShowWindow());
        contextMenu.Items.Add("終了", null, (s, e) => CloseApplication());
        notifyIcon.ContextMenuStrip = contextMenu;

        Console.WriteLine("[MainWindow] システムトレイアイコンを設定しました");
    }

    /// <summary>
    /// 利用可能なマイクデバイスをロード
    /// </summary>
    private void LoadMicrophones()
    {
        Console.WriteLine("[MainWindow] マイクデバイスをロード中...");

        var devices = AudioMonitor.GetAvailableDevices();

        if (devices.Count == 0)
        {
            Console.WriteLine("[MainWindow] 警告: マイクデバイスが見つかりません");
            MicrophoneComboBox.Items.Add("デバイスが見つかりません");
            MicrophoneComboBox.SelectedIndex = 0;
            MicrophoneComboBox.IsEnabled = false;
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            return;
        }

        foreach (var device in devices)
        {
            MicrophoneComboBox.Items.Add(device);
        }

        MicrophoneComboBox.SelectedIndex = 0;
        Console.WriteLine($"[MainWindow] {devices.Count}個のマイクデバイスをロードしました");
    }

    /// <summary>
    /// 監視を開始
    /// </summary>
    private void StartMonitoring()
    {
        try
        {
            Console.WriteLine("[MainWindow] 監視を開始します...");

            int deviceNumber = MicrophoneComboBox.SelectedIndex;

            // AudioMonitorを作成
            audioMonitor = new AudioMonitor(deviceNumber, currentThreshold, currentWarningVolume);
            audioMonitor.VolumeChanged += OnVolumeChanged;
            audioMonitor.ThresholdExceeded += OnThresholdExceeded;

            // 監視開始
            audioMonitor.Start();

            // UI更新
            isMonitoring = true;
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            MicrophoneComboBox.IsEnabled = false;
            ThresholdSlider.IsEnabled = false;
            WarningVolumeSlider.IsEnabled = false;
            UpdateActivityIndicator(true);

            // ピークレベルをリセット
            peakLevel = 0f;

            // 通知
            notifyIcon!.ShowBalloonTip(3000, "監視開始", "マイク音量の監視を開始しました", ToolTipIcon.Info);

            Console.WriteLine("[MainWindow] 監視を開始しました");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] エラー: 監視開始に失敗 - {ex.Message}");
            MessageBox.Show($"監視の開始に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            StopMonitoring();
        }
    }

    /// <summary>
    /// 監視を停止
    /// </summary>
    private void StopMonitoring()
    {
        try
        {
            Console.WriteLine("[MainWindow] 監視を停止します...");

            if (audioMonitor != null)
            {
                audioMonitor.VolumeChanged -= OnVolumeChanged;
                audioMonitor.ThresholdExceeded -= OnThresholdExceeded;
                audioMonitor.Dispose();
                audioMonitor = null;
            }

            // UI更新
            isMonitoring = false;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            MicrophoneComboBox.IsEnabled = true;
            ThresholdSlider.IsEnabled = true;
            WarningVolumeSlider.IsEnabled = true;
            CurrentLevelText.Text = "--";
            UpdateActivityIndicator(false);
            UpdateMeterBar(0);
            UpdateWarningUI(false);

            // ピークレベルをリセット
            peakLevel = 0f;
            UpdateInfoText();

            Console.WriteLine("[MainWindow] 監視を停止しました");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] エラー: 監視停止に失敗 - {ex.Message}");
        }
    }

    /// <summary>
    /// 開始ボタンクリック
    /// </summary>
    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        StartMonitoring();
    }

    /// <summary>
    /// 停止ボタンクリック
    /// </summary>
    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopMonitoring();
    }

    /// <summary>
    /// 閾値スライダー変更
    /// </summary>
    private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThresholdValueText == null || settings == null) return;

        currentThreshold = (float)e.NewValue;
        ThresholdValueText.Text = currentThreshold.ToString("F0");
        UpdateInfoText();
        UpdateThresholdDescription();

        if (audioMonitor != null && isMonitoring)
        {
            Console.WriteLine($"[MainWindow] 警告: 監視中は閾値を変更できません（監視を停止してください）");
            MessageBox.Show("監視中は閾値を変更できません。\n監視を停止してから変更してください。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            // 設定を保存
            settings.Threshold = currentThreshold;
            settings.Save();
        }
    }

    /// <summary>
    /// 警告音量スライダー変更
    /// </summary>
    private void WarningVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WarningVolumeValueText == null || settings == null) return;

        currentWarningVolume = (float)e.NewValue / 100f;
        WarningVolumeValueText.Text = e.NewValue.ToString("F0");

        if (audioMonitor != null && isMonitoring)
        {
            Console.WriteLine($"[MainWindow] 警告: 監視中は警告音量を変更できません（監視を停止してください）");
            MessageBox.Show("監視中は警告音量を変更できません。\n監視を停止してから変更してください。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            // 設定を保存
            settings.WarningVolume = currentWarningVolume;
            settings.Save();
        }
    }

    /// <summary>
    /// マイク選択変更
    /// </summary>
    private void MicrophoneComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (settings == null) return;

        if (isMonitoring)
        {
            Console.WriteLine($"[MainWindow] 警告: 監視中はマイクを変更できません");
            MessageBox.Show("監視中はマイクを変更できません。\n監視を停止してから変更してください。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else if (MicrophoneComboBox.SelectedIndex >= 0)
        {
            // 設定を保存
            settings.SelectedDeviceIndex = MicrophoneComboBox.SelectedIndex;
            settings.Save();
        }
    }

    /// <summary>
    /// 自動起動チェックボックス変更
    /// </summary>
    private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (settings == null) return;

        if (AutoStartCheckBox.IsChecked.HasValue)
        {
            // 設定を保存
            settings.AutoStart = AutoStartCheckBox.IsChecked.Value;
            settings.Save();
        }
    }

    /// <summary>
    /// 音量が更新されたときのイベントハンドラー
    /// </summary>
    private void OnVolumeChanged(float volume)
    {
        // UIスレッドで更新
        Dispatcher.Invoke(() =>
        {
            // 音量テキストを更新
            CurrentLevelText.Text = $"{volume:F0}";

            // メーターバーを更新
            UpdateMeterBar(volume);

            // ピークレベルを追跡
            if (volume > peakLevel)
            {
                peakLevel = volume;
                UpdateInfoText();
            }

            // 閾値超過をチェックして警告UIを更新
            bool isExceeding = volume >= currentThreshold;
            UpdateWarningUI(isExceeding);
        });
    }

    /// <summary>
    /// 閾値を超えたときのイベントハンドラー
    /// </summary>
    private void OnThresholdExceeded()
    {
        // UIスレッドで通知
        Dispatcher.Invoke(() =>
        {
            Console.WriteLine("[MainWindow] 閾値超過通知を受信しました");
            // 必要に応じて追加のUI更新を行う
        });
    }

    /// <summary>
    /// ウィンドウ状態変更
    /// </summary>
    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Console.WriteLine("[MainWindow] ウィンドウを最小化 - トレイに格納します");

            // トレイに格納
            Hide();
            notifyIcon!.Visible = true;

            if (isMonitoring)
            {
                notifyIcon.ShowBalloonTip(3000, "バックグラウンドで監視中", "タスクトレイに格納されました", ToolTipIcon.Info);
            }
        }
    }

    /// <summary>
    /// ウィンドウを閉じる動作（最小化に変更）
    /// </summary>
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        Console.WriteLine("[MainWindow] ウィンドウを閉じようとしています - 最小化に変更します");

        e.Cancel = true;
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// ウィンドウを表示・アクティブ化
    /// </summary>
    private void ShowWindow()
    {
        Console.WriteLine("[MainWindow] ウィンドウを表示します");

        Show();
        WindowState = WindowState.Normal;
        Activate();
        notifyIcon!.Visible = false;
    }

    /// <summary>
    /// アプリケーションを終了
    /// </summary>
    private void CloseApplication()
    {
        Console.WriteLine("[MainWindow] アプリケーションを終了します...");

        // 監視を停止
        StopMonitoring();

        // トレイアイコンを削除
        if (notifyIcon != null)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        }

        // アプリケーション終了
        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>
    /// ヘッダーをドラッグしてウィンドウを移動
    /// </summary>
    private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            this.DragMove();
        }
    }

    /// <summary>
    /// メーターバーの幅を更新
    /// </summary>
    private void UpdateMeterBar(float volume)
    {
        // 音量を0-120dBの範囲でパーセンテージに変換
        float percentage = Math.Clamp(volume / 120f, 0f, 1f);

        // メーターバーの最大幅を取得（親Gridの幅）
        if (MeterBar.Parent is Grid parentGrid)
        {
            double maxWidth = parentGrid.ActualWidth;
            if (maxWidth > 0)
            {
                MeterBar.Width = maxWidth * percentage;
            }
        }

        // 閾値超過時は色を変更
        bool isExceeding = volume >= currentThreshold;
        MeterBar.Background = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
                isExceeding ? "#EF4444" : "#3B82F6"
            )
        );
    }

    /// <summary>
    /// 警告UIの表示状態を更新
    /// </summary>
    private void UpdateWarningUI(bool isWarning)
    {
        if (isWarning)
        {
            // 警告表示
            WarningAlert.Visibility = Visibility.Visible;
            IconBox.Background = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEE2E2")
            );
            CurrentLevelText.Foreground = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626")
            );
        }
        else
        {
            // 通常表示
            WarningAlert.Visibility = Visibility.Collapsed;
            IconBox.Background = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DBEAFE")
            );
            CurrentLevelText.Foreground = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#111827")
            );
        }
    }

    /// <summary>
    /// アクティビティインジケーターを更新
    /// </summary>
    private void UpdateActivityIndicator(bool isActive)
    {
        if (isActive)
        {
            ActivityIcon.Foreground = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3B82F6")
            );
        }
        else
        {
            ActivityIcon.Foreground = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9CA3AF")
            );
        }
    }

    /// <summary>
    /// 情報テキストを更新
    /// </summary>
    private void UpdateInfoText()
    {
        string peakText = peakLevel > 0 ? $"{peakLevel:F0}dB" : "--dB";
        InfoText.Text = $"監視音量: {currentThreshold:F0}dB / ピーク: {peakText}";
    }

    /// <summary>
    /// dB値から音の大きさの説明を取得
    /// </summary>
    private string GetVolumeLevelDescription(float db)
    {
        return db switch
        {
            < 40 => "深夜の郊外・ささやき声",
            < 50 => "図書館・静かな住宅",
            < 60 => "静かなオフィス・普通の会話",
            < 70 => "レストラン・電話のベル",
            < 80 => "掃除機・騒がしい事務所",
            < 90 => "地下鉄の車内・ピアノ",
            < 100 => "犬の鳴き声・工場内",
            < 110 => "電車のガード下・カラオケ",
            _ => "車のクラクション・ロックコンサート"
        };
    }

    /// <summary>
    /// 閾値説明テキストを更新
    /// </summary>
    private void UpdateThresholdDescription()
    {
        if (ThresholdDescriptionText != null)
        {
            string description = GetVolumeLevelDescription(currentThreshold);
            ThresholdDescriptionText.Text = description;
        }
    }
}