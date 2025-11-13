using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MicVolumeMonitor
{
    /// <summary>
    /// アプリケーションアイコンを動的に生成するクラス
    /// </summary>
    public static class IconGenerator
    {
        /// <summary>
        /// マイクアイコンを生成
        /// </summary>
        /// <param name="size">アイコンのサイズ（ピクセル）</param>
        /// <returns>生成されたアイコン</returns>
        public static Icon CreateMicrophoneIcon(int size = 32)
        {
            try
            {
                Console.WriteLine($"[IconGenerator] {size}x{size}のマイクアイコンを生成中...");

                using (Bitmap bitmap = new Bitmap(size, size))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.Clear(Color.Transparent);

                    // マイクの色（青系）
                    Color micColor = Color.FromArgb(33, 150, 243);
                    Color standColor = Color.FromArgb(100, 100, 100);

                    float scale = size / 32f;

                    // マイクのカプセル部分
                    using (SolidBrush brush = new SolidBrush(micColor))
                    {
                        // マイク本体（楕円）
                        float micWidth = 10 * scale;
                        float micHeight = 14 * scale;
                        float micX = (size - micWidth) / 2;
                        float micY = 4 * scale;
                        graphics.FillEllipse(brush, micX, micY, micWidth, micHeight);

                        // マイクのグリル（横線）
                        using (Pen pen = new Pen(Color.White, 1 * scale))
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                float y = micY + 3 * scale + i * 2 * scale;
                                graphics.DrawLine(pen, micX + 2 * scale, y, micX + micWidth - 2 * scale, y);
                            }
                        }
                    }

                    // マイクスタンド部分
                    using (Pen standPen = new Pen(standColor, 2 * scale))
                    {
                        // 縦線
                        float centerX = size / 2;
                        float standTop = 18 * scale;
                        float standBottom = 28 * scale;
                        graphics.DrawLine(standPen, centerX, standTop, centerX, standBottom);

                        // U字型の接続部
                        float arcWidth = 8 * scale;
                        float arcHeight = 6 * scale;
                        float arcX = centerX - arcWidth / 2;
                        float arcY = 14 * scale;
                        graphics.DrawArc(standPen, arcX, arcY, arcWidth, arcHeight, 0, 180);

                        // 底部の横線
                        graphics.DrawLine(standPen, centerX - 6 * scale, standBottom, centerX + 6 * scale, standBottom);
                    }

                    // 音波のアクセント（オプション）
                    using (Pen wavePen = new Pen(Color.FromArgb(150, micColor), 1.5f * scale))
                    {
                        // 右側の音波
                        float waveX = size / 2 + 8 * scale;
                        for (int i = 0; i < 2; i++)
                        {
                            float waveY1 = 8 * scale + i * 6 * scale;
                            float waveY2 = 14 * scale + i * 6 * scale;
                            graphics.DrawArc(wavePen, waveX, waveY1, 4 * scale, waveY2 - waveY1, -90, 180);
                        }
                    }

                    // BitmapからIconを生成
                    IntPtr hIcon = bitmap.GetHicon();
                    Icon icon = Icon.FromHandle(hIcon);

                    Console.WriteLine("[IconGenerator] マイクアイコンの生成が完了しました");
                    return icon;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IconGenerator] エラー - アイコン生成に失敗: {ex.Message}");
                // フォールバック: システムデフォルトアイコン
                return SystemIcons.Application;
            }
        }

        /// <summary>
        /// ウィンドウ用のアイコンソースを生成
        /// </summary>
        /// <returns>WPF用のImageSource</returns>
        public static System.Windows.Media.ImageSource CreateWindowIconSource()
        {
            try
            {
                Console.WriteLine("[IconGenerator] ウィンドウアイコンを生成中...");

                using (Icon icon = CreateMicrophoneIcon(48))
                using (Bitmap bitmap = icon.ToBitmap())
                {
                    IntPtr hBitmap = bitmap.GetHbitmap();

                    try
                    {
                        var imageSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap,
                            IntPtr.Zero,
                            System.Windows.Int32Rect.Empty,
                            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                        Console.WriteLine("[IconGenerator] ウィンドウアイコンの生成が完了しました");
                        return imageSource;
                    }
                    finally
                    {
                        // ネイティブリソースを解放
                        DeleteObject(hBitmap);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IconGenerator] エラー - ウィンドウアイコン生成に失敗: {ex.Message}");
                return null!;
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
