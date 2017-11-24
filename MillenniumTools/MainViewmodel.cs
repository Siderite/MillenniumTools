using Microsoft.Win32;
using MillenniumTools.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace MillenniumTools
{
    public class MainViewmodel : ViewModelBase, IDisposable
    {
        private List<WorkerModel> _workers { get; set; }
        private Timer _reloadTimer;
        private Timer _pingTimer;
        private long _maxRoundTrip;
        private double _maxSpeed;
        private static object _syncLock = new object();
        private DeviceModel _selectedDevice;
        private DateTime? _lastInfoTime;
        private string _configError;
        private int _routerShouldBeRestarted;
        private bool _startWithWindows;

        public event EventHandler AlarmSoundRequested;

        public BindingList<DeviceModel> Devices
        {
            get;
            private set;
        }

        public BindingList<GraphItem> GraphItems
        {
            get;
            private set;
        }

        public long MaxRoundtrip
        {
            get
            {
                return _maxRoundTrip;
            }
            set
            {
                if (_maxRoundTrip != value)
                {
                    _maxRoundTrip = value;
                    OnPropertyChanged("MaxRoundtrip");
                }
            }
        }

        public double MaxSpeed
        {
            get
            {
                return _maxSpeed;
            }
            set
            {
                if (_maxSpeed != value)
                {
                    _maxSpeed = value;
                    OnPropertyChanged("MaxSpeed");
                }
            }
        }


        public int RouterShouldBeRestarted
        {
            get
            {
                return _routerShouldBeRestarted;
            }
            set
            {
                if (_routerShouldBeRestarted != value)
                {
                    _routerShouldBeRestarted = value;
                    OnPropertyChanged("RouterShouldBeRestarted");
                }
            }
        }

        public string ConfigError
        {
            get
            {
                return _configError;
            }
            set
            {
                if (_configError != value)
                {
                    _configError = value;
                    OnPropertyChanged("ConfigError");
                }
            }
        }

        public DeviceModel SelectedDevice
        {
            get
            {
                return _selectedDevice;
            }
            set
            {
                if (_selectedDevice != value)
                {
                    if (_selectedDevice!= null)
                    {
                        _selectedDevice.IsSelected = false;
                    }
                    _selectedDevice = value;
                    if (_selectedDevice != null)
                    {
                        _selectedDevice.IsSelected = true;
                    }
                    OnPropertyChanged("SelectedDevice");
                }
            }
        }

        public ICommand ReloadCommand { get; set; }
        public ICommand EscapeCommand { get; set; }

        public ICommand RestartRouterCommand { get; set; }

        public ICommand ResetPingsCommand { get; set; }

        public bool StartWithWindows
        {
            get
            {
                return _startWithWindows;
            }
            set
            {
                if (_startWithWindows != value)
                {
                    _startWithWindows = value;
                    if (value)
                    {
                        var executablePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Application.Current.MainWindow.GetType().Module.Name);
                        Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true).SetValue("MillenniumTools", executablePath);
                    }
                    else
                    {
                        Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true).DeleteValue("MillenniumTools");
                    }
                    OnPropertyChanged("StartWithWindows");
                }
            }
        }

        public bool IsMuted
        {
            get
            {
                return Config.Instance.IsMuted;
            }
            set
            {
                if (Config.Instance.IsMuted != value)
                {
                    Config.Instance.IsMuted = value;
                    OnPropertyChanged("IsMuted");
                }
            }
        }

        public int AlarmVolume
        {
            get
            {
                return Config.Instance.AlarmSoundVolume;
            }
            set
            {
                if (Config.Instance.AlarmSoundVolume != value)
                {
                    Config.Instance.AlarmSoundVolume = value;
                    OnPropertyChanged("AlarmVolume");
                }
            }
        }


        public MainViewmodel()
        {
            ConfigError = Config.Instance.Error;

            ReloadCommand = new RelayCommand(Reload);
            EscapeCommand = new RelayCommand(EscapePressed);
            ResetPingsCommand = new RelayCommand(ResetPings);
            Devices = new BindingList<DeviceModel>();
            GraphItems = new BindingList<GraphItem>();
            BindingOperations.EnableCollectionSynchronization(Devices, _syncLock);
            BindingOperations.EnableCollectionSynchronization(GraphItems, _syncLock);

            StartWithWindows = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true).GetValue("MillenniumTools") != null;

            _workers = new List<WorkerModel>();
            var router = new Router(Config.Instance.RouterIP);
            var routerModel = getDevice(Config.Instance.RouterIP);
            RestartRouterCommand = new RelayCommand(() => router.RestartModem(),()=>routerModel.IsAuthenticated==true);
            _workers.Add(new WorkerModel
            {
                Worker = getRouterWorker(router, Config.Instance.RouterUsername, Config.Instance.RouterPassword),
                Device = router
            });
            foreach (var ip in Config.Instance.ExtenderIPs)
            {
                var extender = new Extender(ip);
                _workers.Add(new WorkerModel
                {
                    Worker = getExtenderWorker(extender, Config.Instance.ExtenderUsername, Config.Instance.ExtenderPassword),
                    Device = extender
                });
            }
            foreach (var ip in Config.Instance.ReferenceIPs)
            {
                var reference = new ReferenceDevice(ip);
                _workers.Add(new WorkerModel
                {
                    Worker = getReferenceWorker(reference),
                    Device = reference
                });
            }
            _reloadTimer = new Timer
            {
                AutoReset = true,
                Enabled = false,
                Interval = Config.Instance.ReloadInterval.TotalMilliseconds
            };
            _reloadTimer.Elapsed += (s, e) => Reload();
            Reload();
            _reloadTimer.Start();
            _pingTimer = new Timer
            {
                AutoReset = true,
                Enabled = false,
                Interval = Config.Instance.PingInterval.TotalMilliseconds
            };
            _pingTimer.Elapsed += (s, e) => DoPing();
            _pingTimer.Start();
        }

        private void ResetPings()
        {
            RouterShouldBeRestarted = 0;
            foreach (var device in Devices)
            {
                device.Pings.Clear();
            }
        }

        private void EscapePressed()
        {
            SelectedDevice = null;
        }

        private void DoPing()
        {
            RefreshButtons();
            RefreshGraph();
            //var offset = Config.PingInterval.TotalMilliseconds / (_workers.Count + 1);
            foreach (var worker in _workers)
            {
                var deviceModel = getDevice(worker.Device.IP);
                worker.Device.Ping(null, reply=>deviceModel.AddPing(reply));
                //System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(offset));
            }
            soundAlarm();
        }

        private void RefreshGraph()
        {
            var items = drawSpeedGraph();
            GraphItems.AddRange(items, true);
            items = drawPingGraph();
            GraphItems.AddRange(items, false);
            this.Dispatcher.Invoke(GraphItems.ResetBindings, DispatcherPriority.Normal);
        }

        private List<GraphItem> drawPingGraph()
        {
            DateTime? minTime = DateTime.Now.Add(-Config.Instance.PingGraphTimeSize);
            DateTime? maxTime = DateTime.Now;
            long? maxRoundtrip = null;

            foreach (var deviceModel in Devices)
            {
                for (var i = deviceModel.Pings.Count - 1; i >= 0; i--)
                {
                    var ping = deviceModel.Pings[i];
                    if (ping.Time < minTime)
                    {
                        break;
                    }
                    if (maxRoundtrip == null || ping.Roundtrip > maxRoundtrip)
                    {
                        maxRoundtrip = ping.Roundtrip ?? 0;
                    }
                }
            }
            MaxRoundtrip = Math.Max(250, maxRoundtrip ?? 0);

            var items = new List<GraphItem>();
            for (var deviceIndex = 0; deviceIndex < Devices.Count; deviceIndex++)
            {
                var deviceModel = Devices[deviceIndex];
                double? lastX = null;
                double? lastY = null;
                double lastNotNullY = 0.95;
                for (var i = deviceModel.Pings.Count - 1; i >= 0; i--)
                {
                    var ping = deviceModel.Pings[i];
                    if (ping.Time < minTime) break;

                    var x = (ping.Time - minTime.Value).TotalMilliseconds / (maxTime.Value - minTime.Value).TotalMilliseconds;
                    if (ping.Roundtrip.HasValue)
                    {
                        var y = 1 - (double)ping.Roundtrip.Value / maxRoundtrip.Value;
                        lastNotNullY = y;
                        if (lastX != null)
                        {
                            items.Add(new GraphSpline
                            {
                                Ip = deviceModel.Ip,
                                X = lastX.Value,
                                Y = lastY.Value,
                                X2 = x,
                                Y2 = y,
                                Dash = Config.Instance.ReferenceIPs.Contains(deviceModel.Ip)
                                    ? 4
                                    : (double?)null,
                                Offset = deviceIndex
                            });
                        }
                        else
                        {
                            items.Add(new GraphStart
                            {
                                Ip = deviceModel.Ip,
                                X = x,
                                Y = y,
                                Offset = deviceIndex
                            });
                        }
                        lastX = x;
                        lastY = y;
                    }
                    else
                    {
                        if (lastX != null)
                        {
                            items.Add(new GraphEnd
                            {
                                Ip = deviceModel.Ip,
                                X = lastX.Value,
                                Y = lastY.Value,
                                Offset = deviceIndex
                            });
                        }
                        else
                        {
                            if (Config.Instance.ShowPingWarningsOnChart)
                            {
                                items.Add(new GraphText
                                {
                                    Ip = deviceModel.Ip,
                                    X = x,
                                    Y = lastNotNullY,
                                    Text = "!",
                                    Offset = deviceIndex
                                });
                            }
                        }
                        lastX = null;
                        lastY = null;
                    }
                }
            }
            foreach (var deviceModel in Devices)
            {
                deviceModel.Pings.RaiseListChangedEvents = false;
                lock (deviceModel.Pings.GetLock())
                {
                    while (deviceModel.Pings.Count > Config.Instance.MaxPingsInMemory && deviceModel.Pings[0].Time < minTime)
                    {
                        deviceModel.Pings.RemoveAt(0);
                    }
                }
                deviceModel.Pings.RaiseListChangedEvents = true;
            }
            return items;
        }

        private List<GraphItem> drawSpeedGraph()
        {
            DateTime? minTime = DateTime.Now.Add(-Config.Instance.SpeedGraphTimeSize);
            DateTime? maxTime = DateTime.Now;
            var maxSpeed = double.MinValue;

            foreach (var deviceModel in Devices)
            {
                for (var i = deviceModel.Throughputs.Count - 1; i > 0; i--)
                {
                    if (deviceModel.Throughputs[i].Time < minTime) break;
                    var current = deviceModel.Throughputs[i];
                    var prev = deviceModel.Throughputs[i - 1];
                    if (current.Time >= prev.Time && current.UpTime >= prev.UpTime)
                    {
                        var speed = (double)(current.Transmit - prev.Transmit) / (current.Time - prev.Time).TotalSeconds;
                        maxSpeed = Math.Max(maxSpeed, speed);
                        speed = (double)(current.Receive - prev.Receive) / (current.Time - prev.Time).TotalSeconds;
                        maxSpeed = Math.Max(maxSpeed, speed);
                    }
                }
            }
            MaxSpeed = Math.Max(0, maxSpeed / 1024);

            var items = new List<GraphItem>();
            for (var deviceIndex = 0; deviceIndex < Devices.Count; deviceIndex++)
            {
                var deviceModel = Devices[deviceIndex];

                double? lastX = null;
                double? lastY = null;
                for (var i = deviceModel.Throughputs.Count - 1; i > 0; i--)
                {
                    var thr = deviceModel.Throughputs[i];
                    if (thr.Time < minTime) break;
                    if (deviceModel.Throughputs[i].Time < deviceModel.Throughputs[i - 1].Time || deviceModel.Throughputs[i].UpTime < deviceModel.Throughputs[i - 1].UpTime)
                    {
                        lastX = null;
                        break;
                    }

                    var x = (thr.Time - minTime.Value).TotalMilliseconds / (maxTime.Value - minTime.Value).TotalMilliseconds;
                    var transmitSpeed = (double)(deviceModel.Throughputs[i].Transmit - deviceModel.Throughputs[i - 1].Transmit) / (deviceModel.Throughputs[i].Time - deviceModel.Throughputs[i - 1].Time).TotalSeconds;
                    var receiveSpeed = (double)(deviceModel.Throughputs[i].Receive - deviceModel.Throughputs[i - 1].Receive) / (deviceModel.Throughputs[i].Time - deviceModel.Throughputs[i - 1].Time).TotalSeconds;
                    var speed = Math.Max(transmitSpeed, receiveSpeed);
                    var y = 1 - (double)speed / maxSpeed;
                    if (lastX != null && speed>0)
                    {
                        items.Add(new GraphLine
                        {
                            Ip = deviceModel.Ip,
                            X = lastX.Value,
                            Y = lastY.Value,
                            X2 = x,
                            Y2 = y,
                            Offset = deviceIndex,
                            Blur=true
                        });
                    }
                    lastY = y;
                    lastX = x;
                }
            }
            return items;
        }

        private void Reload()
        {
            _reloadTimer.Stop();
            autoRouterReboot();
            foreach (var worker in _workers)
            {
                if (!worker.Worker.IsBusy)
                {
                    worker.Worker.RunWorkerAsync();
                }
            }
            _reloadTimer.Start();
        }

        private void soundAlarm()
        {
            if (Config.Instance.DeviceAlarmThreshold <= 0) return;
            foreach (var device in Devices)
            {
                if (device.NetworkAvailability <= Config.Instance.DeviceAlarmThreshold)
                {
                    setAlarm(device,true);
                    AlarmSoundRequested.Fire(this);
                }
                else
                {
                    setAlarm(device, false);
                }
            }
        }

        private void setAlarm(DeviceModel device, bool val)
        {
            if (device.SoundingAlarm != val)
            {
                this.LogInfo(val
                    ? "Sounding alarm for device " + device.Ip
                    : "Stopping alarm for device " + device.Ip);
                device.SoundingAlarm = val;
            }
        }

        private void autoRouterReboot()
        {
            if (Config.Instance.RestartRouterWifiThreshold <= 0 || Config.Instance.RestartRouterCount <= 0) return;

            var routerModel = getDevice(Config.Instance.RouterIP);
            if (routerModel.NetworkAvailability >= Config.Instance.RestartRouterWifiThreshold)
            {
                RouterShouldBeRestarted = 0;
                return;
            }
            if (RouterShouldBeRestarted < Config.Instance.RestartRouterCount)
            {
                RouterShouldBeRestarted++;
                return;
            }
            if (routerModel.IsAuthenticated == true)
            {
                this.LogInfo("Router reached too low a level of availability. Rebooting!");
                routerModel.DoReboot();
            }
        }

        private BackgroundWorker getExtenderWorker(Extender extender, string user, string pass)
        {
            var bw = new BackgroundWorker();
            bw.DoWork += (s, e) =>
            {
                var device = getDevice(extender.IP);
                device.DoReboot = extender.Restart;
                device.IsLoading = true;
                device.IsNetworkAccessible = extender.IsNetworkAccessible();
                //if (device.IsNetworkAccessible)
                {
                    var state = extender.GetState();
                    device.IsHttpAccessible = state.Accessible;
                    device.IsAuthenticated = state.Authenticated;
                    if (device.IsHttpAccessible==true)
                    {
                        if (!state.Authenticated)
                        {
                            var result = extender.Authenticate(user, pass);
                            if (result == null)
                            {
                                device.IsAuthenticated = true;
                                state = extender.GetState();
                            }
                        }
                        if (device.IsAuthenticated==true)
                        {
                            device.IsActive = state.IPConnStatus == Extender.DeviceState.ConnStatusEnum.Success;
                            var speedFormat = "{0:N0}KBps | {1}";
                            device.RxSpeed = string.Format(speedFormat, state.AverageReceive/1024, state.RxRate);
                            device.TxSpeed = string.Format(speedFormat, state.AverageTransmit/1024, state.TxRate);
                            device.UpTime = state.ConnTime;
                            device.Users.AddRange(state.Users.Select(u => new DeviceModel.UserInfo(u)),true);
                            device.Throughputs.AddRange(extender.Throughputs,true);
                            fillUsers();
                            autoExtenderReboot(device);
                            this.Dispatcher.BeginInvoke(new Action(device.Users.ResetBindings), DispatcherPriority.Normal);
                        }
                    }
                }
                //RefreshButtons();
                device.IsLoading = false;
            };
            bw.RunWorkerCompleted += (s, e) =>
            {
                OnPropertyChanged("Devices");
            };
            return bw;
        }

        private void autoExtenderReboot(DeviceModel device)
        {
            if (device.UpTimeSpan > Config.Instance.MaximumDeviceAge)
            {
                this.LogInfo("Device " + device.Ip + " exceeded up time. Rebooting it!");
                device.DoReboot();
            }
        }

        private BackgroundWorker getRouterWorker(Router router, string user, string pass)
        {
            var bw = new BackgroundWorker();
            bw.DoWork += (s, e) =>
            {
                var device = getDevice(router.IP);
                device.DoReboot = () =>
                {
                    RouterShouldBeRestarted = 0;
                    return router.ResetWifi();
                };
                device.IsLoading = true;
                device.IsNetworkAccessible = router.IsNetworkAccessible();
                //if (device.IsNetworkAccessible)
                {
                    var state = router.GetState();
                    device.IsHttpAccessible = state.Accessible;
                    if (device.IsHttpAccessible==true)
                    {
                        device.IsAuthenticated = state.Authenticated;
                        if (!state.Authenticated)
                        {
                            var result = router.Authenticate(user, pass);
                            if (result == null)
                            {
                                device.IsAuthenticated = true;
                                state = router.GetState();
                            }
                        }
                        if (device.IsAuthenticated==true)
                        {
                            device.IsActive = state.Connected;
                            var speedFormat = "{0:N0}KBps | {1}";
                            device.RxSpeed = string.Format(speedFormat, state.AverageReceive/1024, state.ADSLRxSpeed);
                            device.TxSpeed = string.Format(speedFormat, state.AverageTransmit/1024, state.ADSLTxSpeed);
                            device.UpTime = "----------------";
                            device.Users.AddRange(state.Users.Select(u => new DeviceModel.UserInfo(u)), true);
                            device.Throughputs.AddRange(router.Throughputs, true);
                            fillUsers();
                            this.Dispatcher.BeginInvoke(new Action(device.Users.ResetBindings), DispatcherPriority.Normal);
                        }
                    }
                }
                //RefreshButtons();
                device.IsLoading = false;
            };
            bw.RunWorkerCompleted += (s, e) =>
            {
                OnPropertyChanged("Devices");
            };
            return bw;
        }
        
        private BackgroundWorker getReferenceWorker(ReferenceDevice referenceDevice)
        {
            var bw = new BackgroundWorker();
            bw.DoWork += (s, e) =>
            {
                var device = getDevice(referenceDevice.IP);
                device.RebootCommand = null;
                device.DoReboot = null;
                device.IsLoading = true;
                device.IsNetworkAccessible = referenceDevice.IsNetworkAccessible();
                var state = referenceDevice.GetState();
                device.IsHttpAccessible = state.Accessible;
                device.IsAuthenticated = null;
                device.IsActive = null;
                device.IsLoading = false;
                device.CouldNotDetermineIp = referenceDevice.CouldNotDetermineIp;
            };
            bw.RunWorkerCompleted += (s, e) =>
            {
                OnPropertyChanged("Devices");
            };
            return bw;
        }

        private void fillUsers()
        {
            var totalUsers = new List<DeviceModel.UserInfo>();
            var router = getDevice(Config.Instance.RouterIP);
            fillUsersFromExtenders(router.Users);
            totalUsers.AddRange(router.Users);
            foreach (var extender in Devices.Where(d => d.Type=="Extender"))
            {
                fillUsersFromRouter(extender.Users);
                totalUsers.AddRange(extender.Users);
            }
            if (Config.Instance.GetAdditionalNetworkInfo)
            {
                getMoreNetworkInformation(totalUsers);
            }
        }

        private void getMoreNetworkInformation(List<DeviceModel.UserInfo> totalUsers)
        {
            if (_lastInfoTime == null || DateTime.Now - _lastInfoTime.Value > Config.Instance.PingEntireSubnetInterval)
            {
                _lastInfoTime = DateTime.Now;
                var tasks = new List<Task>();
                var range = NetUtil.GetIpRange();
                foreach (var address in range)
                {
                    tasks.Add(PingWrapper.GetInstance().SendPingAsync(address.ToString(), TimeSpan.FromSeconds(1)));
                    var running = tasks.Where(t => t.Status == TaskStatus.Running).ToArray();
                    if (running.Length > 5)
                    {
                        Task.WaitAny(running);
                    }
                }
                Task.WaitAll(tasks.ToArray());
            }
            var devicesOnLan = NetUtil.GetAllDevicesOnLAN();
            foreach (var user in totalUsers)
            {
                if (!string.IsNullOrWhiteSpace(user.MacAddress) /*&& string.IsNullOrWhiteSpace(user.Ip)*/)
                {
                    var mac = System.Net.NetworkInformation.PhysicalAddress.Parse(user.MacAddress);
                    var ips = devicesOnLan.Where(p => p.Value.ToString() == mac.ToString()).Select(p => p.Key).ToList();
                    foreach (var ip in ips)
                    {
                        user.Ip = ip.ToString();
                        var local = user;
                        /*var ping = new MillenniumTools.Common.Ping();
                        ping.SendPingAsync(user.Ip, 1000).ContinueWith(t =>
                        {
                            if (t.Result.Status == System.Net.NetworkInformation.IPStatus.Success)
                            {*/
                        System.Net.Dns.GetHostEntryAsync(ip).ContinueWith(t2 =>
                        {
                            if (!t2.IsFaulted && !string.IsNullOrWhiteSpace(t2.Result.HostName)) { local.Name = t2.Result.HostName; }
                        });
                        /*}
                    });*/
                    }
                }
            }
        }

        private void fillUsersFromRouter(BindingList<DeviceModel.UserInfo> extenderUsers)
        {
            var refresh = false;
            var routerModel = getDevice(Config.Instance.RouterIP);
            foreach (var extenderUser in extenderUsers.Where(eu => string.IsNullOrWhiteSpace(eu.Name) || string.IsNullOrWhiteSpace(eu.Ip)))
            {
                var routerUser = routerModel.Users.FirstOrDefault(ru => ru.MacAddress == extenderUser.MacAddress);
                if (routerUser != null)
                {
                    extenderUser.Name = routerUser.Name;
                    extenderUser.Ip = routerUser.Ip;
                    refresh = true;
                }
            }
            if (refresh) Dispatcher.BeginInvoke(new Action(() => extenderUsers.ResetBindings()), DispatcherPriority.Normal);
        }

        private void fillUsersFromExtenders(BindingList<DeviceModel.UserInfo> routerUsers)
        {
            var refresh = false;
            var extenderUsers = Devices.Where(d => d.Type=="Router").SelectMany(d => d.Users).ToList();
            foreach (var routerUser in routerUsers.Where(ru=>string.IsNullOrWhiteSpace(ru.PacketsRx)&&string.IsNullOrWhiteSpace(ru.PacketsTx)))
            {
                var extenderUser = extenderUsers.FirstOrDefault(u => u.MacAddress == routerUser.MacAddress);
                if (extenderUser != null)
                {
                    routerUser.PacketsRx = extenderUser.PacketsRx;
                    routerUser.PacketsTx = extenderUser.PacketsTx;
                    refresh = true;
                }
            }
            if (refresh) Dispatcher.BeginInvoke(new Action(() => routerUsers.ResetBindings()), DispatcherPriority.Normal);
        }

        private DeviceModel getDevice(string ip)
        {
            lock (Devices.GetLock())
            {
                var device = Devices.FirstOrDefault(d => d.Ip == ip);
                if (device == null)
                {
                    device = new DeviceModel
                    {
                        Ip = ip,
                    };
                    var nextIp = Devices.Where(d => d.Ip.CompareTo(ip) > 0).Min(d => d.Ip);
                    var next = Devices.FirstOrDefault(d => d.Ip == nextIp);
                    if (next != null)
                    {
                        Devices.Insert(Devices.IndexOf(next), device);
                    }
                    else
                    {
                        Devices.Add(device);
                    }
                }
                return device;
            }
        }

        private class WorkerModel
        {
            public BackgroundWorker Worker { get; set; }
            public Device Device { get; set; }
        }

        private void RefreshButtons()
        {
            this.Dispatcher.BeginInvoke(new Action(CommandManager.InvalidateRequerySuggested), DispatcherPriority.Background);
        }


        public void Dispose()
        {
            if (_pingTimer != null)
            {
                _pingTimer.Stop();
                _pingTimer.Dispose();
            }
            if (_reloadTimer != null)
            {
                _reloadTimer.Stop();
                _reloadTimer.Dispose();
            }
        }

    }
}
