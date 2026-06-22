using NetworkService.Models;
using NetworkService.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
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
            viewModel.NetworkDisplay.DropSlots.CollectionChanged += NetworkDropSlots_CollectionChanged;
            SubscribeToSlotChanges(viewModel);
        }

        private void SubscribeToSlotChanges(MainWindowViewModel viewModel)
        {
            foreach (NetworkGridSlotViewModel slot in viewModel.NetworkDisplay.DropSlots)
            {
                slot.PropertyChanged += NetworkGridSlot_PropertyChanged;
            }
        }

        private void NetworkDropSlots_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (object item in e.OldItems)
                {
                    NetworkGridSlotViewModel slot = item as NetworkGridSlotViewModel;

                    if (slot != null)
                    {
                        slot.PropertyChanged -= NetworkGridSlot_PropertyChanged;
                    }
                }
            }

            if (e.NewItems != null)
            {
                foreach (object item in e.NewItems)
                {
                    NetworkGridSlotViewModel slot = item as NetworkGridSlotViewModel;

                    if (slot != null)
                    {
                        slot.PropertyChanged += NetworkGridSlot_PropertyChanged;
                    }
                }
            }

            UpdateConnectionLinesDelayed();
        }

        private void NetworkGridSlot_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Entity")
            {
                UpdateConnectionLinesDelayed();
            }
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

            viewModel.NetworkEntities.SelectedEntity = clickedSlot.Entity;
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

            ConnectionCanvas.Width = NetworkGridItemsControl.ActualWidth;
            ConnectionCanvas.Height = NetworkGridItemsControl.ActualHeight;
            ConnectionCanvas.Children.Clear();

            int connectionIndex = 0;

            foreach (NetworkConnectionViewModel connection in viewModel.NetworkDisplay.Connections)
            {
                NetworkGridSlotViewModel firstSlot = viewModel.NetworkDisplay.GetSlotByEntity(connection.FirstEntity);
                NetworkGridSlotViewModel secondSlot = viewModel.NetworkDisplay.GetSlotByEntity(connection.SecondEntity);

                Rect firstBounds;
                Rect secondBounds;

                if (!TryGetSlotBounds(firstSlot, out firstBounds) || !TryGetSlotBounds(secondSlot, out secondBounds))
                {
                    continue;
                }

                List<Point> points = BuildOrthogonalConnectionPoints(firstSlot, secondSlot, firstBounds, secondBounds, connectionIndex);
                DrawConnectionPath(points, connectionIndex);
                DrawConnectionNumber(points, connectionIndex);

                connectionIndex++;
            }
        }

        private void DrawConnectionPath(List<Point> points, int connectionIndex)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }

            Polyline polyline = new Polyline
            {
                Stroke = GetConnectionBrush(connectionIndex),
                StrokeThickness = GetConnectionThickness(connectionIndex),
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                SnapsToDevicePixels = true
            };

            foreach (Point point in points)
            {
                polyline.Points.Add(point);
            }

            if (connectionIndex % 4 == 2)
            {
                polyline.StrokeDashArray = new DoubleCollection { 6, 3 };
            }
            else if (connectionIndex % 4 == 3)
            {
                polyline.StrokeDashArray = new DoubleCollection { 2, 3 };
            }

            ConnectionCanvas.Children.Add(polyline);
        }

        private void DrawConnectionNumber(List<Point> points, int connectionIndex)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }

            Point markerPoint = GetConnectionMarkerPoint(points);
            Brush connectionBrush = GetConnectionBrush(connectionIndex);

            Border marker = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = new SolidColorBrush(Color.FromRgb(232, 232, 232)),
                BorderBrush = connectionBrush,
                BorderThickness = new Thickness(2),
                Child = new TextBlock
                {
                    Text = (connectionIndex + 1).ToString(),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = connectionBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };

            Canvas.SetLeft(marker, markerPoint.X - 11);
            Canvas.SetTop(marker, markerPoint.Y - 11);
            ConnectionCanvas.Children.Add(marker);
        }

        private Point GetConnectionMarkerPoint(List<Point> points)
        {
            if (points.Count >= 3)
            {
                return points[1];
            }

            if (points.Count == 2)
            {
                Point start = points[0];
                Point end = points[1];
                double markerX = start.X + (end.X - start.X) * 0.18;
                double markerY = start.Y + (end.Y - start.Y) * 0.18;
                return new Point(markerX, markerY);
            }

            return points[0];
        }

        private SolidColorBrush GetConnectionBrush(int connectionIndex)
        {
            byte[] shades = new byte[] { 45, 70, 95, 120, 145 };
            byte shade = shades[connectionIndex % shades.Length];
            return new SolidColorBrush(Color.FromRgb(shade, shade, shade));
        }

        private double GetConnectionThickness(int connectionIndex)
        {
            if (connectionIndex % 3 == 0)
            {
                return 2.2;
            }

            if (connectionIndex % 3 == 1)
            {
                return 3.1;
            }

            return 4.0;
        }

        private List<Point> BuildOrthogonalConnectionPoints(
            NetworkGridSlotViewModel firstSlot,
            NetworkGridSlotViewModel secondSlot,
            Rect firstSlotBounds,
            Rect secondSlotBounds,
            int connectionIndex)
        {
            List<Point> points = new List<Point>();
            Rect firstCard = GetEntityCardBounds(firstSlotBounds);
            Rect secondCard = GetEntityCardBounds(secondSlotBounds);

            int firstRow = (firstSlot.SlotNumber - 1) / 3;
            int firstColumn = (firstSlot.SlotNumber - 1) % 3;
            int secondRow = (secondSlot.SlotNumber - 1) / 3;
            int secondColumn = (secondSlot.SlotNumber - 1) % 3;

            if (firstRow == secondRow)
            {
                if (firstColumn < secondColumn)
                {
                    AddUniquePoint(points, new Point(firstCard.Right, GetRectCenterY(firstCard)));
                    AddUniquePoint(points, new Point(secondCard.Left, GetRectCenterY(secondCard)));
                }
                else
                {
                    AddUniquePoint(points, new Point(firstCard.Left, GetRectCenterY(firstCard)));
                    AddUniquePoint(points, new Point(secondCard.Right, GetRectCenterY(secondCard)));
                }

                return points;
            }

            if (firstColumn == secondColumn)
            {
                bool useRightLane = firstColumn < 2;
                double laneOffset = 20 + (connectionIndex % 4) * 8;
                double laneX = useRightLane
                    ? Math.Max(firstCard.Right, secondCard.Right) + laneOffset
                    : Math.Min(firstCard.Left, secondCard.Left) - laneOffset;

                laneX = Clamp(laneX, 8, Math.Max(8, NetworkGridItemsControl.ActualWidth - 8));

                Point start = useRightLane
                    ? new Point(firstCard.Right, GetRectCenterY(firstCard))
                    : new Point(firstCard.Left, GetRectCenterY(firstCard));

                Point end = useRightLane
                    ? new Point(secondCard.Right, GetRectCenterY(secondCard))
                    : new Point(secondCard.Left, GetRectCenterY(secondCard));

                AddUniquePoint(points, start);
                AddUniquePoint(points, new Point(laneX, start.Y));
                AddUniquePoint(points, new Point(laneX, end.Y));
                AddUniquePoint(points, end);

                return points;
            }

            bool secondIsRight = secondColumn > firstColumn;
            Point horizontalStart = secondIsRight
                ? new Point(firstCard.Right, GetRectCenterY(firstCard))
                : new Point(firstCard.Left, GetRectCenterY(firstCard));

            Point horizontalEnd = secondIsRight
                ? new Point(secondCard.Left, GetRectCenterY(secondCard))
                : new Point(secondCard.Right, GetRectCenterY(secondCard));

            double middleX = (horizontalStart.X + horizontalEnd.X) / 2;
            double laneSpacing = 9 * ((connectionIndex % 3) - 1);
            middleX = Clamp(middleX + laneSpacing, 8, Math.Max(8, NetworkGridItemsControl.ActualWidth - 8));

            AddUniquePoint(points, horizontalStart);
            AddUniquePoint(points, new Point(middleX, horizontalStart.Y));
            AddUniquePoint(points, new Point(middleX, horizontalEnd.Y));
            AddUniquePoint(points, horizontalEnd);

            return points;
        }

        private void AddUniquePoint(List<Point> points, Point point)
        {
            if (points.Count > 0)
            {
                Point lastPoint = points[points.Count - 1];

                if (Math.Abs(lastPoint.X - point.X) < 0.1 && Math.Abs(lastPoint.Y - point.Y) < 0.1)
                {
                    return;
                }
            }

            points.Add(point);
        }

        private Rect GetEntityCardBounds(Rect slotBounds)
        {
            double cardWidth = Math.Min(150, Math.Max(60, slotBounds.Width - 28));
            double cardHeight = Math.Min(104, Math.Max(50, slotBounds.Height - 28));
            double cardLeft = slotBounds.Left + (slotBounds.Width - cardWidth) / 2;
            double cardTop = slotBounds.Top + (slotBounds.Height - cardHeight) / 2;
            return new Rect(cardLeft, cardTop, cardWidth, cardHeight);
        }

        private double GetRectCenterY(Rect rect)
        {
            return rect.Top + rect.Height / 2;
        }

        private double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }

        private bool TryGetSlotBounds(NetworkGridSlotViewModel slot, out Rect bounds)
        {
            bounds = new Rect(0, 0, 0, 0);

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
            bounds = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
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
