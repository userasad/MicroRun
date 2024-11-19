using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Microsoft.Build.Logging;
namespace NetCoreProjectLauncher
{
    public partial class MainWindow : Window
    {
        private int rowCount = 1;
        private string projectPath;
        private Dictionary<string, LaunchProfile> launchProfiles;
        private Process runningProcess;
        public MainWindow()
        {
            RegisterMSBuild();

            InitializeComponent();
            UpdateStartStopButton();
        }

        private void RegisterMSBuild()
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "C# Project Files (*.csproj)|*.csproj",
                Title = "Select .NET Core Project"
            };
            if (dlg.ShowDialog() == true)
            {
                TextBox txtProjectPath = button.Tag as TextBox;
                string TextBoxName = txtProjectPath.Name;
                projectPath = dlg.FileName;
                txtProjectPath.Text = projectPath;
                LoadLaunchConfigurations();
            }
        }
        private void LoadLaunchConfigurations()
        {
            cmbLaunchConfigs.Items.Clear();
            string launchSettingsPath = Path.Combine(Path.GetDirectoryName(projectPath), "Properties", "launchSettings.json");
            if (File.Exists(launchSettingsPath))
            {
                string jsonText = File.ReadAllText(launchSettingsPath);
                // Configure JsonSerializer to be case-insensitive
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var launchSettings = JsonSerializer.Deserialize<LaunchSettings>(jsonText, options);
                launchProfiles = launchSettings.Profiles;
                if (launchProfiles != null)
                {
                    foreach (var profile in launchProfiles)
                    {
                        cmbLaunchConfigs.Items.Add(profile.Key);
                    }
                    if (cmbLaunchConfigs.Items.Count > 0)
                        cmbLaunchConfigs.SelectedIndex = 0;
                }
                else
                {
                    MessageBox.Show("No launch profiles found in launchSettings.json.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("launchSettings.json not found in the project.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnStartStop_Click(object sender, RoutedEventArgs e)
        {
            if (runningProcess == null || runningProcess.HasExited)
            {
                StartProject();
            }
            else
            {
                StopProject();
            }
            UpdateStartStopButton();
        }
        private void StartProject()
        {
            if (string.IsNullOrEmpty(projectPath) || cmbLaunchConfigs.SelectedItem == null)
            {
                MessageBox.Show("Please select a project and a launch configuration.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Build the project
            BuildProject();
            // Get the selected launch profile
            var selectedProfileName = cmbLaunchConfigs.SelectedItem.ToString();
            var launchProfile = launchProfiles[selectedProfileName];
            // Determine the command to run based on the commandName
            string commandName = launchProfile.CommandName;
            string arguments = "";
            if (commandName == "Project")
            {
                // Run the DLL with dotnet
                arguments = $"\"{GetOutputDllPath()}\"";
            }
            else if (commandName == "IISExpress")
            {
                // Start IIS Express with the application URL
                string iisExpressPath = GetIISExpressPath();
                if (iisExpressPath == null)
                {
                    MessageBox.Show("IIS Express not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                string sitePath = Path.GetDirectoryName(projectPath);
                string appUrl = launchProfile.ApplicationUrl ?? "http://localhost:8080";
                var iisArguments = $"/path:\"{sitePath}\" /port:{GetPortFromUrl(appUrl)}";
                StartProcess(iisExpressPath, iisArguments, launchProfile);
                return;
            }
            else
            {
                MessageBox.Show($"Unsupported commandName: {commandName}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Append command-line arguments from the launch profile
            if (!string.IsNullOrEmpty(launchProfile.CommandLineArgs))
            {
                arguments += $" {launchProfile.CommandLineArgs}";
            }
            StartProcess("dotnet", arguments, launchProfile);
        }
        private void StartProcess(string fileName, string arguments, LaunchProfile launchProfile)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = Path.GetDirectoryName(projectPath)
            };
            // Set environment variables
            if (launchProfile.EnvironmentVariables != null)
            {
                foreach (var envVar in launchProfile.EnvironmentVariables)
                {
                    startInfo.Environment[envVar.Key] = envVar.Value;
                }
            }

            if (!string.IsNullOrEmpty(launchProfile.ApplicationUrl))
            {
                startInfo.Environment["ASPNETCORE_URLS"] = launchProfile.ApplicationUrl;
            }
            // Start the process
            runningProcess = Process.Start(startInfo);
            // Optionally launch the browser if specified
            //if (launchProfile.LaunchBrowser == true && !string.IsNullOrEmpty(launchProfile.LaunchUrl))
            //{
            //    Process.Start(new ProcessStartInfo
            //    {
            //        FileName = launchProfile.LaunchUrl,
            //        UseShellExecute = true
            //    });
            //}
        }
        private void StopProject()
        {
            if (runningProcess != null && !runningProcess.HasExited)
            {
                runningProcess.Kill();
                runningProcess = null;
            }
        }
        private void BuildProject()
        {
            var projectCollection = new ProjectCollection();
            var project = projectCollection.LoadProject(projectPath);
            var buildParameters = new BuildParameters(projectCollection)
            {
                Loggers = new List<ILogger> { new ConsoleLogger(LoggerVerbosity.Minimal) }
            };
            var buildRequest = new BuildRequestData(project.CreateProjectInstance(), new[] { "Build" });
            var buildResult = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequest);
            if (buildResult.OverallResult != BuildResultCode.Success)
            {
                MessageBox.Show("Build failed. Please check the output for errors.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private string GetOutputDllPath()
        {
            string outputPath = Path.Combine(Path.GetDirectoryName(projectPath), "bin", "Debug", GetTargetFramework()); // Adjust configuration as needed
            string outputDll = Path.Combine(outputPath, Path.GetFileNameWithoutExtension(projectPath) + ".dll");
            return outputDll;
        }
        private string GetTargetFramework()
        {
            var project = new Project(projectPath);
            return project.GetPropertyValue("TargetFramework") ?? "net6.0";
        }
        private string GetIISExpressPath()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string iisExpressPath = Path.Combine(programFiles, "IIS Express", "iisexpress.exe");
            return File.Exists(iisExpressPath) ? iisExpressPath : null;
        }
        private int GetPortFromUrl(string url)
        {
            var uri = new Uri(url.Split(';')[0]); // Handle multiple URLs
            return uri.Port;
        }
        private void UpdateStartStopButton()
        {
            if (runningProcess != null && !runningProcess.HasExited)
            {
                btnStartStop.Content = "Stop";
            }
            else
            {
                btnStartStop.Content = "Start";
            }
        }

        private void BtnAddMore_Click(object sender, RoutedEventArgs e)
        {

            MainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Create a new Grid for the new row
            var newGrid = new Grid();
            newGrid.Margin = new Thickness(0, 10, 0, 0);
            newGrid.SetValue(Grid.RowProperty, rowCount); // Set the row index

            // Define ColumnDefinitions for the new row (same as existing one)
            newGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            newGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) });
            newGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            newGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            newGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) });
            newGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Add controls for the new row
            var projectLabel = new Label { Content = "Project:" };
            var projectPathTextBox = new TextBox { Name = $"txtProjectPath{rowCount}", IsReadOnly = true };
            var browseButton = new Button { Content = "Browse...", Margin = new Thickness(5, 0, 0, 0) };
            var configLabel = new Label { Content = "Configuration:" };
            var configComboBox = new ComboBox { Name = $"cmbLaunchConfigs{rowCount}" };
            var startButton = new Button { Content = "Start", Margin = new Thickness(5, 0, 0, 0) };

            // Add the new grid to the main grid
            Grid.SetColumn(projectLabel, 0);
            Grid.SetColumn(projectPathTextBox, 1);
            Grid.SetColumn(browseButton, 2);
            Grid.SetColumn(configLabel, 3);
            Grid.SetColumn(configComboBox, 4);
            Grid.SetColumn(browseButton, 5);

            newGrid.Children.Add(projectLabel);
            newGrid.Children.Add(projectPathTextBox);
            newGrid.Children.Add(browseButton);
            newGrid.Children.Add(configLabel);
            newGrid.Children.Add(configComboBox);
            newGrid.Children.Add(startButton);

            // Add the new grid to the main grid
            MainGrid.Children.Add(newGrid);
            // Increment rowCount to ensure the next set of controls goes to the next row
            rowCount++;

        }

    }
    // Classes for deserializing launchSettings.json
    public class LaunchSettings
    {
        public IisSettings IisSettings { get; set; }
        public Dictionary<string, LaunchProfile> Profiles { get; set; }
    }
    public class IisSettings
    {
        public bool WindowsAuthentication { get; set; }
        public bool AnonymousAuthentication { get; set; }
        public IisExpressSettings IisExpress { get; set; }
    }
    public class IisExpressSettings
    {
        public string ApplicationUrl { get; set; }
        public int SslPort { get; set; }
    }
    public class LaunchProfile
    {
        public string CommandName { get; set; }
        public bool? LaunchBrowser { get; set; }
        public string LaunchUrl { get; set; }
        public string ApplicationUrl { get; set; }
        public string CommandLineArgs { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; }
    }
}