
using Microsoft.Win32;
using Reo.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Reo.Studio
{
    public partial class MainWindow : Window
    {
        private string? _currentFile;
        private string _buildDir;
        private string? _lastExe;

        public MainWindow()
        {
            InitializeComponent();
            _buildDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReoStudio", "Builds");
            Directory.CreateDirectory(_buildDir);
            var nowExe = Path.Combine(_buildDir, "program.exe");
            ExePathBox.Text = nowExe;

            Editor.Text = SampleProgram();
            BuildOutput.Text = "Ready.";
            RunOutput.Text = "";
            using (var db = new Data.StudioDb())
            {
                db.Database.EnsureCreated();
            }
        }

        private string SampleProgram() => """
# Welcome to Reo Studio!
let name be ""Joy"".
say ""Hello, "" plus name plus ""!"".

to add(a, b):
    return a plus b.
end.

let nums be range(1, 5).
for each item in nums:
    say item.
end for each.

let total be add(40, 2).
say ""Answer: "" plus total.

repeat 3 times:
    say ""tick"".
end repeat.

let today be now().
say ""Now: "" plus today.
say format_now(""yyyy-MM-dd HH:mm:ss"").
""";

        private void NewBtn_Click(object sender, RoutedEventArgs e)
        {
            _currentFile = null;
            Editor.Text = "";
            BuildOutput.Text = "New file.";
            RunOutput.Text = "";
        }

        private void OpenBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Reo files (*.reo)|*.reo|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                _currentFile = dlg.FileName;
                Editor.Text = File.ReadAllText(_currentFile);
                BuildOutput.Text = $"Opened {_currentFile}";
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFile))
            {
                var dlg = new SaveFileDialog { Filter = "Reo files (*.reo)|*.reo|All files (*.*)|*.*", FileName = "program.reo" };
                if (dlg.ShowDialog() == true)
                    _currentFile = dlg.FileName;
                else
                    return;
            }
            File.WriteAllText(_currentFile!, Editor.Text, Encoding.UTF8);
            BuildOutput.Text = $"Saved {_currentFile}";

            using var db = new Data.StudioDb();
            var prog = db.UpsertProgram(_currentFile!, Editor.Text);
            db.SaveChanges();
        }

        private async void CompileBtn_Click(object sender, RoutedEventArgs e)
        {
            var exePath = string.IsNullOrWhiteSpace(ExePathBox.Text) ? Path.Combine(_buildDir, "program.exe") : ExePathBox.Text;
            Directory.CreateDirectory(Path.GetDirectoryName(exePath)!);

            var (ok, csharp, diags) = ReoCompiler.CompileToExe(Editor.Text, exePath);
            _lastExe = ok ? exePath : null;

            BuildOutput.Text = ok ? $"Build OK -> {exePath}" : $"Build FAILED\n{diags}";

            using var db = new Data.StudioDb();
            var prog = _currentFile != null ? db.UpsertProgram(_currentFile!, Editor.Text) : db.UpsertProgram("(unsaved)", Editor.Text);
            db.Builds.Add(new Data.BuildRecord { ProgramRecordId = prog.Id, OutputPath = exePath, Success = ok, Diagnostics = ok ? "" : diags, BuiltAt = DateTime.UtcNow });
            db.SaveChanges();
        }

        private async void RunBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastExe) || !File.Exists(_lastExe))
            {
                BuildOutput.Text = "No built EXE found. Click Compile first.";
                return;
            }
            RunOutput.Text = "Running...\n";
            var sb = new StringBuilder();
            var psi = new ProcessStartInfo
            {
                FileName = _lastExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (s, ev) => { if (ev.Data != null) Dispatcher.Invoke(() => RunOutput.AppendText(ev.Data + "\n")); };
            proc.ErrorDataReceived += (s, ev) => { if (ev.Data != null) Dispatcher.Invoke(() => RunOutput.AppendText("[ERR] " + ev.Data + "\n")); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await Task.Run(() => proc.WaitForExit());

            using var db = new Data.StudioDb();
            var prog = _currentFile != null ? db.GetProgramByPath(_currentFile!) : db.UpsertProgram("(unsaved)", Editor.Text);
            db.Runs.Add(new Data.RunRecord { ProgramRecordId = prog.Id, Output = RunOutput.Text, ExitCode = proc.ExitCode, RanAt = DateTime.UtcNow });
            db.SaveChanges();
        }
    }
}
