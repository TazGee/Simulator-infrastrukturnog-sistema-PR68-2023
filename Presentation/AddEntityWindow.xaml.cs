using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Presentation
{
    /// <summary>
    /// Interaction logic for AddEntityWindow.xaml
    /// </summary>
    public partial class AddEntityWindow : Window
    {
        public AddEntityWindow()
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
