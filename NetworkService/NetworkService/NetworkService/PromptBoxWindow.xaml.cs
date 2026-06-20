using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace NetworkService
{
    /// <summary>
    /// Interaction logic for PromptBoxWindow.xaml
    /// </summary>
    public partial class PromptBoxWindow : Window
    {
        public PromptBoxWindow()
        {
            InitializeComponent();
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
}
