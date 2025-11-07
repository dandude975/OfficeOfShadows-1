using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using OOS.Shared;

namespace OOS.Game
{
    internal class EventEngine
    {
        private FileSystemWatcher? _watcher;
        private readonly StoryController _story;
        private readonly List<TimelineItem> _timeline;

        // Singleton pattern so other classes can easily call EventEngine.Instance
        private static EventEngine? _instance;
        public static EventEngine Instance => _instance ??= new EventEngine();

        private EventEngine()
        {
            _story = new StoryController();
            _timeline = Timeline.LoadOrEmpty();
        }

        /// <summary>
        /// Starts the Event Engine and begins watching for incoming messages.
        /// </summary>
        public void Start()
        {
            SharedLogger.Info("EventEngine started.");
            _watcher = FileQueue.CreateWatcher(OnMessage);

            // Optionally trigger startup events (like checkpoint resume)
            if (_story.AtLeast("tools_opened"))
            {
                SharedLogger.Info("Resuming from checkpoint: tools_opened");
            }
        }

        /// <summary>
        /// Handles any GameMessage posted from other applications.
        /// </summary>
        private void OnMessage(GameMessage msg)
        {
            try
            {
                SharedLogger.Info($"Received message: {msg.Type} from {msg.From}");

                switch (msg.Type)
                {
                    // Example: terminal usage
                    case "terminal.command":
                        _story.Flag("used_terminal");
                        break;

                    // Example: user connected to VPN
                    case "vpn.connected":
                        _story.Flag("vpn_connected");
                        break;

                    // Example: email opened
                    case "email.read":
                        _story.Flag("email_read");
                        break;

                    // Example: fake file opened
                    case "file.opened":
                        HandleFileOpened(msg);
                        break;
                }

                // After processing, check if this message triggers a timeline action
                ProcessTimeline(msg);
            }
            catch (Exception ex)
            {
                SharedLogger.Error($"EventEngine error: {ex.Message}");
            }
        }

        /// <summary>
        /// React to a file being opened inside the sandbox folder.
        /// </summary>
        private void HandleFileOpened(GameMessage msg)
        {
            try
            {
                if (msg.Data is null) return;
                var dataJson = System.Text.Json.JsonSerializer.Serialize(msg.Data);
                SharedLogger.Info($"File opened data: {dataJson}");

                // You could add logic here for specific filenames triggering events.
                // Example: if user opens "credentials.txt", mark flag or spawn popup
            }
            catch (Exception ex)
            {
                SharedLogger.Error($"HandleFileOpened() failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes any timeline events that match the incoming message.
        /// </summary>
        private void ProcessTimeline(GameMessage msg)
        {
            foreach (var item in _timeline)
            {
                if (item.OnType == msg.Type)
                {
                    SharedLogger.Info($"Timeline triggered: {item.Id}");
                    foreach (var act in item.Do)
                    {
                        ExecuteTimelineAction(act);
                    }
                }
            }
        }

        /// <summary>
        /// Executes a specific timeline action (e.g., popup, sound, file drop, etc.)
        /// </summary>
        private void ExecuteTimelineAction(TimelineAction action)
        {
            try
            {
                switch (action.Act)
                {
                    case "show_toast":
                        var text = action.Args != null && action.Args.ContainsKey("text")
                            ? action.Args["text"]
                            : "Unknown event occurred.";
                        ShowToast(text);
                        break;

                    case "drop_file":
                        if (action.Args != null && action.Args.TryGetValue("name", out string? fileName))
                        {
                            var filePath = Path.Combine(SharedPaths.DesktopSandbox, fileName);
                            File.WriteAllText(filePath, action.Args.GetValueOrDefault("content", "No content."));
                            SharedLogger.Info($"Dropped file: {filePath}");
                        }
                        break;

                    case "checkpoint":
                        if (action.Args != null && action.Args.TryGetValue("id", out string? id))
                        {
                            _story.SetCheckpoint(id);
                        }
                        break;

                    default:
                        SharedLogger.Warn($"Unknown timeline action: {action.Act}");
                        break;
                }
            }
            catch (Exception ex)
            {
                SharedLogger.Error($"ExecuteTimelineAction() failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a Windows-style toast message.
        /// </summary>
        private void ShowToast(string message)
        {
            try
            {
                Task.Run(() =>
                {
                    MessageBox.Show(message, "Office of Shadows", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
            catch (Exception ex)
            {
                SharedLogger.Error($"ShowToast() failed: {ex.Message}");
            }
        }
    }
}
