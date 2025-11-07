using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace OOS.Game
{
    public partial class VideoWindow : Window
    {
        private TaskCompletionSource<bool>? _tcs;

        // Size bounds (tweak to taste)
        private const double MIN_W = 880;   // slightly larger than intro
        private const double MIN_H = 540;
        private const double MAX_W = 1100;  // cap so huge videos don't fill the screen
        private const double MAX_H = 700;

        public VideoWindow(string path, double? left = null, double? top = null)
        {
            InitializeComponent();

            // — Position to match the intro (safe fallback to centering) —
            if (left.HasValue && top.HasValue &&
                !double.IsNaN(left.Value) && !double.IsInfinity(left.Value) &&
                !double.IsNaN(top.Value) && !double.IsInfinity(top.Value))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left.Value;
                Top = top.Value;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // — Resolve path (accept relative or absolute) —
            string full = Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));

            video.Source = new Uri(full, UriKind.Absolute);

            // — Sizing once media is ready —
            video.MediaOpened += (s, e) =>
            {
                int natW = video.NaturalVideoWidth;
                int natH = video.NaturalVideoHeight;

                if (natW > 0 && natH > 0)
                {
                    // Scale uniformly to fit within MAX bounds (no cropping), then enforce MIN bounds
                    double kW = MAX_W / natW;
                    double kH = MAX_H / natH;
                    double k = Math.Min(Math.Min(kW, kH), 1.0); // don't upscale above MAX

                    double targetW = Math.Max(natW * k, MIN_W);
                    double targetH = Math.Max(natH * k, MIN_H);

                    video.Width = targetW;
                    video.Height = targetH;
                }
                else
                {
                    // If unknown, use a sensible default
                    video.Width = MIN_W;
                    video.Height = MIN_H;
                }
            };

            video.MediaEnded += (s, e) => _tcs?.TrySetResult(true);
            video.MediaFailed += (s, e) =>
            {
                MessageBox.Show(
                    $"Failed to play video:\n{full}\n\n{e.ErrorException?.Message}",
                    "Office of Shadows");
                _tcs?.TrySetResult(true);
            };
        }

        public Task PlayAsync()
        {
            _tcs = new TaskCompletionSource<bool>();
            Show();
            video.Play();
            return _tcs.Task.ContinueWith(_ => Dispatcher.Invoke(Close));
        }

        protected override void OnClosed(EventArgs e)
        {
            try { video.Stop(); } catch { /* ignore */ }
            base.OnClosed(e);
        }
    }
}
