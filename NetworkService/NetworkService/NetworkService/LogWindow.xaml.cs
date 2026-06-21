using NetworkService.ViewModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace NetworkService
{
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
            DataContext = new LogWindowViewModel(this);
        }

        private void WindowHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            if (IsInsideButton(e.OriginalSource as DependencyObject))
            {
                return;
            }

            DragMove();
        }

        private bool IsInsideButton(DependencyObject source)
        {
            while (source != null)
            {
                if (source is ButtonBase)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }
    }

    public class LogWindowViewModel : INotifyPropertyChanged
    {
        private readonly Window logWindow;
        private readonly string logFilePath;
        private string logFilePathText;

        public ObservableCollection<string> LogEntries { get; private set; }
        public ICommand BackCommand { get; private set; }
        public ICommand ToggleFullscreenCommand { get; private set; }
        public ICommand CloseApplicationCommand { get; private set; }
        public ICommand RefreshLogCommand { get; private set; }

        public string LogFilePathText
        {
            get { return logFilePathText; }
            set
            {
                logFilePathText = value;
                OnPropertyChanged("LogFilePathText");
            }
        }

        public LogWindowViewModel(Window logWindow)
        {
            this.logWindow = logWindow;
            logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "measurements_log.txt");
            LogEntries = new ObservableCollection<string>();
            LogFilePathText = "Log file: " + logFilePath;

            BackCommand = new RelayCommand(_ => CloseLogWindow());
            ToggleFullscreenCommand = new RelayCommand(_ => ToggleFullscreen());
            CloseApplicationCommand = new RelayCommand(_ => Application.Current.Shutdown());
            RefreshLogCommand = new RelayCommand(_ => LoadLogEntries());

            LoadLogEntries();
        }

        private void CloseLogWindow()
        {
            logWindow.Close();
        }

        private void ToggleFullscreen()
        {
            logWindow.WindowState = logWindow.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void LoadLogEntries()
        {
            LogEntries.Clear();

            if (!File.Exists(logFilePath))
            {
                LogEntries.Add("Log file does not exist yet. Add entities and start MeteringSimulator to create measurements.");
                return;
            }

            string[] lines = File.ReadAllLines(logFilePath, Encoding.UTF8);

            if (lines.Length == 0)
            {
                LogEntries.Add("Log file is empty.");
                return;
            }

            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    LogEntries.Add(line);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
