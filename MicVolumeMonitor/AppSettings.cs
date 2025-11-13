using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace MicVolumeMonitor
{
    /// <summary>
    /// アプリケーション設定を管理するクラス
    /// </summary>
    public class AppSettings
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MicVolumeMonitor",
            "settings.json"
        );

        private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "MicVolumeMonitor";

        /// <summary>
        /// 警告閾値（dB）
        /// </summary>
        public float Threshold { get; set; } = 80f;

        /// <summary>
        /// 警告音量（0.0-1.0）
        /// </summary>
        public float WarningVolume { get; set; } = 0.8f;

        /// <summary>
        /// PC起動時に自動起動するか
        /// </summary>
        public bool AutoStart { get; set; } = false;

        /// <summary>
        /// 選択されたマイクデバイス番号
        /// </summary>
        public int SelectedDeviceIndex { get; set; } = 0;

        /// <summary>
        /// 設定をファイルから読み込み
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                Console.WriteLine($"[AppSettings] 設定ファイルを読み込み中: {SettingsFilePath}");

                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);

                    if (settings != null)
                    {
                        Console.WriteLine($"[AppSettings] 設定を読み込みました: 閾値={settings.Threshold}dB, 音量={settings.WarningVolume}, 自動起動={settings.AutoStart}");
                        return settings;
                    }
                }
                else
                {
                    Console.WriteLine("[AppSettings] 設定ファイルが存在しません。デフォルト設定を使用します");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppSettings] エラー - 設定の読み込みに失敗: {ex.Message}");
            }

            return new AppSettings();
        }

        /// <summary>
        /// 設定をファイルに保存
        /// </summary>
        public void Save()
        {
            try
            {
                Console.WriteLine($"[AppSettings] 設定を保存中: {SettingsFilePath}");

                // ディレクトリが存在しない場合は作成
                string? directory = Path.GetDirectoryName(SettingsFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Console.WriteLine($"[AppSettings] ディレクトリを作成しました: {directory}");
                }

                // JSON形式で保存
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsFilePath, json);

                Console.WriteLine($"[AppSettings] 設定を保存しました: 閾値={Threshold}dB, 音量={WarningVolume}, 自動起動={AutoStart}");

                // 自動起動設定をレジストリに反映
                UpdateAutoStartRegistry();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppSettings] エラー - 設定の保存に失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 自動起動設定をレジストリに反映
        /// </summary>
        private void UpdateAutoStartRegistry()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true))
                {
                    if (key == null)
                    {
                        Console.WriteLine("[AppSettings] エラー - レジストリキーを開けませんでした");
                        return;
                    }

                    if (AutoStart)
                    {
                        // 実行ファイルのパスを取得
                        string exePath = Environment.ProcessPath ?? "";
                        if (string.IsNullOrEmpty(exePath))
                        {
                            Console.WriteLine("[AppSettings] エラー - 実行ファイルのパスを取得できませんでした");
                            return;
                        }

                        // レジストリに登録
                        key.SetValue(APP_NAME, $"\"{exePath}\"");
                        Console.WriteLine($"[AppSettings] 自動起動を有効化: {exePath}");
                    }
                    else
                    {
                        // レジストリから削除
                        if (key.GetValue(APP_NAME) != null)
                        {
                            key.DeleteValue(APP_NAME);
                            Console.WriteLine("[AppSettings] 自動起動を無効化");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppSettings] エラー - レジストリ更新に失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在の自動起動状態を確認
        /// </summary>
        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false))
                {
                    if (key != null)
                    {
                        object? value = key.GetValue(APP_NAME);
                        return value != null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppSettings] エラー - 自動起動状態の確認に失敗: {ex.Message}");
            }

            return false;
        }
    }
}
