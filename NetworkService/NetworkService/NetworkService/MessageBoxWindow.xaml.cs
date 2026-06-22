using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace NetworkService
{
    public partial class MessageBoxWindow : Window
    {
        public MessageBoxWindow()
        {
            InitializeComponent();
        }

        public MessageBoxWindow(string title, string messageText, string confirmButtonText)
        {
            InitializeComponent();
            DataContext = new MessageBoxWindowViewModel(this, title, messageText, confirmButtonText);
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

    public class MessageBoxWindowViewModel : INotifyPropertyChanged
    {
        private readonly MessageBoxWindow window;
        private string title;
        private string messageText;
        private string confirmButtonText;

        public string Title
        {
            get { return title; }
            set
            {
                title = value;
                OnPropertyChanged("Title");
            }
        }

        public string MessageText
        {
            get { return messageText; }
            set
            {
                messageText = value;
                OnPropertyChanged("MessageText");
            }
        }

        public string ConfirmButtonText
        {
            get { return confirmButtonText; }
            set
            {
                confirmButtonText = value;
                OnPropertyChanged("ConfirmButtonText");
            }
        }

        public ICommand ConfirmCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }

        public MessageBoxWindowViewModel(MessageBoxWindow window, string title, string messageText, string confirmButtonText)
        {
            this.window = window;
            Title = title;
            MessageText = messageText;
            ConfirmButtonText = confirmButtonText;
            ConfirmCommand = new MessageWindowCommand(_ => CloseWindow());
            CloseCommand = new MessageWindowCommand(_ => CloseWindow());
        }

        private void CloseWindow()
        {
            window.Close();
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

    public class MessageWindowCommand : ICommand
    {
        private readonly Action<object> execute;

        public MessageWindowCommand(Action<object> execute)
        {
            this.execute = execute;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            execute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
