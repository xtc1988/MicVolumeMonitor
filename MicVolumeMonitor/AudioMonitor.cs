using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Threading;

namespace MicVolumeMonitor
{
    /// <summary>
    /// マイク入力の監視、音量計算、警告音再生を管理するクラス
    /// </summary>
    public class AudioMonitor : IDisposable
    {
        private WaveInEvent? audioRecord;
        private readonly int deviceNumber;
        private readonly float threshold;
        private readonly float warningVolume;
        private DateTime lastWarningTime = DateTime.MinValue;
        private const int COOLDOWN_MS = 2000; // 警告音のクールダウン時間（ミリ秒）
        private bool disposed = false;

        /// <summary>
        /// 音量が更新されたときに発生するイベント
        /// </summary>
        public event Action<float>? VolumeChanged;

        /// <summary>
        /// 閾値を超えたときに発生するイベント
        /// </summary>
        public event Action? ThresholdExceeded;

        /// <summary>
        /// AudioMonitorの新しいインスタンスを初期化
        /// </summary>
        /// <param name="deviceNumber">使用するマイクのデバイス番号</param>
        /// <param name="threshold">警告音を鳴らす閾値（dB）</param>
        /// <param name="warningVolume">警告音の音量（0.0-1.0）</param>
        public AudioMonitor(int deviceNumber, float threshold, float warningVolume = 0.8f)
        {
            this.deviceNumber = deviceNumber;
            this.threshold = threshold;
            this.warningVolume = warningVolume;

            Console.WriteLine($"[AudioMonitor] 初期化: デバイス={deviceNumber}, 閾値={threshold}dB, 警告音量={warningVolume}");
        }

        /// <summary>
        /// マイク監視を開始
        /// </summary>
        public void Start()
        {
            try
            {
                Console.WriteLine("[AudioMonitor] 監視を開始します...");

                audioRecord = new WaveInEvent
                {
                    DeviceNumber = deviceNumber,
                    WaveFormat = new WaveFormat(44100, 16, 1), // 44.1kHz, 16bit, モノラル
                    BufferMilliseconds = 100 // 100msごとにデータを取得
                };

                audioRecord.DataAvailable += OnDataAvailable;
                audioRecord.StartRecording();

                Console.WriteLine("[AudioMonitor] 監視開始に成功しました");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioMonitor] エラー - 監視開始に失敗: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// マイク監視を停止
        /// </summary>
        public void Stop()
        {
            try
            {
                Console.WriteLine("[AudioMonitor] 監視を停止します...");

                if (audioRecord != null)
                {
                    audioRecord.StopRecording();
                    audioRecord.DataAvailable -= OnDataAvailable;
                    audioRecord.Dispose();
                    audioRecord = null;
                }

                Console.WriteLine("[AudioMonitor] 監視停止に成功しました");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioMonitor] エラー - 監視停止に失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 音声データが利用可能になったときのコールバック
        /// </summary>
        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                // バッファから16bitサンプルを取得
                float sum = 0;
                int sampleCount = e.BytesRecorded / 2; // 16bit = 2 bytes per sample

                for (int i = 0; i < e.BytesRecorded; i += 2)
                {
                    // 16bitサンプルを取得（リトルエンディアン）
                    short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));

                    // -32768 ~ 32767 を -1.0 ~ 1.0 に正規化
                    float normalizedSample = sample / 32768f;

                    // RMS計算用に二乗和を累積
                    sum += normalizedSample * normalizedSample;
                }

                // RMS (Root Mean Square) を計算
                float rms = (float)Math.Sqrt(sum / sampleCount);

                // dBに変換（reference = 1.0）
                // 非常に小さい値の場合はlog10が負の無限大になるのを防ぐ
                float db = rms > 0.0001f ? 20f * (float)Math.Log10(rms / 1.0) + 90f : 0f;

                // デバッグログ（音量が変化したときのみ）
                if (db > 10) // 10dB以上のときのみログ出力
                {
                    Console.WriteLine($"[AudioMonitor] 音量: {db:F1}dB (RMS: {rms:F4})");
                }

                // UIスレッドに音量を通知
                VolumeChanged?.Invoke(db);

                // 閾値を超えたかチェック
                if (db >= threshold)
                {
                    // クールダウン中かチェック
                    if ((DateTime.Now - lastWarningTime).TotalMilliseconds >= COOLDOWN_MS)
                    {
                        Console.WriteLine($"[AudioMonitor] 警告: 閾値超過 ({db:F1}dB >= {threshold}dB)");
                        lastWarningTime = DateTime.Now;

                        // UIスレッドに閾値超過を通知
                        ThresholdExceeded?.Invoke();

                        // 警告音を別スレッドで再生（UIをブロックしないため）
                        ThreadPool.QueueUserWorkItem(_ => PlayWarningSound());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioMonitor] エラー - データ処理中: {ex.Message}");
            }
        }

        /// <summary>
        /// 警告音を再生（緊急性の高い高速ビープ音）
        /// </summary>
        private void PlayWarningSound()
        {
            try
            {
                Console.WriteLine("[AudioMonitor] 警告音を再生します...");

                // 緊急性の高い高周波ビープ音パターン（2000Hz → 1500Hz を5回繰り返し）
                double[] frequencies = { 2000, 1500, 2000, 1500, 2000, 1500, 2000, 1500, 2000, 1500 };
                var signalChain = new List<ISampleProvider>();

                foreach (var freq in frequencies)
                {
                    // サイン波トーンを生成（より緊急性の高い音）
                    var tone = new SignalGenerator(44100, 1)
                    {
                        Type = SignalGeneratorType.Sin,
                        Frequency = freq,
                        Gain = warningVolume * 0.9 // 音量を大きく（0.5→0.9）
                    };

                    // 100msの短いビープ（より緊迫感を演出）
                    var toneWithDuration = tone.Take(TimeSpan.FromMilliseconds(100));
                    signalChain.Add(toneWithDuration);

                    // ビープ間に短い無音（50ms）を挿入
                    var silence = new SignalGenerator(44100, 1)
                    {
                        Type = SignalGeneratorType.Sin,
                        Frequency = 0,
                        Gain = 0
                    };
                    var silenceWithDuration = silence.Take(TimeSpan.FromMilliseconds(50));
                    signalChain.Add(silenceWithDuration);
                }

                // すべてのトーンを連結
                var concatenated = new ConcatenatingSampleProvider(signalChain);

                // WaveOutで再生
                using (var waveOut = new WaveOutEvent())
                {
                    waveOut.Init(concatenated);
                    waveOut.Play();

                    // 再生が完了するまで待機
                    while (waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        Thread.Sleep(50);
                    }
                }

                Console.WriteLine("[AudioMonitor] 警告音の再生が完了しました");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioMonitor] エラー - 警告音再生失敗、フォールバックを試行: {ex.Message}");

                // フォールバック: Console.Beep（緊急性の高いパターン）
                try
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Console.Beep(2000, 100);
                        Thread.Sleep(50);
                        Console.Beep(1500, 100);
                        Thread.Sleep(50);
                    }
                    Console.WriteLine("[AudioMonitor] フォールバック警告音を再生しました");
                }
                catch (Exception beepEx)
                {
                    Console.WriteLine($"[AudioMonitor] エラー - フォールバックも失敗: {beepEx.Message}");
                }
            }
        }

        /// <summary>
        /// リソースを解放
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                Console.WriteLine("[AudioMonitor] リソースを解放します...");
                Stop();
                disposed = true;
            }
        }

        /// <summary>
        /// 利用可能なマイクデバイスのリストを取得
        /// </summary>
        public static List<string> GetAvailableDevices()
        {
            var devices = new List<string>();
            int deviceCount = WaveInEvent.DeviceCount;

            Console.WriteLine($"[AudioMonitor] 利用可能なデバイス数: {deviceCount}");

            for (int i = 0; i < deviceCount; i++)
            {
                var capabilities = WaveInEvent.GetCapabilities(i);
                string deviceName = $"{i}: {capabilities.ProductName}";
                devices.Add(deviceName);
                Console.WriteLine($"[AudioMonitor] デバイス {deviceName}");
            }

            return devices;
        }
    }
}
