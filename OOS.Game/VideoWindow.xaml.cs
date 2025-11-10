using OOS.Shared;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OOS.Game
{
    public partial class VideoWindow : Window
    {
        public VideoWindow()
        {
            InitializeComponent();
            TryStartVideo();
        }

        private void TryStartVideo()
        {
            try
            {
                // Video expected at: <exeDir>\Assets\excoworker_clip.mp4
                var exeDir = AppContext.BaseDirectory;
                var videoPath = Path.Combine(exeDir, "Assets", "excoworker_clip.mp4");

                if (!File.Exists(videoPath))
                {
                    SharedLogger.Warn("Intro video not found at: " + videoPath);
                    FinishVideoFlow(); // Fail gracefully into the game
                    return;
                }

                var uri = new Uri(videoPath, UriKind.Absolute);
                VideoPlayer.Source = uri;

                // Play
                VideoPlayer.LoadedBehavior = MediaState.Manual;
                VideoPlayer.UnloadedBehavior = MediaState.Manual;
                VideoPlayer.Volume = 1.0;

                // WPF sometimes needs a Dispatcher defer to actually start playback
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { VideoPlayer.Play(); }
                    catch (Exception exPlay)
                    {
                        SharedLogger.Warn("Video playback failed to start:\n" + exPlay);
                        FinishVideoFlow();
                    }
                }));
            }
            catch (Exception ex)
            {
                SharedLogger.Warn("TryStartVideo failed:\n" + ex);
                FinishVideoFlow();
            }
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            FinishVideoFlow();
        }

        private void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            SharedLogger.Warn("MediaFailed: " + e.ErrorException);
            FinishVideoFlow();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            FinishVideoFlow();
        }

        /// <summary>
        /// After the intro video finishes (or fails/skips), open the sandbox folder and close the window.
        /// </summary>
        private void FinishVideoFlow()
        {
            try
            {
                var settings = SettingsStore.Load();
                if (settings.OpenSandboxOnStart)
                    SandboxHelper.OpenSandboxFolder();
            }
            catch (Exception exOpen)
            {
                SharedLogger.Warn("Opening sandbox folder failed from VideoWindow:\n" + exOpen);
            }
            finally
            {
                this.Close();
            }
        }
    }
}
