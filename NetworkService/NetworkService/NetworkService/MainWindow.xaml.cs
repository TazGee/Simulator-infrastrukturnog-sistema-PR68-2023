using NetworkService.Models;
using NetworkService.ViewModel;
using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NetworkService
{
    public partial class MainWindow : Window
    {
        private Point dragStartPoint;
        private MeracPotrosnje draggedEntity;
        private FrameworkElement dragSourceElement;

        public MainWindow()
        {
            InitializeComponent();

            MainWindowViewModel viewModel = new MainWindowViewModel();
            DataContext = viewModel;
            viewModel.NetworkDisplay.Connections.CollectionChanged += NetworkConnections_CollectionChanged;
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

        private void AvailableEntityDragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FrameworkElement handle = sender as FrameworkElement;

            if (handle == null)
            {
                return;
            }

            MeracPotrosnje entity = handle.DataContext as MeracPotrosnje;

            if (entity == null)
            {
                return;
            }

            StartDragPreparation(handle, entity, e);
        }

        private void PlacedEntityDragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FrameworkElement handle = sender as FrameworkElement;

            if (handle == null)
            {
                return;
            }

            NetworkGridSlotViewModel slot = handle.DataContext as NetworkGridSlotViewModel;

            if (slot == null || slot.Entity == null)
            {
                return;
            }

            StartDragPreparation(handle, slot.Entity, e);
        }

        private void StartDragPreparation(FrameworkElement handle, MeracPotrosnje entity, MouseButtonEventArgs e)
        {
            draggedEntity = entity;
            dragSourceElement = handle;
            dragStartPoint = e.GetPosition(this);
            handle.CaptureMouse();
            e.Handled = true;
        }

        private void DragHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedEntity == null || dragSourceElement == null)
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                ClearDragState();
                return;
            }

            Point currentPosition = e.GetPosition(this);
            double horizontalMove = Math.Abs(currentPosition.X - dragStartPoint.X);
            double verticalMove = Math.Abs(currentPosition.Y - dragStartPoint.Y);

            if (horizontalMove < SystemParameters.MinimumHorizontalDragDistance &&
                verticalMove < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            try
            {
                dragSourceElement.ReleaseMouseCapture();
                Mouse.OverrideCursor = Cursors.SizeAll;
                DragDrop.DoDragDrop(dragSourceElement, draggedEntity, DragDropEffects.Move);
            }
            finally
            {
                ClearDragState();
            }
        }

        private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ClearDragState();
        }

        private void NetworkGridSlot_DragOver(object sender, DragEventArgs e)
        {
            MeracPotrosnje entity = e.Data.GetData(typeof(MeracPotrosnje)) as MeracPotrosnje;
            NetworkGridSlotViewModel targetSlot = GetSlotFromSender(sender);
            MainWindowViewModel viewModel = DataContext as MainWindowViewModel;

            if (viewModel != null && viewModel.NetworkDisplay.CanPlaceEntityInSlot(entity, targetSlot))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void NetworkGridSlot_Drop(object sender, DragEventArgs e)
        {
            MeracPotrosnje entity = e.Data.GetData(typeof(MeracPotrosnje)) as MeracPotrosnje;
            NetworkGridSlotViewModel targetSlot = GetSlotFromSender(sender);
            MainWindowViewModel viewModel = DataContext as MainWindowViewModel;

            if (viewModel != null && viewModel.NetworkDisplay.PlaceEntityInSlot(entity, targetSlot))
            {
                e.Effects = DragDropEffects.Move;
                UpdateConnectionLinesDelayed();
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            ClearDragState();
            e.Handled = true;
        }

        private void PlacedEntityCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsInsideButton(e.OriginalSource as DependencyObject))
            {
                return;
            }

            NetworkGridSlotViewModel clickedSlot = GetSlotFromSender(sender);
            MainWindowViewModel viewModel = DataContext as MainWindowViewModel;

            if (clickedSlot == null || viewModel == null)
            {
                return;
            }

            viewModel.NetworkDisplay.HandleSlotClickForConnection(clickedSlot);
            UpdateConnectionLinesDelayed();
            e.Handled = true;
        }

        private NetworkGridSlotViewModel GetSlotFromSender(object sender)
        {
            FrameworkElement element = sender as FrameworkElement;

            if (element == null)
            {
                return null;
            }

            return element.DataContext as NetworkGridSlotViewModel;
        }

        private void NetworkGridItemsControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateConnectionLinesDelayed();
        }

        private void NetworkConnections_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateConnectionLinesDelayed();
        }

        private void UpdateConnectionLinesDelayed()
        {
            Dispatcher.BeginInvoke(new Action(UpdateConnectionLines), DispatcherPriority.Background);
        }

        private void UpdateConnectionLines()
        {
            MainWindowViewModel viewModel = DataContext as MainWindowViewModel;

            if (viewModel == null || ConnectionCanvas == null || NetworkGridItemsControl == null)
            {
                return;
            }

            ConnectionCanvas.Children.Clear();

            foreach (NetworkConnectionViewModel connection in viewModel.NetworkDisplay.Connections)
            {
                NetworkGridSlotViewModel firstSlot = viewModel.NetworkDisplay.GetSlotByEntity(connection.FirstEntity);
                NetworkGridSlotViewModel secondSlot = viewModel.NetworkDisplay.GetSlotByEntity(connection.SecondEntity);

                Point firstCenter;
                Point secondCenter;

                if (!TryGetSlotCenter(firstSlot, out firstCenter) || !TryGetSlotCenter(secondSlot, out secondCenter))
                {
                    continue;
                }

                Line line = new Line
                {
                    X1 = firstCenter.X,
                    Y1 = firstCenter.Y,
                    X2 = secondCenter.X,
                    Y2 = secondCenter.Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                    StrokeThickness = 3,
                    SnapsToDevicePixels = true
                };

                ConnectionCanvas.Children.Add(line);
            }
        }

        private bool TryGetSlotCenter(NetworkGridSlotViewModel slot, out Point center)
        {
            center = new Point(0, 0);

            if (slot == null || NetworkGridItemsControl == null || ConnectionCanvas == null)
            {
                return false;
            }

            DependencyObject container = NetworkGridItemsControl.ItemContainerGenerator.ContainerFromItem(slot);
            FrameworkElement element = container as FrameworkElement;

            if (element == null || element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                return false;
            }

            GeneralTransform transform = element.TransformToAncestor(NetworkGridItemsControl);
            Rect bounds = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            return true;
        }

        private void ClearDragState()
        {
            if (dragSourceElement != null)
            {
                dragSourceElement.ReleaseMouseCapture();
            }

            draggedEntity = null;
            dragSourceElement = null;
            Mouse.OverrideCursor = null;
        }
    }
}
