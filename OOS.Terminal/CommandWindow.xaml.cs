using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OOS.Terminal
{
    public partial class CommandWindow : Window
    {
        private string _cwd;
        private int _inputStart; // index within Screen.Text where the current input begins

        public CommandWindow()
        {
            InitializeComponent();

            // Current directory like real cmd (use sandbox if you prefer)
            _cwd = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            // _cwd = OOS.Shared.SharedPaths.DesktopSandbox; // uncomment to start in sandbox

            // Print banner (Windows-like, adjust as you want)
            WriteLine($"Microsoft Windows [Version 10.0.22631.1]");
            WriteLine($"(c) Microsoft Corporation. All rights reserved.");
            WriteLine("");

            PrintPrompt();
            MoveCaretToEnd();
        }

        // ----- Rendering helpers -----

        private void Write(string text)
        {
            Screen.AppendText(text);
        }

        private void WriteLine(string line = "")
        {
            Screen.AppendText(line + Environment.NewLine);
        }

        private void PrintPrompt()
        {
            var drive = Path.GetPathRoot(_cwd)?.TrimEnd('\\') ?? "C:";
            var rel = _cwd;
            Write($"{drive}{_cwd.Substring(drive.Length - 1)}>"); // e.g., C:\Users\Dan>
            _inputStart = Screen.Text.Length;
        }

        private void MoveCaretToEnd()
        {
            Screen.CaretIndex = Screen.Text.Length;
            Screen.Focus();
            Scroller.ScrollToEnd();
        }

        // ----- Input handling -----

        private void Screen_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Prevent editing before the prompt
            if (e.Key == Key.Back)
            {
                if (Screen.CaretIndex <= _inputStart)
                {
                    e.Handled = true;
                    return;
                }
            }

            // Enter executes the command
            if (e.Key == Key.Return)
            {
                e.Handled = true;
                var cmd = Screen.Text.Substring(_inputStart).TrimEnd('\r', '\n');
                Execute(cmd);
                return;
            }

            // Ctrl+C → new line + prompt (no process to kill)
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Screen.AppendText("^C" + Environment.NewLine);
                PrintPrompt();
                MoveCaretToEnd();
                e.Handled = true;
                return;
            }

            // Home should jump to after prompt
            if (e.Key == Key.Home)
            {
                Screen.CaretIndex = _inputStart;
                e.Handled = true;
                return;
            }
        }

        private void Screen_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Keep caret visible and lock edits before prompt
            if (Screen.CaretIndex < _inputStart)
                Screen.CaretIndex = _inputStart;

            Scroller.ScrollToEnd();
        }

        // ----- Command handling -----

        private void Execute(string input)
        {
            // Echo the enter (as cmd would)
            WriteLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                PrintPrompt();
                MoveCaretToEnd();
                return;
            }

            // Simple built-ins to feel real
            var parts = SplitArgs(input);
            var verb = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";

            switch (verb)
            {
                case "cls":
                    Screen.Clear();
                    PrintPrompt();
                    MoveCaretToEnd();
                    return;

                case "exit":
                    Close();
                    return;

                case "cd":
                    HandleCd(parts);
                    break;

                case "dir":
                    HandleDir(parts);
                    break;

                default:
                    // your fake terminal executor here; also raise shared message
                    // OOS.Shared.FileQueue.Enqueue(new OOS.Shared.GameMessage {
                    //     Type = "terminal.command", From = "Terminal", Data = new { input }
                    // });

                    // For now, mimic "not recognized" message
                    WriteLine($"'{verb}' is not recognized as an internal or external command,");
                    WriteLine("operable program or batch file.");
                    break;
            }

            PrintPrompt();
            MoveCaretToEnd();
        }

        private void HandleCd(string[] parts)
        {
            if (parts.Length == 1)
            {
                WriteLine(_cwd);
                return;
            }

            var target = parts[1];
            string next;

            if (target == "\\")
            {
                next = Path.GetPathRoot(_cwd) ?? _cwd;
            }
            else if (target == "..")
            {
                var parent = Directory.GetParent(_cwd);
                next = parent?.FullName ?? _cwd;
            }
            else if (Path.IsPathRooted(target))
            {
                next = target;
            }
            else
            {
                next = Path.Combine(_cwd, target);
            }

            if (Directory.Exists(next))
            {
                _cwd = Path.GetFullPath(next);
            }
            else
            {
                WriteLine("The system cannot find the path specified.");
            }
        }

        private void HandleDir(string[] parts)
        {
            var path = _cwd;
            if (parts.Length > 1)
            {
                var p = parts[1];
                path = Path.IsPathRooted(p) ? p : Path.Combine(_cwd, p);
            }

            if (!Directory.Exists(path))
            {
                WriteLine("File Not Found");
                return;
            }

            var di = new DirectoryInfo(path);
            WriteLine($" Volume in drive {Path.GetPathRoot(path)?.TrimEnd('\\')} has no label.");
            WriteLine($" Directory of {di.FullName}");
            WriteLine("");

            try
            {
                foreach (var d in di.EnumerateDirectories())
                    WriteLine($"{d.LastWriteTime:dd/MM/yyyy  HH:mm}    <DIR>          {d.Name}");
                foreach (var f in di.EnumerateFiles())
                    WriteLine($"{f.LastWriteTime:dd/MM/yyyy  HH:mm}           {f.Length,10} {f.Name}");
            }
            catch
            {
                WriteLine("Access is denied.");
            }

            WriteLine("");
        }

        private static string[] SplitArgs(string input)
        {
            // simple split; good enough for the illusion
            return input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
