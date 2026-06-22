using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace NetworkService
{
    public partial class PromptBoxWindow : Window
    {
        public PromptBoxWindow()
        {
            InitializeComponent();
        }

        public PromptBoxWindow(string title, string questionText)
        {
            InitializeComponent();
            DataContext = new PromptBoxWindowViewModel(this, title, questionText);
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

    public class PromptBoxWindowViewModel : INotifyPropertyChanged
    {
        private readonly PromptBoxWindow window;
        private string title;
        private string questionText;

        public string Title
        {
            get { return title; }
            set
            {
                title = value;
                OnPropertyChanged("Title");
            }
        }

        public string QuestionText
        {
            get { return questionText; }
            set
            {
                questionText = value;
                OnPropertyChanged("QuestionText");
            }
        }

        public ICommand YesCommand { get; private set; }
        public ICommand NoCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public PromptBoxWindowViewModel(PromptBoxWindow window, string title, string questionText)
        {
            this.window = window;
            Title = title;
            QuestionText = questionText;
            YesCommand = new WindowCommand(_ => ConfirmYes());
            NoCommand = new WindowCommand(_ => ConfirmNo());
            CancelCommand = new WindowCommand(_ => ConfirmNo());
        }

        private void ConfirmYes()
        {
            window.DialogResult = true;
            window.Close();
        }

        private void ConfirmNo()
        {
            window.DialogResult = false;
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

    public class WindowCommand : ICommand
    {
        private readonly Action<object> execute;

        public WindowCommand(Action<object> execute)
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
