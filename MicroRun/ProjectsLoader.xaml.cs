using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Microsoft.Build.Logging;
using Microsoft.WindowsAPICodePack.Dialogs;



namespace MicroRun
{
    public partial class ProjectsLoader : Window
    {
        public ObservableCollection<FileViewModel> Files { get; set; } = new ObservableCollection<FileViewModel>();
        public ObservableCollection<string> Configurations { get; set; } = new ObservableCollection<string>();
        private Dictionary<string, LaunchProfile> launchProfiles;
        private Dictionary<string, Process> runningProcesses = new Dictionary<string, Process>();
        private static readonly string path = Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
           "SavedFile.json"
       );

        public ProjectsLoader()
        {
            InitializeComponent();
            FilesList.ItemsSource = Files;
            RegisterMSBuild();
            loadMultipleFile();
        }

        public async void loadMultipleFile() {
            List<FileViewModel> filesViewModels;
            filesViewModels = await LoadMultipleFromStorageAsync(path);

            foreach (var fileViewModel in filesViewModels) {
                Files.Add(fileViewModel);
                LoadLaunchConfigurations(fileViewModel);
            }
        }

        private void RegisterMSBuild()
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }

        // "Add File" button click event

        private void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            var newFile = new FileViewModel();
            Files.Add(newFile);
        }
        public async Task SaveToStorageAsync(ObservableCollection<FileViewModel> fileViewModels, string filePath)
        {
            try {
                if (!File.Exists(filePath))
                {
                    // If the file does not exist, create it with an empty list
                    var emptyListJson = JsonSerializer.Serialize(new List<FileViewModel>(), new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(filePath, emptyListJson);
                }

                // Load the existing list from the file
                var existingJson = await File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions { WriteIndented = true };

                var updatedJson = JsonSerializer.Serialize(fileViewModels, options);
                await File.WriteAllTextAsync(filePath, updatedJson);
            } catch (Exception ex) {
                MessageBox.Show("Something went wrong.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }




        public async Task<List<FileViewModel>> LoadMultipleFromStorageAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<FileViewModel>();

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<List<FileViewModel>>(json);
        }

        // "Browse" button click event for each file
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var fileViewModel = (sender as Button)?.DataContext as FileViewModel;

            if (fileViewModel != null)
            {


                try {
                    var dlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "C# Project Files (*.csproj)|*.csproj",
                        Title = "Select .NET Core Project"
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        //var newFile = new FileViewModel();
                        //Files.Add(newFile);
                        fileViewModel.FilePath = dlg.FileName;

                        //newFile.FilePath = dlg.FileName;
                        //LoadLaunchConfigurations(newFile);
                        SaveToStorageAsync(Files, path);
                        LoadLaunchConfigurations(fileViewModel);

                    }
                }
                catch (Exception ex) {
                    MessageBox.Show("SomeThing went wrong", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public async Task<FileViewModel> LoadFromStorageAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<FileViewModel>(json);
            } catch (Exception ex)
            {
                return null;

            }
        }

        // Load configurations for a specific file
        private void LoadLaunchConfigurations(FileViewModel file)
        {
            try
            {
                file.Configurations.Clear();
                string launchSettingsPath = Path.Combine(Path.GetDirectoryName(file.FilePath), "Properties", "launchSettings.json");
                if (File.Exists(launchSettingsPath))
                {
                    file.EnableStartProject = true;
                    file.EnableStartProject = true;

                    string jsonText = File.ReadAllText(launchSettingsPath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var launchSettings = JsonSerializer.Deserialize<LaunchSettings>(jsonText, options);
                    launchProfiles = launchSettings?.Profiles;

                    if (launchProfiles != null)
                    {
                        foreach (var profile in launchProfiles.Keys)
                        {
                            file.Configurations.Add(profile);
                        }
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
            catch (Exception ex) {
                MessageBox.Show("No launch profiles found in launchSettings.json.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StartAllProject_Click(object sender, RoutedEventArgs e)
        {

            {
                foreach (var fileViewModel in Files)
                {
                    if (fileViewModel.IsChecked && !fileViewModel.IsProcessRunning)
                    {
                        var selectedProfile = launchProfiles[fileViewModel.SelectedConfiguration];
                        StartProject(fileViewModel, selectedProfile);
                        fileViewModel.IsProcessRunning = true;
                        fileViewModel.IsEnableBrowserButton = false;
                    }
                }

            }
        }

        private async void StopAllProject_Click(object sender, RoutedEventArgs e) {


            foreach (var fileViewModel in Files)
            {
                if (fileViewModel.IsChecked && fileViewModel.IsProcessRunning)
                {
                    StopProject(fileViewModel.FilePath);
                    fileViewModel.IsProcessRunning = false;
                    fileViewModel.IsEnableBrowserButton = true;
                }
            }

        }

        private async void OpendSelectedProjectFolder_Click(object sender, RoutedEventArgs e) {
            var fileViewModel = (sender as Button)?.DataContext as FileViewModel;
            string initialDirectory = string.IsNullOrEmpty(fileViewModel.FilePath)
       ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) // Default to My Documents if FilePath is null or empty
       : Path.GetDirectoryName(fileViewModel.FilePath);
            try
            {
                var folderDialog = new CommonOpenFileDialog
                {
                    Title = "Select a Folder",
                    InitialDirectory = initialDirectory // Set initial directory
                };

                if (folderDialog.ShowDialog() == CommonFileDialogResult.Ok) // Modern API with better UI
                {
                    string selectedFolderPath = folderDialog.FileName;
                    MessageBox.Show($"Selected folder: {selectedFolderPath}", "Folder Selected", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Update FilePath in ViewModel if needed
                    var viewModel = DataContext as FileViewModel;
                    if (viewModel != null)
                    {
                        viewModel.FilePath = selectedFolderPath;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Something went wrong: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    

        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            try {
                var fileViewModel = ((Button)sender).Tag as FileViewModel;
                fileViewModel.IsButtonLoading = true;
                if (fileViewModel == null)
                    return;

                SaveToStorageAsync(Files, path);
                if (fileViewModel.IsProcessRunning)
                {
                    // Stop the process
                    StopProject(fileViewModel.FilePath);
                    fileViewModel.IsProcessRunning = false;
                    fileViewModel.IsEnableBrowserButton = true;
                }
                else
                {
                    // Start the process
                    var selectedProfile = launchProfiles[fileViewModel.SelectedConfiguration];
                    StartProject(fileViewModel, selectedProfile);
                    fileViewModel.IsProcessRunning = true;
                    fileViewModel.IsEnableBrowserButton = false;
                }

                fileViewModel.IsButtonLoading = false;
            }
            catch(Exception ex)
            {
                MessageBox.Show("No start project.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
       
        
        //CehckBox Button Click
        private async void CheckBoxButton_Click(object sender, RoutedEventArgs e) {
            var checkBox = sender as CheckBox;
            var fileViewModel = checkBox?.Tag as FileViewModel;
            if (fileViewModel != null && fileViewModel.IsChecked)
            {
                fileViewModel.IsChecked = true;
            }
            else {
                fileViewModel.IsChecked = false;
            }

           await SaveToStorageAsync(Files, path);
        }


        //Rmove the FileViewModel

        private async void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            var fileViewModel = ((Button)sender).Tag as FileViewModel;
            Files.Remove(fileViewModel);
            await SaveToStorageAsync(Files, path);

        }

        //open file in browser

        private async void OpenBrowser_Button(object sender, RoutedEventArgs e)
        {

        }

        // Starts the specified project with the given launch profile
        private void StartProject(FileViewModel fileViewModel, LaunchProfile profile)
        {
            try {

                if (string.IsNullOrEmpty(fileViewModel.FilePath) || fileViewModel.SelectedConfiguration == null)
                {
                    MessageBox.Show("Please select a project and a launch configuration.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Build the project
                try
                {
                    BuildProject(fileViewModel);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error building the project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                // Get the selected launch profile
                var selectedProfileName = fileViewModel.SelectedConfiguration;
                var launchProfile = launchProfiles[selectedProfileName];
                // Determine the command to run based on the commandName
                
                string commandName = launchProfile.CommandName;
                string arguments = "";
                if (commandName == "Project")
                {
                    // Run the DLL with dotnet
                    arguments = $"\"{GetOutputDllPath(fileViewModel.FilePath)}\"";
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
                    string sitePath = Path.GetDirectoryName(fileViewModel.FilePath);
                    string appUrl = launchProfile.ApplicationUrl ?? "http://localhost:8080";
                    var iisArguments = $"/path:\"{sitePath}\" /port:{GetPortFromUrl(appUrl)}";
                    StartProcess(fileViewModel.FilePath, iisExpressPath, iisArguments, launchProfile);
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
                StartProcess(fileViewModel.FilePath, "dotnet", arguments, launchProfile);
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Something want wrong.: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }


        }

        private void StartProcess(string projectPath, string fileName, string arguments, LaunchProfile launchProfile)
        {
            try
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
                // Set environment variables and start the process
                runningProcesses[projectPath] = Process.Start(startInfo);

                try
                {
                    // Launch the default browser to the specified URL
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = launchProfile.ApplicationUrl,
                        UseShellExecute = true // Required for opening URLs in a browser
                    });
                }
                catch (Exception ex)
                {
                    // Handle potential exceptions (e.g., invalid URL format)
                    Console.WriteLine($"Failed to launch browser: {ex.Message}");
                }
                // Update the IsRunning flag in the view model
                var fileViewModel = Files.FirstOrDefault(f => f.FilePath == projectPath);
                if (fileViewModel != null)
                {
                    fileViewModel.IsProcessRunning = true;
                    fileViewModel.IsEnableBrowserButton = false;
                }
            }
            catch (Exception ex) {

                MessageBox.Show($"Something went wrong: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopProject(string projectPath)
        {
            if (runningProcesses.ContainsKey(projectPath))
            {
                var process = runningProcesses[projectPath];
                if (!process.HasExited)
                {
                    process.Kill();
                }
                runningProcesses.Remove(projectPath);
            }
        }

        private void BuildProject(FileViewModel fileViewModel)
        {
            fileViewModel.StartStopButtonText ="Build...";
            var projectCollection = new ProjectCollection();
            var project = projectCollection.LoadProject(fileViewModel.FilePath);
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



        private string GetOutputDllPath(string projectPath)
        {
            string outputPath = Path.Combine(Path.GetDirectoryName(projectPath), "bin", "Debug", GetTargetFramework(projectPath)); // Adjust configuration as needed
            string outputDll = Path.Combine(outputPath, Path.GetFileNameWithoutExtension(projectPath) + ".dll");
            return outputDll;
        }
        private string GetTargetFramework(string projectPath)
        {
            var projectCollection = ProjectCollection.GlobalProjectCollection;

            // Unload the project if it is already loaded
            var loadedProjects = projectCollection.GetLoadedProjects(projectPath);
            foreach (var loadedProject in loadedProjects)
            {
                projectCollection.UnloadProject(loadedProject);
            }
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
    }

    // Helper classes for deserializing launchSettings.json
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

    public class FileViewModel : INotifyPropertyChanged
    {
        private bool isButtonLoading;
        private bool isChecked=false;
        private string filePath;
        private string selectedConfiguration;
        private string startStopButtonText = "Start"; // Default text for the button
        private bool isProcessRunning = false;// Flag to track if the process is running
        private bool isEnableStartProject = false;
        private bool isEnableBrowserButton= true;

        public string FilePath
        {
            get => filePath;
            set
            {
                filePath = value;
                OnPropertyChanged(nameof(FilePath));
                OnPropertyChanged(nameof(FileName));
            }
        }

        // Computed property to return only the file name
        public string FileName
        {
            get => string.IsNullOrEmpty(FilePath) ? string.Empty : Path.GetFileNameWithoutExtension(FilePath);
            set
            {
                if (!string.IsNullOrEmpty(FilePath))
                {
                    // Update FilePath to include the new file name while keeping the directory
                    var directory = Path.GetDirectoryName(FilePath);
                    FilePath = Path.Combine(directory, value);
                }
            }
        }
        [JsonIgnore]
        public ObservableCollection<string> Configurations { get; set; } = new ObservableCollection<string>();

        public string SelectedConfiguration
        {
            get => selectedConfiguration;
            set
            {
                selectedConfiguration = value;
                OnPropertyChanged(nameof(SelectedConfiguration));
            }
        }
        [JsonIgnore]
        public bool IsButtonLoading
        {
            get => isButtonLoading;
            set
            {
                isButtonLoading = value;
                OnPropertyChanged(nameof(IsButtonLoading));
            }
        }

        [JsonIgnore]
        public bool IsEnableBrowserButton
        {
            get => isEnableBrowserButton;
            set
            {
                isEnableBrowserButton = value;
                OnPropertyChanged(nameof(IsEnableBrowserButton));
            }
        }

        public bool EnableStartProject
        {
            get => isEnableStartProject;
            set
            {
                isEnableStartProject = value;
                OnPropertyChanged(nameof(EnableStartProject));
            }
        }

        public bool IsChecked
        {
            get => isChecked;
            set
            {
                isChecked = value;
                OnPropertyChanged(nameof(IsChecked));
            }
        }
        [JsonIgnore]
        public string StartStopButtonText
        {
            get => startStopButtonText;
            set
            {
                startStopButtonText = value;
                OnPropertyChanged(nameof(StartStopButtonText));
            }
        }
        [JsonIgnore]
        public bool IsProcessRunning
        {
            get => isProcessRunning;
            set
            {
                isProcessRunning = value;
                StartStopButtonText = isProcessRunning ? "Stop" : "Start";// Change button text based on process state
                IsEnableBrowserButton = !isProcessRunning;
                OnPropertyChanged(nameof(IsEnableBrowserButton));
                OnPropertyChanged(nameof(IsProcessRunning));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
