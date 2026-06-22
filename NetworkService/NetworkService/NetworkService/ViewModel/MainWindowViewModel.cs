using NetworkService.Enums;
using NetworkService.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace NetworkService.ViewModel
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly object entitiesLock = new object();
        private readonly object logLock = new object();
        private int selectedRightTabIndex;

        public List<MeracPotrosnje> listaObjekata = new List<MeracPotrosnje>();

        public NetworkEntitiesViewModel NetworkEntities { get; private set; }
        public NetworkDisplayViewModel NetworkDisplay { get; private set; }
        public MeasurementGraphViewModel MeasurementGraph { get; private set; }

        public ICommand BackCommand { get; private set; }
        public ICommand ToggleFullscreenCommand { get; private set; }
        public ICommand CloseApplicationCommand { get; private set; }
        public ICommand OpenUsageLogCommand { get; private set; }
        public ICommand OpenEntitiesTabCommand { get; private set; }
        public ICommand OpenGraphTabCommand { get; private set; }

        public int SelectedRightTabIndex
        {
            get { return selectedRightTabIndex; }
            set
            {
                selectedRightTabIndex = value;
                OnPropertyChanged("SelectedRightTabIndex");
            }
        }

        public MainWindowViewModel()
        {
            NetworkEntities = new NetworkEntitiesViewModel(this);
            NetworkDisplay = new NetworkDisplayViewModel(this, NetworkEntities.Entities);
            MeasurementGraph = new MeasurementGraphViewModel(NetworkEntities.Entities);

            BackCommand = new RelayCommand(_ => { });
            ToggleFullscreenCommand = new RelayCommand(_ => ToggleFullscreen());
            CloseApplicationCommand = new RelayCommand(_ => Application.Current.Shutdown());
            OpenUsageLogCommand = new RelayCommand(_ => OpenUsageLog());
            OpenEntitiesTabCommand = new RelayCommand(_ => SelectedRightTabIndex = 0);
            OpenGraphTabCommand = new RelayCommand(_ => SelectedRightTabIndex = 1);

            createListener();
        }

        private void ToggleFullscreen()
        {
            Window mainWindow = Application.Current.MainWindow;

            if (mainWindow == null)
            {
                return;
            }

            mainWindow.WindowState = mainWindow.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OpenUsageLog()
        {
            LogWindow logWindow = new LogWindow();
            logWindow.Owner = Application.Current.MainWindow;
            logWindow.ShowDialog();
        }

        public bool ShowPrompt(string title, string questionText)
        {
            PromptBoxWindow promptBoxWindow = new PromptBoxWindow(title, questionText);
            promptBoxWindow.Owner = Application.Current.MainWindow;
            bool? result = promptBoxWindow.ShowDialog();
            return result == true;
        }

        public void ShowMessage(string title, string messageText)
        {
            MessageBoxWindow messageBoxWindow = new MessageBoxWindow(title, messageText, "OK");
            messageBoxWindow.Owner = Application.Current.MainWindow;
            messageBoxWindow.ShowDialog();
        }

        private void createListener()
        {
            var tcp = new TcpListener(IPAddress.Any, 25675);
            tcp.Start();

            var listeningThread = new Thread(() =>
            {
                while (true)
                {
                    var tcpClient = tcp.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(param => ProcessSimulatorClient((TcpClient)param), tcpClient);
                }
            });

            listeningThread.IsBackground = true;
            listeningThread.Start();
        }

        private void ProcessSimulatorClient(TcpClient tcpClient)
        {
            using (tcpClient)
            {
                NetworkStream stream = tcpClient.GetStream();
                byte[] bytes = new byte[1024];
                int bytesRead = stream.Read(bytes, 0, bytes.Length);
                string incoming = Encoding.ASCII.GetString(bytes, 0, bytesRead);

                if (incoming.Equals("Need object count"))
                {
                    int objectCount;

                    lock (entitiesLock)
                    {
                        objectCount = listaObjekata.Count;
                    }

                    byte[] data = Encoding.ASCII.GetBytes(objectCount.ToString());
                    stream.Write(data, 0, data.Length);
                    return;
                }

                int entityIndex;
                double measure;

                if (TryProcessMeasurementMessage(incoming, out entityIndex, out measure))
                {
                    UpdateEntityMeasurement(entityIndex, measure);
                }
            }
        }

        private bool TryProcessMeasurementMessage(string message, out int entityIndex, out double measuredValue)
        {
            entityIndex = -1;
            measuredValue = 0;

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string[] messageParts = message.Trim().Split(':');

            if (messageParts.Length != 2)
            {
                return false;
            }

            string[] entityParts = messageParts[0].Split('_');

            if (entityParts.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(entityParts[1], out entityIndex))
            {
                return false;
            }

            if (double.TryParse(messageParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out measuredValue))
            {
                return true;
            }

            return double.TryParse(messageParts[1], NumberStyles.Float, CultureInfo.CurrentCulture, out measuredValue);
        }

        private void UpdateEntityMeasurement(int entityIndex, double measure)
        {
            MeracPotrosnje entity = null;

            lock (entitiesLock)
            {
                if (entityIndex >= 0 && entityIndex < listaObjekata.Count)
                {
                    entity = listaObjekata[entityIndex];
                }
            }

            if (entity == null)
            {
                return;
            }

            DateTime measurementTime = DateTime.Now;

            Application.Current.Dispatcher.Invoke(() =>
            {
                entity.HasMeasurement = true;
                entity.LastMeasure = measure;
                MeasurementGraph.AddMeasurement(entity, measure, measurementTime);
                NetworkEntities.RefreshFilter();
            });

            WriteMeasurementLog(entityIndex, entity, measure, measurementTime);
        }

        private void WriteMeasurementLog(int simulatorEntityIndex, MeracPotrosnje entity, double measure, DateTime measurementTime)
        {
            string logsFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

            if (!Directory.Exists(logsFolderPath))
            {
                Directory.CreateDirectory(logsFolderPath);
            }

            string logFilePath = Path.Combine(logsFolderPath, "measurements_log.txt");
            string status = measure >= 0.34 && measure <= 2.73 ? "VALID" : "INVALID";

            string logLine = measurementTime.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                             " | Simulator entity: Entitet_" + simulatorEntityIndex +
                             " | Entity ID: " + entity.Id +
                             " | Name: " + entity.Name +
                             " | Type: " + entity.TypeName +
                             " | Value: " + measure.ToString("F2", CultureInfo.InvariantCulture) + " kWh" +
                             " | Status: " + status +
                             Environment.NewLine;

            lock (logLock)
            {
                File.AppendAllText(logFilePath, logLine, Encoding.UTF8);
            }
        }

        public void AddEntityFromSelectedType(string selectedEntityType)
        {
            EntityType entityType = selectedEntityType == "Smart Meter"
                ? EntityType.SMART_METER
                : EntityType.INTERVAL_METER;

            EntityTypeInfo typeInfo = entityType == EntityType.SMART_METER
                ? new EntityTypeInfo("Smart Meter", "Images/smart-meter.png", EntityType.SMART_METER)
                : new EntityTypeInfo("Interval Meter", "Images/interval-meter.png", EntityType.INTERVAL_METER);

            int newId = GetNextEntityId();

            var entity = new MeracPotrosnje
            {
                Id = newId,
                Name = typeInfo.Name + " " + newId,
                Type = typeInfo,
                LastMeasure = 0,
                HasMeasurement = false
            };

            lock (entitiesLock)
            {
                listaObjekata.Add(entity);
            }

            NetworkEntities.Entities.Add(entity);
            NetworkEntities.RefreshFilter();
            NetworkDisplay.RefreshAvailableEntities(NetworkEntities.Entities);

            if (MeasurementGraph.SelectedEntity == null)
            {
                MeasurementGraph.SelectedEntity = entity;
            }

            RestartMeteringSimulatorIfPossible();
        }

        public void RemoveEntity(MeracPotrosnje entity)
        {
            if (entity == null)
            {
                return;
            }

            lock (entitiesLock)
            {
                listaObjekata.Remove(entity);
            }

            NetworkDisplay.RemoveEntityFromGrid(entity);
            NetworkEntities.Entities.Remove(entity);
            NetworkEntities.RefreshFilter();
            NetworkDisplay.RefreshAvailableEntities(NetworkEntities.Entities);
            MeasurementGraph.RemoveEntityHistory(entity);

            if (MeasurementGraph.SelectedEntity == entity)
            {
                MeasurementGraph.SelectedEntity = NetworkEntities.Entities.FirstOrDefault();
            }

            RestartMeteringSimulatorIfPossible();
        }

        private int GetNextEntityId()
        {
            lock (entitiesLock)
            {
                if (listaObjekata.Count == 0)
                {
                    return 0;
                }

                return listaObjekata.Max(entity => entity.Id) + 1;
            }
        }

        private void RestartMeteringSimulatorIfPossible()
        {
            string simulatorPath = FindMeteringSimulatorExecutablePath();

            if (string.IsNullOrWhiteSpace(simulatorPath))
            {
                return;
            }

            foreach (Process process in Process.GetProcessesByName("MeteringSimulator"))
            {
                process.Kill();
                process.WaitForExit(2000);
            }

            Process.Start(simulatorPath);
        }

        private string FindMeteringSimulatorExecutablePath()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            string debugPath = Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\..\..\..\MeteringSimulator\MeteringSimulator\bin\Debug\MeteringSimulator.exe"));

            if (File.Exists(debugPath))
            {
                return debugPath;
            }

            string releasePath = Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\..\..\..\MeteringSimulator\MeteringSimulator\bin\Release\MeteringSimulator.exe"));

            if (File.Exists(releasePath))
            {
                return releasePath;
            }

            return string.Empty;
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

    public class NetworkEntitiesViewModel : INotifyPropertyChanged
    {
        private readonly MainWindowViewModel parent;
        private MeracPotrosnje selectedEntity;
        private string selectedEntityType;
        private FilterPresetViewModel selectedSavedFilter;

        public ObservableCollection<MeracPotrosnje> Entities { get; private set; }
        public ICollectionView FilteredEntities { get; private set; }
        public ObservableCollection<FilterPresetViewModel> SavedFilters { get; private set; }
        public FilterViewModel Filter { get; private set; }

        public ICommand OpenAddEntityDialogCommand { get; private set; }
        public ICommand RemoveSelectedEntityCommand { get; private set; }
        public ICommand SaveFilterCommand { get; private set; }
        public ICommand ClearFiltersCommand { get; private set; }

        public MeracPotrosnje SelectedEntity
        {
            get { return selectedEntity; }
            set
            {
                selectedEntity = value;
                OnPropertyChanged("SelectedEntity");
            }
        }

        public string SelectedEntityType
        {
            get { return selectedEntityType; }
            set
            {
                selectedEntityType = value;
                OnPropertyChanged("SelectedEntityType");
            }
        }

        public FilterPresetViewModel SelectedSavedFilter
        {
            get { return selectedSavedFilter; }
            set
            {
                selectedSavedFilter = value;
                OnPropertyChanged("SelectedSavedFilter");

                if (selectedSavedFilter != null)
                {
                    ApplyPreset(selectedSavedFilter);
                }
            }
        }

        public NetworkEntitiesViewModel(MainWindowViewModel parent)
        {
            this.parent = parent;
            Entities = new ObservableCollection<MeracPotrosnje>();
            SavedFilters = new ObservableCollection<FilterPresetViewModel>();
            Filter = new FilterViewModel();
            Filter.SelectedType = "All";
            SelectedEntityType = "Interval Meter";

            FilteredEntities = CollectionViewSource.GetDefaultView(Entities);
            FilteredEntities.Filter = FilterEntity;
            Filter.PropertyChanged += Filter_PropertyChanged;

            OpenAddEntityDialogCommand = new RelayCommand(_ => parent.AddEntityFromSelectedType(SelectedEntityType));
            RemoveSelectedEntityCommand = new RelayCommand(_ => RemoveSelectedEntity());
            SaveFilterCommand = new RelayCommand(_ => SaveCurrentFilter());
            ClearFiltersCommand = new RelayCommand(_ => ClearFiltersWithPrompt());
        }

        private void Filter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RefreshFilter();
        }

        public void RefreshFilter()
        {
            if (FilteredEntities != null)
            {
                FilteredEntities.Refresh();
            }
        }

        private bool FilterEntity(object item)
        {
            MeracPotrosnje entity = item as MeracPotrosnje;

            if (entity == null)
            {
                return false;
            }

            bool hasTypeFilter = !string.IsNullOrWhiteSpace(Filter.SelectedType) && Filter.SelectedType != "All";
            bool hasIdFilter = HasValidIdFilter();

            if (!hasTypeFilter && !hasIdFilter)
            {
                return true;
            }

            bool typeMatches = !hasTypeFilter || entity.TypeName == Filter.SelectedType;
            bool idMatches = !hasIdFilter || EntityIdMatchesFilter(entity.Id);

            if (hasTypeFilter && hasIdFilter)
            {
                if (Filter.CombineTypeAndId)
                {
                    return typeMatches && idMatches;
                }

                return typeMatches || idMatches;
            }

            if (hasTypeFilter)
            {
                return typeMatches;
            }

            return idMatches;
        }

        private bool HasValidIdFilter()
        {
            int idValue;
            bool hasOperator = Filter.IsLessThan || Filter.IsEqual || Filter.IsGreaterThan;
            bool hasValidValue = int.TryParse(Filter.IdValue, out idValue);
            return hasOperator && hasValidValue;
        }

        private bool EntityIdMatchesFilter(int entityId)
        {
            int idValue;

            if (!int.TryParse(Filter.IdValue, out idValue))
            {
                return true;
            }

            if (Filter.IsLessThan)
            {
                return entityId < idValue;
            }

            if (Filter.IsEqual)
            {
                return entityId == idValue;
            }

            if (Filter.IsGreaterThan)
            {
                return entityId > idValue;
            }

            return true;
        }

        private void RemoveSelectedEntity()
        {
            if (SelectedEntity == null)
            {
                return;
            }

            string questionText = "Do you want to delete entity \"" + SelectedEntity.Name + "\" from the table?\n" +
                                  "All its grid position and all connected lines will also be removed.";

            if (!parent.ShowPrompt("Delete entity", questionText))
            {
                return;
            }

            parent.RemoveEntity(SelectedEntity);
            SelectedEntity = null;
        }

        private void SaveCurrentFilter()
        {
            FilterPresetViewModel preset = new FilterPresetViewModel(Filter);
            FilterPresetViewModel existingPreset = SavedFilters.FirstOrDefault(item => item.Name == preset.Name);

            if (existingPreset == null)
            {
                SavedFilters.Add(preset);
                SelectedSavedFilter = preset;
            }
            else
            {
                SelectedSavedFilter = existingPreset;
            }

            parent.ShowMessage("Filter preset saved", "Filter preset has been saved and can now be selected from the Filter preset dropdown.");
        }

        private void ClearFiltersWithPrompt()
        {
            if (!parent.ShowPrompt("Clear filters", "Do you want to clear all active filters and show all entities again?"))
            {
                return;
            }

            ClearFilters();
        }

        private void ClearFilters()
        {
            Filter.SelectedType = "All";
            Filter.IdValue = string.Empty;
            Filter.IsLessThan = false;
            Filter.IsEqual = false;
            Filter.IsGreaterThan = false;
            Filter.CombineTypeAndId = false;
            SelectedSavedFilter = null;
            RefreshFilter();
        }

        private void ApplyPreset(FilterPresetViewModel preset)
        {
            Filter.SelectedType = preset.SelectedType;
            Filter.IdValue = preset.IdValue;
            Filter.IsLessThan = preset.IsLessThan;
            Filter.IsEqual = preset.IsEqual;
            Filter.IsGreaterThan = preset.IsGreaterThan;
            Filter.CombineTypeAndId = preset.CombineTypeAndId;
            RefreshFilter();
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

    public class FilterPresetViewModel
    {
        public string Name { get; private set; }
        public string SelectedType { get; private set; }
        public string IdValue { get; private set; }
        public bool IsLessThan { get; private set; }
        public bool IsEqual { get; private set; }
        public bool IsGreaterThan { get; private set; }
        public bool CombineTypeAndId { get; private set; }

        public FilterPresetViewModel(FilterViewModel filter)
        {
            SelectedType = string.IsNullOrWhiteSpace(filter.SelectedType) ? "All" : filter.SelectedType;
            IdValue = filter.IdValue ?? string.Empty;
            IsLessThan = filter.IsLessThan;
            IsEqual = filter.IsEqual;
            IsGreaterThan = filter.IsGreaterThan;
            CombineTypeAndId = filter.CombineTypeAndId;
            Name = BuildName();
        }

        private string BuildName()
        {
            string typePart = "Type: " + SelectedType;
            string idPart = "ID: Any";

            if (!string.IsNullOrWhiteSpace(IdValue))
            {
                if (IsLessThan)
                {
                    idPart = "ID < " + IdValue;
                }
                else if (IsEqual)
                {
                    idPart = "ID = " + IdValue;
                }
                else if (IsGreaterThan)
                {
                    idPart = "ID > " + IdValue;
                }
            }

            string joinPart = CombineTypeAndId ? "AND" : "OR";
            return typePart + " | " + idPart + " | " + joinPart;
        }
    }

    public class FilterViewModel : INotifyPropertyChanged
    {
        private string selectedType;
        private string idValue;
        private bool isLessThan;
        private bool isEqual;
        private bool isGreaterThan;
        private bool combineTypeAndId;

        public string SelectedType
        {
            get { return selectedType; }
            set
            {
                selectedType = value;
                OnPropertyChanged("SelectedType");
            }
        }

        public string IdValue
        {
            get { return idValue; }
            set
            {
                idValue = value;
                OnPropertyChanged("IdValue");
            }
        }

        public bool IsLessThan
        {
            get { return isLessThan; }
            set
            {
                isLessThan = value;
                OnPropertyChanged("IsLessThan");
            }
        }

        public bool IsEqual
        {
            get { return isEqual; }
            set
            {
                isEqual = value;
                OnPropertyChanged("IsEqual");
            }
        }

        public bool IsGreaterThan
        {
            get { return isGreaterThan; }
            set
            {
                isGreaterThan = value;
                OnPropertyChanged("IsGreaterThan");
            }
        }

        public bool CombineTypeAndId
        {
            get { return combineTypeAndId; }
            set
            {
                combineTypeAndId = value;
                OnPropertyChanged("CombineTypeAndId");
            }
        }

        public FilterViewModel()
        {
            SelectedType = "All";
            IdValue = string.Empty;
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

    public class NetworkDisplayViewModel : INotifyPropertyChanged
    {
        private readonly MainWindowViewModel parent;
        private readonly ObservableCollection<MeracPotrosnje> allEntities;
        private NetworkGridSlotViewModel selectedConnectionStartSlot;

        public ObservableCollection<EntityTypeGroupViewModel> AvailableEntityTypes { get; private set; }
        public ObservableCollection<NetworkGridSlotViewModel> DropSlots { get; private set; }
        public ObservableCollection<NetworkConnectionViewModel> Connections { get; private set; }
        public ICommand AutoPlaceEntitiesCommand { get; private set; }
        public ICommand RemoveEntityFromSlotCommand { get; private set; }

        public NetworkDisplayViewModel(MainWindowViewModel parent, ObservableCollection<MeracPotrosnje> allEntities)
        {
            this.parent = parent;
            this.allEntities = allEntities;
            AvailableEntityTypes = new ObservableCollection<EntityTypeGroupViewModel>();
            DropSlots = new ObservableCollection<NetworkGridSlotViewModel>();
            Connections = new ObservableCollection<NetworkConnectionViewModel>();

            AvailableEntityTypes.Add(new EntityTypeGroupViewModel("Interval Meter"));
            AvailableEntityTypes.Add(new EntityTypeGroupViewModel("Smart Meter"));

            for (int i = 1; i <= 12; i++)
            {
                DropSlots.Add(new NetworkGridSlotViewModel(i));
            }

            AutoPlaceEntitiesCommand = new RelayCommand(_ => AutoPlaceEntitiesWithPrompt());
            RemoveEntityFromSlotCommand = new RelayCommand(parameter => RemoveEntityFromSlot(parameter as NetworkGridSlotViewModel));
        }

        public bool CanPlaceEntityInSlot(MeracPotrosnje entity, NetworkGridSlotViewModel targetSlot)
        {
            if (entity == null || targetSlot == null)
            {
                return false;
            }

            return targetSlot.Entity == null || targetSlot.Entity == entity;
        }

        public bool PlaceEntityInSlot(MeracPotrosnje entity, NetworkGridSlotViewModel targetSlot)
        {
            if (!CanPlaceEntityInSlot(entity, targetSlot))
            {
                return false;
            }

            NetworkGridSlotViewModel currentSlot = DropSlots.FirstOrDefault(slot => slot.Entity == entity);

            if (currentSlot == targetSlot)
            {
                return true;
            }

            if (currentSlot != null)
            {
                currentSlot.Entity = null;
            }

            targetSlot.Entity = entity;
            RefreshAvailableEntities(allEntities);
            return true;
        }

        public void HandleSlotClickForConnection(NetworkGridSlotViewModel clickedSlot)
        {
            if (clickedSlot == null || clickedSlot.Entity == null)
            {
                ClearSelectedConnectionStartSlot();
                return;
            }

            if (selectedConnectionStartSlot == null)
            {
                selectedConnectionStartSlot = clickedSlot;
                selectedConnectionStartSlot.IsSelectedForConnection = true;
                return;
            }

            if (selectedConnectionStartSlot == clickedSlot)
            {
                ClearSelectedConnectionStartSlot();
                return;
            }

            AddConnection(selectedConnectionStartSlot.Entity, clickedSlot.Entity);
            ClearSelectedConnectionStartSlot();
        }

        private void AddConnection(MeracPotrosnje firstEntity, MeracPotrosnje secondEntity)
        {
            if (firstEntity == null || secondEntity == null || firstEntity == secondEntity)
            {
                return;
            }

            bool alreadyExists = Connections.Any(connection =>
                (connection.FirstEntity == firstEntity && connection.SecondEntity == secondEntity) ||
                (connection.FirstEntity == secondEntity && connection.SecondEntity == firstEntity));

            if (alreadyExists)
            {
                return;
            }

            Connections.Add(new NetworkConnectionViewModel(firstEntity, secondEntity));
        }

        private void ClearSelectedConnectionStartSlot()
        {
            if (selectedConnectionStartSlot != null)
            {
                selectedConnectionStartSlot.IsSelectedForConnection = false;
                selectedConnectionStartSlot = null;
            }
        }

        public void RemoveEntityFromSlot(NetworkGridSlotViewModel slot)
        {
            if (slot == null || slot.Entity == null)
            {
                return;
            }

            MeracPotrosnje entity = slot.Entity;
            slot.Entity = null;
            RemoveConnectionsForEntity(entity);
            ClearSelectedConnectionStartSlot();
            RefreshAvailableEntities(allEntities);
        }

        public void RemoveEntityFromGrid(MeracPotrosnje entity)
        {
            if (entity == null)
            {
                return;
            }

            foreach (NetworkGridSlotViewModel slot in DropSlots)
            {
                if (slot.Entity == entity)
                {
                    slot.Entity = null;
                }
            }

            RemoveConnectionsForEntity(entity);
            ClearSelectedConnectionStartSlot();
            RefreshAvailableEntities(allEntities);
        }

        private void RemoveConnectionsForEntity(MeracPotrosnje entity)
        {
            List<NetworkConnectionViewModel> connectionsToRemove = Connections
                .Where(connection => connection.FirstEntity == entity || connection.SecondEntity == entity)
                .ToList();

            foreach (NetworkConnectionViewModel connection in connectionsToRemove)
            {
                Connections.Remove(connection);
            }
        }

        public NetworkGridSlotViewModel GetSlotByEntity(MeracPotrosnje entity)
        {
            return DropSlots.FirstOrDefault(slot => slot.Entity == entity);
        }

        public void RefreshAvailableEntities(IEnumerable<MeracPotrosnje> entities)
        {
            foreach (EntityTypeGroupViewModel group in AvailableEntityTypes)
            {
                group.Entities.Clear();
            }

            foreach (MeracPotrosnje entity in entities)
            {
                if (IsEntityPlacedOnGrid(entity))
                {
                    continue;
                }

                EntityTypeGroupViewModel group = AvailableEntityTypes.FirstOrDefault(item => item.Name == entity.TypeName);

                if (group != null)
                {
                    group.Entities.Add(entity);
                }
            }
        }

        private bool IsEntityPlacedOnGrid(MeracPotrosnje entity)
        {
            return DropSlots.Any(slot => slot.Entity == entity);
        }

        private void AutoPlaceEntitiesWithPrompt()
        {
            if (!parent.ShowPrompt("Auto place entities", "Do you want to place all currently unplaced entities into free network grid slots?"))
            {
                return;
            }

            AutoPlaceEntities();
        }

        private void AutoPlaceEntities()
        {
            List<MeracPotrosnje> availableEntities = allEntities
                .Where(entity => !IsEntityPlacedOnGrid(entity))
                .ToList();

            foreach (MeracPotrosnje entity in availableEntities)
            {
                NetworkGridSlotViewModel freeSlot = DropSlots.FirstOrDefault(slot => slot.Entity == null);

                if (freeSlot == null)
                {
                    break;
                }

                freeSlot.Entity = entity;
            }

            RefreshAvailableEntities(allEntities);
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

    public class NetworkGridSlotViewModel : INotifyPropertyChanged
    {
        private MeracPotrosnje entity;
        private bool isSelectedForConnection;

        public int SlotNumber { get; private set; }

        public string SlotNumberText
        {
            get { return "Slot " + SlotNumber; }
        }

        public MeracPotrosnje Entity
        {
            get { return entity; }
            set
            {
                entity = value;
                OnPropertyChanged("Entity");
                OnPropertyChanged("HasEntity");
            }
        }

        public bool HasEntity
        {
            get { return Entity != null; }
        }

        public bool IsSelectedForConnection
        {
            get { return isSelectedForConnection; }
            set
            {
                isSelectedForConnection = value;
                OnPropertyChanged("IsSelectedForConnection");
            }
        }

        public NetworkGridSlotViewModel(int slotNumber)
        {
            SlotNumber = slotNumber;
            IsSelectedForConnection = false;
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

    public class NetworkConnectionViewModel
    {
        public MeracPotrosnje FirstEntity { get; private set; }
        public MeracPotrosnje SecondEntity { get; private set; }

        public NetworkConnectionViewModel(MeracPotrosnje firstEntity, MeracPotrosnje secondEntity)
        {
            FirstEntity = firstEntity;
            SecondEntity = secondEntity;
        }
    }

    public class EntityTypeGroupViewModel
    {
        public string Name { get; private set; }
        public bool IsExpanded { get; set; }
        public ObservableCollection<MeracPotrosnje> Entities { get; private set; }

        public EntityTypeGroupViewModel(string name)
        {
            Name = name;
            IsExpanded = true;
            Entities = new ObservableCollection<MeracPotrosnje>();
        }
    }

    public class MeasurementGraphViewModel : INotifyPropertyChanged
    {
        private readonly Dictionary<MeracPotrosnje, List<MeasurementHistoryItem>> measurementHistory;
        private MeracPotrosnje selectedEntity;

        public ObservableCollection<MeracPotrosnje> Entities { get; private set; }
        public ObservableCollection<GraphPointViewModel> LastMeasurements { get; private set; }

        public MeracPotrosnje SelectedEntity
        {
            get { return selectedEntity; }
            set
            {
                selectedEntity = value;
                OnPropertyChanged("SelectedEntity");
                RefreshGraphPoints();
            }
        }

        public MeasurementGraphViewModel(ObservableCollection<MeracPotrosnje> entities)
        {
            Entities = entities;
            LastMeasurements = new ObservableCollection<GraphPointViewModel>();
            measurementHistory = new Dictionary<MeracPotrosnje, List<MeasurementHistoryItem>>();
        }

        public void AddMeasurement(MeracPotrosnje entity, double measure, DateTime time)
        {
            if (entity == null)
            {
                return;
            }

            if (!measurementHistory.ContainsKey(entity))
            {
                measurementHistory[entity] = new List<MeasurementHistoryItem>();
            }

            measurementHistory[entity].Add(new MeasurementHistoryItem(measure, time));

            if (measurementHistory[entity].Count > 50)
            {
                measurementHistory[entity].RemoveAt(0);
            }

            if (SelectedEntity == null)
            {
                SelectedEntity = entity;
            }

            if (SelectedEntity == entity)
            {
                RefreshGraphPoints();
            }
        }

        public void RemoveEntityHistory(MeracPotrosnje entity)
        {
            if (entity == null)
            {
                return;
            }

            if (measurementHistory.ContainsKey(entity))
            {
                measurementHistory.Remove(entity);
            }

            RefreshGraphPoints();
        }

        private void RefreshGraphPoints()
        {
            LastMeasurements.Clear();

            if (SelectedEntity == null || !measurementHistory.ContainsKey(SelectedEntity))
            {
                return;
            }

            List<MeasurementHistoryItem> lastItems = measurementHistory[SelectedEntity]
                .Skip(Math.Max(0, measurementHistory[SelectedEntity].Count - 5))
                .ToList();

            double[] xPositions = new double[] { 52, 158, 264, 370, 476 };

            for (int i = 0; i < lastItems.Count; i++)
            {
                LastMeasurements.Add(new GraphPointViewModel(lastItems[i], xPositions[i]));
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

    public class MeasurementHistoryItem
    {
        public double Value { get; private set; }
        public DateTime Time { get; private set; }

        public MeasurementHistoryItem(double value, DateTime time)
        {
            Value = value;
            Time = time;
        }
    }

    public class GraphPointViewModel
    {
        private const double CenterLineY = 150;

        public double X { get; private set; }
        public double CenterY { get; private set; }
        public double CircleSize { get; private set; }
        public double CircleLeft { get; private set; }
        public double CircleTop { get; private set; }
        public double LabelLeft { get; private set; }
        public double LabelTop { get; private set; }
        public double TimeLeft { get; private set; }
        public double DateLeft { get; private set; }
        public string ValueText { get; private set; }
        public string TimeText { get; private set; }
        public string DateText { get; private set; }
        public Brush CircleFill { get; private set; }

        public GraphPointViewModel(MeasurementHistoryItem item, double x)
        {
            X = x;
            CenterY = CenterLineY;

            double radius = Clamp(7 + item.Value * 8.5, 7, 42);
            CircleSize = radius * 2;
            CircleLeft = X - radius;
            CircleTop = CenterY - radius;
            LabelLeft = X - 30;
            LabelTop = CircleTop - 24;
            TimeLeft = X - 31;
            DateLeft = X - 35;
            ValueText = item.Value.ToString("F2", CultureInfo.InvariantCulture) + "kWh";
            TimeText = item.Time.ToString("HH:mm:ss");
            DateText = item.Time.ToString("dd.MM.yyyy");

            bool isValid = item.Value >= 0.34 && item.Value <= 2.73;
            CircleFill = isValid
                ? new SolidColorBrush(Color.FromRgb(229, 229, 229))
                : new SolidColorBrush(Color.FromRgb(145, 145, 145));
        }

        private static double Clamp(double value, double minimum, double maximum)
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
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> execute;

        public RelayCommand(Action<object> execute)
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
