using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MillenniumTools.Common
{
    public class Config : ViewModelBase
    {
        private Config() { Init(); }

        public static Config Instance
        {
            get
            {
                return _instance ?? (_instance = new Config());
            }
        }

        private static FileSystemWatcher _watcher;
        private static Regex _regIni = new Regex(@"^\s*(?<name>[^=\s]+)\s*=\s*(?<value>[^#]*)\s*(#.*)?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        private static PropertyInfo[] _props;
        private static Config _instance;

        private string _configFilePath;
        private string _error;

        public event EventHandler Change;

        private string _routerIP;
        private List<string> _extenderIPs;
        private TimeSpan _clientTimeout;
        private string _userAgent;
        private string _routerUsername;
        private string _routerPassword;
        private string _extenderUsername;
        private string _extenderPassword;
        private TimeSpan _reloadInterval;
        private TimeSpan _pingInterval;
        private TimeSpan _pingTimeout;
        private TimeSpan _pingGraphTimeSize;
        private double _smoothness;
        private int _maxPingsInMemory;
        private List<string> _referenceIPs;
        private TimeSpan _pingEntireSubnetInterval;
        private bool _getAdditionalNetworkInfo;
        private bool _useStandardPing;
        private bool _showPingWarningsOnChart;
        private int _restartRouterWifiThreshold;
        private int _restartRouterCount;
        private int _deviceAlarmThreshold;
        private int _alarmSoundVolume;
        private bool _isMuted;
        private TimeSpan _maximumDeviceAge;
        private TimeSpan _speedGraphTimeSize;

        public string Email
        {
            get { return "siderite@madnet.ro"; }
        }

        public string ConfigFilePath
        {
            get { return _configFilePath; }
        }

        public double MaximumDeviceAgeInHours
        {
            get { return MaximumDeviceAge.TotalHours; }
        }

        public double ReloadIntervalInMinutes
        {
            get { return ReloadInterval.TotalMinutes; }
        }

        private void reset()
        {
            _routerIP = "192.168.1.1";
            _clientTimeout = TimeSpan.FromSeconds(30);
            _userAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/43.0.2357.130 Safari/537.36";
            _extenderIPs = new List<string> { "192.168.1.91", "192.168.1.92", "192.168.1.93", "192.168.1.94" };
            _routerUsername = "admin";
            _routerPassword = "marco";
            _extenderUsername = "marco";
            _extenderPassword = "marco";
            _reloadInterval = TimeSpan.FromSeconds(30);
            _pingInterval = TimeSpan.FromSeconds(1);
            _pingTimeout = TimeSpan.FromSeconds(3);
            _speedGraphTimeSize = TimeSpan.FromMinutes(10);
            _pingGraphTimeSize = TimeSpan.FromSeconds(100);
            _smoothness = 0.33;
            _maxPingsInMemory = 10000;
            _referenceIPs = new List<string> { "192.168.200.1", "google.com" };
            _pingEntireSubnetInterval = TimeSpan.FromMinutes(10);
            _getAdditionalNetworkInfo = true;
            _useStandardPing = true;
            _showPingWarningsOnChart = false;
            _restartRouterWifiThreshold = 75;
            _restartRouterCount = 8;
            _deviceAlarmThreshold = 80;
            _alarmSoundVolume = 5;
            _maximumDeviceAge = TimeSpan.FromDays(1);
            _isMuted = true;
        }

        public void Init()
        {
            var type = typeof(Config);
            _props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && p.CanRead).ToArray();

            _configFilePath = Assembly.GetEntryAssembly().Location;
            _configFilePath = Path.Combine(Path.GetDirectoryName(_configFilePath), Path.GetFileNameWithoutExtension(_configFilePath) + ".config");
            loadConfig();
            _watcher = new FileSystemWatcher(AppDomain.CurrentDomain.BaseDirectory)
            {
                IncludeSubdirectories = false
            };
            _watcher.Changed += _watcher_Changed;
            _watcher.Created += _watcher_Changed;
            _watcher.Deleted += _watcher_Changed;
            _watcher.Renamed += _watcher_Changed;
            _watcher.EnableRaisingEvents = true;
        }

        void _watcher_Changed(object sender, FileSystemEventArgs e)
        {
            var filenames = new List<string> { e.FullPath };
            var ren = e as RenamedEventArgs;
            if (ren != null)
            {
                filenames.Add(ren.OldFullPath);
            }
            if (!filenames.Any(f => string.Equals(f, _configFilePath, StringComparison.CurrentCultureIgnoreCase))) return;

            loadConfig();
            if (Change != null)
            {
                Change(sender, e);
            }
        }

        private void loadConfig()
        {
            try
            {
                reset();
                if (!File.Exists(_configFilePath))
                {
                    saveConfig();
                    return;
                }
                using (var sr = new StreamReader(_configFilePath))
                {
                    string l;
                    while ((l = sr.ReadLine()) != null)
                    {
                        var match = _regIni.Match(l);
                        if (!match.Success) continue;
                        var name = match.Groups["name"].Value;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        var value = match.Groups["value"].Value;
                        setValue(name, value);
                    }
                }
                _error = null;
            }
            catch (Exception ex)
            {
                _error = "Configuration error: " + ex.Message;
            }
        }

        private void setValue(string name, string value)
        {
            try
            {
                var prop = _props.FirstOrDefault(p => string.Equals(name, p.Name, StringComparison.CurrentCultureIgnoreCase));
                if (prop == null) return;
                var val = deserialize(value, prop.PropertyType);
                prop.SetValue(this, val);
            }
            catch (Exception ex)
            {
                throw new Exception("Can't set " + name + " to " + value + " (" + ex.Message + ")");
            }
        }

        private static object deserialize(string value, Type type)
        {
            if (string.IsNullOrWhiteSpace(value)) return type.GetDefaultValue();
            try
            {
                return Convert.ChangeType(value, type,CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    Type itemType = type.GetGenericArguments()[0];
                    var splits = Regex.Split(value, ", ");
                    if (itemType == typeof(string))
                    {
                        return Convert.ChangeType(splits.Select(s => (string)deserialize(s, itemType)).ToList(), type,CultureInfo.InvariantCulture);
                    }
                    else if (itemType == typeof(object))
                    {
                        return Convert.ChangeType(splits.Select(s => deserialize(s, itemType)).ToList(), type, CultureInfo.InvariantCulture);
                    }
                }
                if (type == typeof(TimeSpan))
                {
                    return TimeSpan.Parse(value, CultureInfo.InvariantCulture);
                }
                throw new NotImplementedException("Type " + type + " not implemented", ex);
            }
        }

        private static string serialize(object value)
        {
            if (value == null) return string.Empty;
            var s = value as string;
            if (s != null) return s;

            var ienum = value as IEnumerable;
            if (ienum != null)
            {
                return string.Join(", ", ienum.Select(o => serialize(o)).ToArray());
            }
            return string.Format(CultureInfo.InvariantCulture,"{0}", value);
        }

        private void saveConfig()
        {
            try
            {
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                }
                using (var st = new FileStream(_configFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    using (var sw = new StreamWriter(st))
                    {
                        sw.WriteLine("#" + Resource.ResourceManager.GetString("ConfigurationFileHeader"));
                        sw.WriteLine("");
                        foreach (var prop in _props)
                        {
                            var description = Resource.ResourceManager.GetString(prop.Name + "ConfigProperty");
                            if (string.IsNullOrWhiteSpace(description))
                            {
                                var desc = prop.GetCustomAttribute(typeof(DescriptionAttribute)) as DescriptionAttribute;
                                if (desc != null)
                                {
                                    description = desc.Description;
                                }
                            }
                            if (!string.IsNullOrWhiteSpace(description))
                            {
                                sw.WriteLine("# " + description);

                            } var val = prop.GetValue(this);
                            sw.WriteLine(prop.Name + " = " + serialize(val));
                            sw.WriteLine();
                        }
                    }
                }
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = true;
                }
                _error = null;
            }
            catch (Exception ex)
            {
                _error = "Configuration error: " + ex.Message;
            }
        }

        public void Save()
        {
            saveConfig();
        }

        [Description("The IP Address of the router")]
        public string RouterIP
        {
            get { return _routerIP; }
            set
            {
                if (value != _routerIP)
                {
                    _routerIP = value;
                    OnPropertyChanged("RouterIP");
                }
            }
        }

        [Description("The IP Addresses of the extenders, comma separated. Make sure the extenders have a static IP address configured.")]
        public List<string> ExtenderIPs
        {
            get { return _extenderIPs; }
            set
            {
                if (value != _extenderIPs)
                {
                    _extenderIPs = value;
                    OnPropertyChanged("ExtenderIPs");
                }
            }
        }

        [Description("Timeout for connecting to web interfaces.")]
        public TimeSpan ClientTimeout
        {
            get { return _clientTimeout; }
            set
            {
                if (value != _clientTimeout)
                {
                    _clientTimeout = value;
                    OnPropertyChanged("ClientTimeout");
                }
            }
        }

        [Description("The browser declared when connecting to web interfaces")]
        public string UserAgent
        {
            get { return _userAgent; }
            set
            {
                if (value != _userAgent)
                {
                    _userAgent = value;
                    OnPropertyChanged("UserAgent");
                }
            }
        }

        [Description("The router interface username")]
        public string RouterUsername
        {
            get { return _routerUsername; }
            set
            {
                if (value != _routerUsername)
                {
                    _routerUsername = value;
                    OnPropertyChanged("RouterUsername");
                }
            }
        }

        [Description("The router interface password")]
        public string RouterPassword
        {
            get { return _routerPassword; }
            set
            {
                if (value != _routerPassword)
                {
                    _routerPassword = value;
                    OnPropertyChanged("RouterPassword");
                }
            }
        }

        [Description("The extender interface username. Make sure the same username is configured on all extenders.")]
        public string ExtenderUsername
        {
            get { return _extenderUsername; }
            set
            {
                if (value != _extenderUsername)
                {
                    _extenderUsername = value;
                    OnPropertyChanged("ExtenderUsername");
                }
            }
        }

        [Description("The extender interface password. Make sure the same password is configured on all extenders.")]
        public string ExtenderPassword
        {
            get { return _extenderPassword; }
            set
            {
                if (value != _extenderPassword)
                {
                    _extenderPassword = value;
                    OnPropertyChanged("ExtenderPassword");
                }
            }
        }

        [Description("Time in which to automatically refresh the status of the devices on the network (format days.hours:minutes:seconds)")]
        public TimeSpan ReloadInterval
        {
            get { return _reloadInterval; }
            set
            {
                if (value != _reloadInterval)
                {
                    _reloadInterval = value;
                    OnPropertyChanged("ReloadInterval");
                    OnPropertyChanged("ReloadIntervalInMinutes");
                }
            }
        }

        [Description("Time in which the devices are being pinged to check availability and refresh the chart (format days.hours:minutes:seconds)")]
        public TimeSpan PingInterval
        {
            get { return _pingInterval; }
            set
            {
                if (value != _pingInterval)
                {
                    _pingInterval = value;
                    OnPropertyChanged("PingInterval");
                }
            }
        }

        [Description("Timeout of ping requests (format days.hours:minutes:seconds)")]
        public TimeSpan PingTimeout
        {
            get { return _pingTimeout; }
            set
            {
                if (value != _pingTimeout)
                {
                    _pingTimeout = value;
                    OnPropertyChanged("PingTimeout");
                }
            }
        }

        [Description("The time interval covered by the ping chart (format days.hours:minutes:seconds)")]
        public TimeSpan PingGraphTimeSize
        {
            get { return _pingGraphTimeSize; }
            set
            {
                if (value != _pingGraphTimeSize)
                {
                    _pingGraphTimeSize = value;
                    OnPropertyChanged("PingGraphTimeSize");
                }
            }
        }

        [Description("The time interval covered by the speed chart (format days.hours:minutes:seconds)")]
        public TimeSpan SpeedGraphTimeSize
        {
            get { return _speedGraphTimeSize; }
            set
            {
                if (value != _speedGraphTimeSize)
                {
                    _speedGraphTimeSize = value;
                    OnPropertyChanged("SpeedGraphTimeSize");
                }
            }
        }

        [Description("The smoothness of the chart. Value from 0.0 to 1.0")]
        public double Smoothness
        {
            get { return _smoothness; }
            set
            {
                if (value != _smoothness)
                {
                    _smoothness = value;
                    OnPropertyChanged("Smoothness");
                }
            }
        }

        [Description("Maximum ping information to keep in memory. The ping information is used to draw the chart.")]
        public int MaxPingsInMemory
        {
            get { return _maxPingsInMemory; }
            set
            {
                if (value != _maxPingsInMemory)
                {
                    _maxPingsInMemory = value;
                    OnPropertyChanged("MaxPingsInMemory");
                }
            }
        }

        [Description("A list of hosts or IP addresses to ping for reference (for example google.com and the first hop towards the Internet). This helps to understand the status of the external network.")]
        public List<string> ReferenceIPs
        {
            get { return _referenceIPs; }
            set
            {
                if (value != _referenceIPs)
                {
                    _referenceIPs = value;
                    OnPropertyChanged("ReferenceIPs");
                }
            }
        }

        [Description("If GetAdditionalNetworkInfo is set, this is the time in which every machine on the subnet is being pinged. It helps with the correlation between MAC address and IP address")]
        public TimeSpan PingEntireSubnetInterval
        {
            get { return _pingEntireSubnetInterval; }
            set
            {
                if (value != _pingEntireSubnetInterval)
                {
                    _pingEntireSubnetInterval = value;
                    OnPropertyChanged("PingEntireSubnetInterval");
                }
            }
        }

        [Description("If this is set to true, more expensive network operations could be performed to gather more information.")]
        public bool GetAdditionalNetworkInfo
        {
            get { return _getAdditionalNetworkInfo; }
            set
            {
                if (value != _getAdditionalNetworkInfo)
                {
                    _getAdditionalNetworkInfo = value;
                    OnPropertyChanged("GetAdditionalNetworkInfo");
                }
            }
        }

        [Description("Set this to true to use the standard Ping class.")]
        public bool UseStandardPing
        {
            get { return _useStandardPing; }
            set
            {
                if (value != _useStandardPing)
                {
                    _useStandardPing = value;
                    OnPropertyChanged("UseStandardPing");
                }
            }
        }

        [Description("Set to true in order to see ! warnings on the chart when ping fails")]
        public bool ShowPingWarningsOnChart
        {
            get { return _showPingWarningsOnChart; }
            set
            {
                if (value != _showPingWarningsOnChart)
                {
                    _showPingWarningsOnChart = value;
                    OnPropertyChanged("ShowPingWarningsOnChart");
                }
            }
        }

        public string Error
        {
            get
            {
                return _error;
            }
        }

        [Description("Percentage of availability under which the router Wi-fi will be reset (0 means never)")]
        public int RestartRouterWifiThreshold
        {
            get { return _restartRouterWifiThreshold; }
            set
            {
                if (value != _restartRouterWifiThreshold)
                {
                    _restartRouterWifiThreshold = value;
                    OnPropertyChanged("RestartRouterWifiThreshold");
                }
            }
        }

        [Description("How many times the router must be under the wifi threshold before it restarts (0 means never)")]
        public int RestartRouterCount
        {
            get { return _restartRouterCount; }
            set
            {
                if (value != _restartRouterCount)
                {
                    _restartRouterCount = value;
                    OnPropertyChanged("RestartRouterCount");
                }
            }
        }

        [Description("Percentage of availability under which a device will cause a sound beep to be sounded (0 means never)")]
        public int DeviceAlarmThreshold
        {
            get { return _deviceAlarmThreshold; }
            set
            {
                if (value != _deviceAlarmThreshold)
                {
                    _deviceAlarmThreshold = value;
                    OnPropertyChanged("DeviceAlarmThreshold");
                }
            }
        }

        [Description("Alarm sound volume (0-100)")]
        public int AlarmSoundVolume
        {
            get { return _alarmSoundVolume; }
            set
            {
                if (value != _alarmSoundVolume)
                {
                    _alarmSoundVolume = value;
                    OnPropertyChanged("AlarmSoundVolume");
                }
            }
        }

        [Description("If set, the alarm will not sound")]
        public bool IsMuted
        {
            get { return _isMuted; }
            set
            {
                if (value != _isMuted)
                {
                    _isMuted = value;
                    OnPropertyChanged("IsMuted");
                }
            }
        }

        [Description("The maximum time interval before a device is restarted (format days.hours:minutes:seconds)")]
        public TimeSpan MaximumDeviceAge
        {
            get { return _maximumDeviceAge; }
            set
            {
                if (value != _maximumDeviceAge)
                {
                    _maximumDeviceAge = value;
                    OnPropertyChanged("MaximumDeviceAge");
                    OnPropertyChanged("MaximumDeviceAgeInHours");
                }
            }
        }
    }
}
