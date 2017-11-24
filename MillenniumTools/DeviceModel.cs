using MillenniumTools.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace MillenniumTools
{
    public class DeviceModel: ViewModelBase
    {
        private static object _syncLock = new object();

        private bool? _isNetworkAccessible;
        private bool? _isHttpAccessible;
        private bool? _isAuthenticated;
        private bool? _isActive;
        private string _rxSpeed;
        private string _txSpeed;
        private string _upTime;
        private bool _isLoading;
        private double _networkAvailability;
        private long _networkAvgRoundtrip;
        private bool _isSelected;
        private bool _couldNotDetermineIp;
        private bool _soundingAlarm;

        public DeviceModel()
        {
            RebootCommand = new RelayCommand(() =>
            {
                var bw = new BackgroundWorker();
                bw.DoWork+=(s,e)=>DoReboot();
                bw.RunWorkerAsync();
            }, () => IsHttpAccessible==true && IsAuthenticated==true);

            NetworkAvailability = 100;
            Pings = new BindingList<PingInfo>();
            Users = new BindingList<UserInfo>();
            Throughputs = new BindingList<ThroughputResult>();
            BindingOperations.EnableCollectionSynchronization(Pings, _syncLock);
            BindingOperations.EnableCollectionSynchronization(Users, _syncLock);
            BindingOperations.EnableCollectionSynchronization(Throughputs, _syncLock);
        }

        public string Ip { get; set; }

        public Func<string> DoReboot
        {
            get;
            set;
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged("IsLoading");
                    if (value)
                    {
                        Clear();
                    }
                }
            }
        }

        public bool? IsNetworkAccessible
        {
            get { return _isNetworkAccessible; }
            set
            {
                if (_isNetworkAccessible != value)
                {
                    _isNetworkAccessible = value;
                    OnPropertyChanged("IsNetworkAccessible");
                }
            }
        }

        public bool? IsHttpAccessible
        {
            get { return _isHttpAccessible; }
            set
            {
                if (_isHttpAccessible != value)
                {
                    _isHttpAccessible = value;
                    OnPropertyChanged("IsHttpAccessible");
                }
            }
        }


        public bool? IsAuthenticated
        {
            get { return _isAuthenticated; }
            set
            {
                if (_isAuthenticated != value)
                {
                    _isAuthenticated = value;
                    OnPropertyChanged("IsAuthenticated");
                }
            }
        }


        public bool? IsActive
        {
            get { return _isActive; }
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged("IsActive");
                }
            }
        }

        public bool CouldNotDetermineIp
        {
            get { return _couldNotDetermineIp; }
            set
            {
                if (_couldNotDetermineIp != value)
                {
                    _couldNotDetermineIp = value;
                    OnPropertyChanged("CouldNotDetermineIp");
                }
            }
        }

        public bool SoundingAlarm
        {
            get { return _soundingAlarm; }
            set
            {
                if (_soundingAlarm != value)
                {
                    _soundingAlarm = value;
                    OnPropertyChanged("SoundingAlarm");
                }
            }
        }

        public string RxSpeed
        {
            get { return _rxSpeed; }
            set
            {
                if (_rxSpeed != value)
                {
                    _rxSpeed = value;
                    OnPropertyChanged("RxSpeed");
                }
            }
        }


        public string TxSpeed
        {
            get { return _txSpeed; }
            set
            {
                if (_txSpeed != value)
                {
                    _txSpeed = value;
                    OnPropertyChanged("TxSpeed");
                }
            }
        }


        public string UpTime
        {
            get { return _upTime; }
            set
            {
                if (_upTime != value)
                {
                    _upTime = value;
                    UpTimeSpan = Extender.ParseUpTime(value);
                    OnPropertyChanged("UpTime");
                }
            }
        }

        public TimeSpan UpTimeSpan { get; set; }

        public string Type
        {
            get
            {
                if (string.Equals(Config.Instance.RouterIP, Ip, StringComparison.InvariantCultureIgnoreCase)) return "Router";
                if (Config.Instance.ReferenceIPs.Any(ip => string.Equals(ip, Ip, StringComparison.InvariantCultureIgnoreCase))) return "Reference";
                if (Config.Instance.ExtenderIPs.Any(ip => string.Equals(ip, Ip, StringComparison.InvariantCultureIgnoreCase))) return "Extender";
                return "Unknown";
            }
        }

        public double NetworkAvailability
        {
            get { return _networkAvailability; }
            set
            {
                if (_networkAvailability != value)
                {
                    _networkAvailability = value;
                    OnPropertyChanged("NetworkAvailability");
                }
            }
        }

        public long NetworkAvgRoundtrip
        {
            get { return _networkAvgRoundtrip; }
            set
            {
                if (_networkAvgRoundtrip != value)
                {
                    _networkAvgRoundtrip = value;
                    OnPropertyChanged("NetworkAvgRoundtrip");
                }
            }
        }
        
        public BindingList<PingInfo> Pings { get; set; }
        public BindingList<UserInfo> Users { get; set; }
        public BindingList<ThroughputResult> Throughputs { get; set; }

        internal void Clear()
        {
            this.IsActive = null;
            this.IsAuthenticated = null;
            this.IsHttpAccessible = null;
            this.IsNetworkAccessible = null;
            this.RxSpeed = "";
            this.TxSpeed = "";
            this.UpTime = "";
        }

        public ICommand RebootCommand
        {
            set;
            get;
        }

        public void AddPing(PingReplyWrapper reply, PingInfo info = null)
        {
            var result = reply!=null && reply.Status == System.Net.NetworkInformation.IPStatus.Success
                ? reply.RoundtripTime
                : (long?)null;
            IsNetworkAccessible = result != null;
            if (info == null)
            {
                info = new PingInfo { Time = DateTime.Now };
            }
            lock (Pings.GetLock())
            {
                Pings.Add(info);
            }
            info.Roundtrip = result;
            List<PingInfo> lastPings;
            lock (Pings.GetLock())
            {
                lastPings = Pings.Where(p => p.Time > DateTime.Now - Config.Instance.PingGraphTimeSize).ToList();
            }
            var lastPingsSuccess = lastPings.Where(p => p.Roundtrip != null).ToList();
            NetworkAvailability = 100.0 * lastPingsSuccess.Count / lastPings.Count;
            NetworkAvgRoundtrip = lastPingsSuccess.Count == 0
                ? 0
                : (long)lastPingsSuccess.Average(p => p.Roundtrip.Value);
        }

        public class PingInfo
        {
            public DateTime Time { get; set; }

            public long? Roundtrip { get; set; }
        }

        public class UserInfo : ViewModelBase
        {
            private string _name;
            private string _ip;
            private string _macAddress;
            private string _packetsRx;
            private string _packetsTx;
            private string _type;
            public UserInfo(User u)
            {
                Name = u.Name;
                Ip = u.Ip;
                MacAddress = normalizeMacAddress(u.MacAddress);
                PacketsRx = u.PacketsRx;
                PacketsTx = u.PacketsTx;
                Type = u.Type;
            }

            private string normalizeMacAddress(string mac)
            {
                return (mac ?? "").Replace(":", "-").ToUpper();
            }

            public string Name
            {
                get { return _name; }
                set { if (value != _name) { _name = value; OnPropertyChanged("Name"); } }
            }

            public string Ip
            {
                get { return _ip; }
                set { if (value != _ip) { _ip = value; OnPropertyChanged("Ip"); } }
            }

            public string MacAddress
            {
                get { return _macAddress; }
                set { if (value != _macAddress) { _macAddress = value; OnPropertyChanged("MacAddress"); } }
            }

            public string PacketsRx
            {
                get { return _packetsRx; }
                set { if (value != _packetsRx) { _packetsRx = value; OnPropertyChanged("PacketsRx"); } }
            }

            public string PacketsTx
            {
                get { return _packetsTx; }
                set { if (value != _packetsTx) { _packetsTx = value; OnPropertyChanged("PacketsTx"); } }
            }

            public string Type
            {
                get { return _type; }
                set { if (value != _type) { _type = value; OnPropertyChanged("Type"); } }
            }
        }

    }
}
