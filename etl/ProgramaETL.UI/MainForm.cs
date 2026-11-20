using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProgramaETL.UI
{
    internal sealed class MainForm : Form
    {
        private readonly TextBox inputPath = new TextBox();
        private readonly TextBox outputPath = new TextBox();
        private readonly Button runButton = new Button();
        private readonly Button openSummaryButton = new Button();
        private readonly Button openRejectedButton = new Button();
        private readonly Label statusLabel = new Label();
        private readonly TextBox resultBox = new TextBox();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly string repositoryRoot;

        public MainForm()
        {
            repositoryRoot = FindRepositoryRoot();
            Text = "Carga ETL - Opiniones de clientes";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 560);
            Size = new Size(900, 650);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(247, 249, 252);

            BuildLayout();
            string bundledCsv = Path.Combine(repositoryRoot, "DatosCSV");
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            inputPath.Text = Directory.Exists(bundledCsv) ? bundledCsv : documents;
            outputPath.Text = Path.Combine(documents, "Sistema-Analisis-Opiniones", "etl-output");
            UpdateReportButtons();
        }

        private void BuildLayout()
        {
            var heading = new Label
            {
                Text = "Carga de datos de opiniones",
                Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 45, 76),
                AutoSize = true,
                Location = new Point(28, 22)
            };
            var subtitle = new Label
            {
                Text = "SQL Server · SistemaBDTransaccional · config-conexion.txt",
                ForeColor = Color.FromArgb(91, 107, 128),
                AutoSize = true,
                Location = new Point(31, 60)
            };
            Controls.Add(heading);
            Controls.Add(subtitle);

            AddField("Carpeta con los CSV", inputPath, 104, "Examinar...", ChooseInputFolder);
            AddField("Carpeta para los reportes", outputPath, 158, "Examinar...", ChooseOutputFolder);

            runButton.Text = "Ejecutar carga";
            runButton.Location = new Point(30, 220);
            runButton.Size = new Size(150, 38);
            runButton.FlatStyle = FlatStyle.Flat;
            runButton.FlatAppearance.BorderSize = 0;
            runButton.BackColor = Color.FromArgb(35, 104, 196);
            runButton.ForeColor = Color.White;
            runButton.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            runButton.Click += async (sender, args) => await RunEtlAsync();
            Controls.Add(runButton);

            openSummaryButton.Text = "Abrir resumen";
            openSummaryButton.Location = new Point(194, 220);
            openSummaryButton.Size = new Size(130, 38);
            openSummaryButton.Click += (sender, args) => OpenReport("resumen_etl.txt");
            Controls.Add(openSummaryButton);

            openRejectedButton.Text = "Abrir rechazados";
            openRejectedButton.Location = new Point(338, 220);
            openRejectedButton.Size = new Size(140, 38);
            openRejectedButton.Click += (sender, args) => OpenReport("registros_rechazados.csv");
            Controls.Add(openRejectedButton);

            statusLabel.Text = "Listo para ejecutar. La carga mantiene la integridad referencial y genera reportes.";
            statusLabel.ForeColor = Color.FromArgb(74, 88, 109);
            statusLabel.AutoEllipsis = true;
            statusLabel.Location = new Point(31, 275);
            statusLabel.Size = new Size(820, 24);
            Controls.Add(statusLabel);

            progress.Location = new Point(31, 303);
            progress.Size = new Size(820, 8);
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 25;
            progress.Visible = false;
            Controls.Add(progress);

            resultBox.Location = new Point(30, 330);
            resultBox.Size = new Size(820, 245);
            resultBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            resultBox.Multiline = true;
            resultBox.ReadOnly = true;
            resultBox.ScrollBars = ScrollBars.Vertical;
            resultBox.BackColor = Color.White;
            resultBox.BorderStyle = BorderStyle.FixedSingle;
            resultBox.Font = new Font("Consolas", 9F);
            resultBox.Text = "Selecciona la carpeta donde están los seis CSV y pulsa «Ejecutar carga»." + Environment.NewLine +
                "Se mostrarán los conteos y la ubicación de los reportes al finalizar.";
            Controls.Add(resultBox);
        }

        private void AddField(string caption, TextBox textBox, int top, string buttonText, EventHandler clickHandler)
        {
            var label = new Label { Text = caption, Location = new Point(31, top), AutoSize = true, ForeColor = Color.FromArgb(52, 67, 88) };
            textBox.Location = new Point(31, top + 22);
            textBox.Size = new Size(690, 25);
            textBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            var button = new Button { Text = buttonText, Location = new Point(735, top + 18), Size = new Size(115, 31), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            button.Click += clickHandler;
            Controls.Add(label);
            Controls.Add(textBox);
            Controls.Add(button);
        }

        private void ChooseInputFolder(object sender, EventArgs args)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Selecciona la carpeta que contiene los seis archivos CSV";
                dialog.SelectedPath = Directory.Exists(inputPath.Text) ? inputPath.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (dialog.ShowDialog(this) == DialogResult.OK) inputPath.Text = dialog.SelectedPath;
            }
        }

        private void ChooseOutputFolder(object sender, EventArgs args)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Selecciona dónde guardar el resumen y los rechazos";
                dialog.SelectedPath = Directory.Exists(outputPath.Text) ? outputPath.Text : repositoryRoot;
                if (dialog.ShowDialog(this) == DialogResult.OK) outputPath.Text = dialog.SelectedPath;
            }
        }

        private async Task RunEtlAsync()
        {
            string sourceFolder = inputPath.Text.Trim();
            string reportFolder = outputPath.Text.Trim();
            if (!Directory.Exists(sourceFolder))
            {
                MessageBox.Show(this, "La carpeta de CSV no existe.", "Carpeta inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (String.IsNullOrWhiteSpace(reportFolder))
            {
                MessageBox.Show(this, "Selecciona una carpeta para guardar los reportes.", "Falta la carpeta de salida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            runButton.Enabled = false;
            openSummaryButton.Enabled = false;
            openRejectedButton.Enabled = false;
            progress.Visible = true;
            resultBox.Clear();
            statusLabel.Text = "Conectando a SQL Server y procesando los archivos...";
            try
            {
                Directory.CreateDirectory(reportFolder);
                string consoleApp = Path.Combine(Application.StartupPath, "ProgramaETL.Console.exe");
                if (!File.Exists(consoleApp)) throw new FileNotFoundException("No se encontró el motor ETL. Compila el proyecto ProgramaETL.UI para copiarlo junto a la interfaz.", consoleApp);

                var start = new ProcessStartInfo
                {
                    FileName = consoleApp,
                    Arguments = Quote(sourceFolder) + " " + Quote(reportFolder),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var process = new Process { StartInfo = start })
                {
                    process.Start();
                    Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                    Task<string> stderr = process.StandardError.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit());
                    string output = await stdout;
                    string error = await stderr;
                    resultBox.Text = String.IsNullOrWhiteSpace(output) ? error : output + (String.IsNullOrWhiteSpace(error) ? String.Empty : Environment.NewLine + error);
                    if (process.ExitCode == 0)
                    {
                        statusLabel.Text = "Carga finalizada. Revisa el resumen y el detalle de rechazados.";
                        statusLabel.ForeColor = Color.FromArgb(25, 125, 77);
                    }
                    else
                    {
                        statusLabel.Text = "La carga terminó con errores. Lee el mensaje y revisa la conexión, los CSV y el esquema.";
                        statusLabel.ForeColor = Color.FromArgb(176, 63, 50);
                    }
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = "No se pudo ejecutar el ETL.";
                statusLabel.ForeColor = Color.FromArgb(176, 63, 50);
                resultBox.Text = ex.Message;
            }
            finally
            {
                progress.Visible = false;
                runButton.Enabled = true;
                UpdateReportButtons();
            }
        }

        private void OpenReport(string fileName)
        {
            string report = Path.Combine(outputPath.Text.Trim(), fileName);
            if (!File.Exists(report))
            {
                MessageBox.Show(this, "Todavía no existe el archivo. Ejecuta primero la carga.", "Reporte no encontrado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Process.Start(new ProcessStartInfo { FileName = report, UseShellExecute = true });
        }

        private void UpdateReportButtons()
        {
            string folder = outputPath.Text.Trim();
            openSummaryButton.Enabled = Directory.Exists(folder) && File.Exists(Path.Combine(folder, "resumen_etl.txt"));
            openRejectedButton.Enabled = Directory.Exists(folder) && File.Exists(Path.Combine(folder, "registros_rechazados.csv"));
        }

        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Application.StartupPath);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "README.md"))) return directory.FullName;
                directory = directory.Parent;
            }
            return Application.StartupPath;
        }
    }
}
