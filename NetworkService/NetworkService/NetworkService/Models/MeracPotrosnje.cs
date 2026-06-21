using NetworkService.Enums;
using System.ComponentModel;

namespace NetworkService.Models
{
    public class MeracPotrosnje : INotifyPropertyChanged
    {
        private int id;
        private string name;
        private EntityTypeInfo type;
        private double lastMeasure;
        private bool hasMeasurement;

        public int Id
        {
            get { return id; }
            set
            {
                id = value;
                OnPropertyChanged("Id");
            }
        }

        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                OnPropertyChanged("Name");
            }
        }

        public EntityTypeInfo Type
        {
            get { return type; }
            set
            {
                type = value;
                OnPropertyChanged("Type");
                OnPropertyChanged("TypeName");
                OnPropertyChanged("ImagePath");
                OnPropertyChanged("EntityType");
            }
        }

        public EntityType EntityType
        {
            get
            {
                if (type == null)
                {
                    return EntityType.INTERVAL_METER;
                }

                return type.EntityType;
            }
        }

        public string TypeName
        {
            get
            {
                if (type == null)
                {
                    return string.Empty;
                }

                return type.Name;
            }
        }

        public string ImagePath
        {
            get
            {
                if (type == null)
                {
                    return string.Empty;
                }

                return type.ImagePath;
            }
        }

        public double LastMeasure
        {
            get { return lastMeasure; }
            set
            {
                lastMeasure = value;
                OnPropertyChanged("LastMeasure");
                OnPropertyChanged("LastMeasureText");
                OnPropertyChanged("IsLastMeasureValid");
                OnPropertyChanged("IsLastMeasureInvalid");
                OnPropertyChanged("MeasureStatusText");
                OnPropertyChanged("StatusIconText");
            }
        }

        public bool HasMeasurement
        {
            get { return hasMeasurement; }
            set
            {
                hasMeasurement = value;
                OnPropertyChanged("HasMeasurement");
                OnPropertyChanged("LastMeasureText");
                OnPropertyChanged("IsLastMeasureValid");
                OnPropertyChanged("IsLastMeasureInvalid");
                OnPropertyChanged("MeasureStatusText");
                OnPropertyChanged("StatusIconText");
            }
        }

        public string LastMeasureText
        {
            get
            {
                if (!HasMeasurement)
                {
                    return "-- kWh";
                }

                return LastMeasure.ToString("F2") + " kWh";
            }
        }

        public bool IsLastMeasureValid
        {
            get { return HasMeasurement && LastMeasure >= 0.34 && LastMeasure <= 2.73; }
        }

        public bool IsLastMeasureInvalid
        {
            get { return HasMeasurement && !IsLastMeasureValid; }
        }

        public string MeasureStatusText
        {
            get
            {
                if (!HasMeasurement)
                {
                    return "NO DATA";
                }

                return IsLastMeasureValid ? "VALID" : "INVALID";
            }
        }

        public string StatusIconText
        {
            get
            {
                if (!HasMeasurement)
                {
                    return "?";
                }

                return IsLastMeasureValid ? "✓" : "!";
            }
        }

        public MeracPotrosnje()
        {
            id = 0;
            name = string.Empty;
            type = new EntityTypeInfo("Interval Meter", "Images/interval-meter.png", EntityType.INTERVAL_METER);
            lastMeasure = 0;
            hasMeasurement = false;
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

    public class EntityTypeInfo
    {
        public string Name { get; private set; }
        public string ImagePath { get; private set; }
        public EntityType EntityType { get; private set; }

        public EntityTypeInfo(string name, string imagePath, EntityType entityType)
        {
            Name = name;
            ImagePath = imagePath;
            EntityType = entityType;
        }
    }
}
