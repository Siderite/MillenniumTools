using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace MillenniumTools.Common
{
    public class Extender:Device
    {
        private static Regex _regState = new Regex(@"var statusPara = new Array\(\s*""?(?<WlanConnStatus>[^\,]+?)""?\s*,\s*""?(?<InternetStatus>[^\,]+?)""?\s*,\s*""?(?<SystemMode>[^\,]+?)""?\s*,\s*""?(?<RefreshTime>[^\,]+?)""?\s*,\s*""?(?<ActiveTime>[^\,]+?)""?\s*,\s*""?(?<FirmwareVersion>[^\,]+?)""?\s*,\s*""?(?<HardwareVersion>[^\,]+?)""?\s*,\s*""?(?<IpConnStatus>[^\,]+?)""?\s*,\s*""?(?<Reserved1>[^\,]+?)""?\s*,\s*""?(?<Reserved2>[^\,]+?)""?\s*\).*?var rootapPara = new Array\(\s*""?(?<RootapSsid>[^\,]+?)""?\s*,\s*""?(?<MacAddress>[^\,]+?)""?\s*,\s*.*?var extenderPara = new Array\(\s*""?(?<SysMode>[^\,]+?)""?\s*,\s*""?(?<OperMode>[^\,]+?)""?\s*,\s*""?(?<ExtenderSsid>[^\,]+?)""?\s*,\s*""?(?<CurChan>[^\,]+?)""?\s*,\s*""?(?<uChanWidth>[^\,]+?)""?\s*,\s*""?(?<WlanMode>[^\,]+?)""?\s*,\s*""?(?<CurRootapRssi>[^\,]+?)""?\s*,\s*""?(?<CurRootapRate>[^\,]+?)""?\s*,\s*""?(?<LanIp>[^\,]+?)""?\s*,\s*""?(?<LanMacAddress>[^\,]+?)""?\s*,\s*""?(?<LanIpStatus>[^\,]+?)""?\s*,\s*""?(?<DhcpServerStatus>[^\,]+?)""?\s*,\s*""?(?<TxRate>[^\,]+?)""?\s*,\s*""?(?<RxRate>[^\,]+?)""?\s*,\s*""?(?<ConnTime>[^\,]+?)""?\s*,\s*", RegexOptions.Compiled | RegexOptions.Singleline|RegexOptions.ExplicitCapture);
        private static Regex _regUsers = new Regex(@"""(?<MacAddress>([\dA-F]{2}-){5}[\dA-F]{2})""\s*,\s*\d+\s*,\s*(?<PacketsRx>\d+)\s*,\s*(?<PacketsTx>\d+)\s*,\s*\d+\s*,", RegexOptions.Compiled | RegexOptions.Singleline|RegexOptions.ExplicitCapture);
        private static Regex _regThroughput = new Regex(@"var WlanThroughputPara = new Array\(\s*""?(?<Transmit>[^\,]+?)""?\s*,\s*""?(?<Receive>[^\,]+?)""?\s*,", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

        private static Regex _regUpTime = new Regex(@"(?<days>\d+)\s+days\s*(?<hours>\d+):(?<minutes>\d+):(?<seconds>\d+)", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private List<ThroughputResult> _throughputs;

        public Extender(string ip): base(ip)
        {
            _throughputs = new List<ThroughputResult>();
        }

        public string Authentication { get; set; }

        public List<ThroughputResult> Throughputs
        {
            get
            {
                return _throughputs;
            }
        }

        private ExtenderClient getClient()
        {
            return new ExtenderClient(this);
        }

        public string Authenticate(string username, string password)
        {
            Authentication = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
            var client = getClient();
            var response = client.Get("http://" + IP);
            if (response.StatusCode != HttpStatusCode.OK) return "Authentication failed (" + response.StatusCode + ")";
            return null;
        }

        public DeviceState GetState()
        {
            var result = new DeviceState();

            var client = getClient();
            //accessible
            var response = client.Get("http://" + IP);
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    //everything OK
                    result.Accessible = true;
                    result.Authenticated = true;
                    break;
                case HttpStatusCode.Unauthorized:
                    //not authenticated
                    result.Accessible = true;
                    result.Authenticated = false;
                    return result;
                default:
                    //something is wrong
                    result.Accessible = false;
                    result.Authenticated = false;
                    return result;
            }


            var deviceStateTask = client.GetAsync("http://" + IP + "/userRpm/StatusRpm.htm");
            var usersTask = client.GetAsync("http://" + IP + "/userRpm/WlanStationRpm.htm");
            var throughputTask = client.GetAsync("http://" + IP + "/userRpm/WlanThroughputIframe.htm");
            var tasks = new[] { deviceStateTask, usersTask, throughputTask };
            Task.WaitAny(tasks);
            if (tasks.Where(t => t.IsCompleted).Any(t => !t.Result.IsSuccessStatusCode))
            {
                result.Accessible = false;
                return result;
            }
            Task.WaitAll(tasks);
            if (tasks.Any(t => !t.Result.IsSuccessStatusCode))
            {
                result.Accessible = false;
                return result;
            }
            var contentTasks = new[] { deviceStateTask.Result.Content.ReadAsStringAsync(),
                usersTask.Result.Content.ReadAsStringAsync(),
                throughputTask.Result.Content.ReadAsStringAsync()
            };
            Task.WaitAll(contentTasks);
            parseDeviceState(result, contentTasks[0].Result);
            parseUsers(result, contentTasks[1].Result);
            parseThroughput(result, contentTasks[2].Result);

            return result;
        }

        private void parseThroughput(DeviceState result, string html)
        {
            html = Regex.Replace(html, @"\<!--.*?--\>", "");
            var m = _regThroughput.Match(html);
            var data = getDataFromHtmlRegex(m, _regThroughput);
            _throughputs.Add(new ThroughputResult
            {
                Transmit = long.Parse(data["Transmit"]),
                Receive = long.Parse(data["Receive"]),
                Time = DateTime.Now,
                UpTime = ParseUpTime(result.ConnTime)
            });
            if (_throughputs.Count < 2) return;
            var last = _throughputs[_throughputs.Count - 1];
            var prev = _throughputs[_throughputs.Count - 2];
            /*if (last.Transmit < prev.Transmit || last.Receive < prev.Receive || last.UpTime<prev.UpTime)
            {
                _throughputs.RemoveRange(0, _throughputs.Count - 1);
            }*/
            if (_throughputs.Count > Config.Instance.MaxPingsInMemory)
            {
                _throughputs.RemoveRange(0, _throughputs.Count - Config.Instance.MaxPingsInMemory);
            }
            if (_throughputs.Count < 2)
            {
                result.AverageTransmit = 0;
                result.AverageReceive = 0;
            }
            else
            {
                var first = _throughputs.Last();
                for (var i = _throughputs.Count - 2; i >= 0; i--)
                {
                    if (first.Time < _throughputs[i].Time || first.UpTime < _throughputs[i].UpTime) break;
                    first = _throughputs[i];
                    if (last.Time - first.Time >= TimeSpan.FromMinutes(10)) break;
                }
                result.AverageTransmit = ((last.Transmit - first.Transmit) / (last.Time - first.Time).TotalSeconds).Constrain(0,null);
                result.AverageReceive = ((last.Receive - first.Receive) / (last.Time - first.Time).TotalSeconds).Constrain(0, null);
            }
        }

        public static TimeSpan ParseUpTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return TimeSpan.Zero;
            var match = _regUpTime.Match(value);
            if (match.Success)
            {
                var days = int.Parse(match.Groups["days"].Value);
                var hours = int.Parse(match.Groups["hours"].Value);
                var minutes = int.Parse(match.Groups["minutes"].Value);
                var seconds = int.Parse(match.Groups["seconds"].Value);
                return new TimeSpan(days, hours, minutes, seconds);
            }
            else
            {
                return TimeSpan.Zero;
            }
        }

        public string Restart()
        {
            var client=getClient();
            var response = client.Get("http://" + IP + "/userRpm/SysRebootRpm.htm?Reboot=Reboot");
            this.LogInfo("Restarting extender: " + response.StatusCode);
            if (response.StatusCode != HttpStatusCode.OK) return "Reboot failed (" + response.StatusCode + ")";
            return null;
        }

        private void parseDeviceState(DeviceState result, string html)
        {
            html = Regex.Replace(html, @"\<!--.*?--\>", "");
            var m = _regState.Match(html);
            var data = getDataFromHtmlRegex(m, _regState);
            result.ConnTime = data["ConnTime"];
            result.TxRate = data["TxRate"];
            result.RxRate = data["RxRate"];
            result.ExtenderSsid = data["ExtenderSsid"];
            result.CurChan = int.Parse(data["CurChan"]);
            result.WlanConnected = int.Parse(data["WlanConnStatus"])==1;
            result.InternetStatus = (DeviceState.InternetStatusEnum)int.Parse(data["InternetStatus"]);
            result.IPConnStatus = (DeviceState.ConnStatusEnum)int.Parse(data["IpConnStatus"]);
            result.CurRootapRssi = double.Parse(data["CurRootapRssi"]);
        }

        private void parseUsers(DeviceState result, string html)
        {
            html = Regex.Replace(html, @"\<!--.*?--\>", "");
            var m = _regUsers.Match(html);
            result.Users.Clear();
            while (m.Success)
            {
                var data = getDataFromHtmlRegex(m, _regUsers);
                var user=new User {
                    MacAddress=data["MacAddress"],
                    PacketsRx=data["PacketsRx"],
                    PacketsTx=data["PacketsTx"],
                    Type="Wi-Fi"
                };
                result.Users.Add(user);

                m = m.NextMatch();
            }
        }

        private class ExtenderClient:GenericClient
        {
            public ExtenderClient(Extender extender):base(extender)
            {
            }

            protected override void BeforeRequest()
            {
                var extender=(Extender)_owner;
                if (!string.IsNullOrWhiteSpace(extender.Authentication))
                {
                    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", extender.Authentication);
                }
            }

            protected override void ProcessResponse(HttpResponseMessage response)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var extender = (Extender)_owner;
                    extender.Authentication = null;
                }
            }
        }

        public class DeviceState
        {
            public DeviceState()
            {
                Users = new List<User>();
            }

            public bool Accessible { get; set; }

            public bool Authenticated { get; set; }

            public string ConnTime { get; set; }
            public string TxRate { get; set; }
            public string RxRate { get; set; }
            public string ExtenderSsid { get; set; }

            public int CurChan { get; set; }

            public bool WlanConnected { get; set; }

            public InternetStatusEnum InternetStatus { get; set; }

            public ConnStatusEnum IPConnStatus { get; set; }

            public double CurRootapRssi { get; set; }

            public List<User> Users { get; set; }

            public double AverageTransmit { get; set; }

            public double AverageReceive { get; set; }

            public enum InternetStatusEnum
            {
                Success=0,
                Failure=1,
                NoDns=2
            }

            public enum ConnStatusEnum
            {
                Success=0,
                Failure=1,
                Unknown=2
            }
        }

        public void EnableUserAccess(bool val)
        {
            var client = getClient();
            var s = "http://" + IP + "/userRpm/WlanMacFilterRpm.htm?Page=1&" + (val ? "Enfilter" : "Disfilter") + "=1&vapIdx=";
            var response = client.Get(s);
        }

        public void ChangeUserAccess(string mac, string description,bool val)
        {
            var client = getClient();
            var changed = false;
            var s = "http://" + IP + "/userRpm/WlanMacFilterRpm.htm?Mac=" + HttpUtility.UrlEncode(mac) + "&Desc=" + HttpUtility.UrlEncode(description) + "&entryEnabled=" + (val ? "1" : "0") + "&Changed=" + (changed ? "1" : "0") + "&SelIndex=0&Page=1&vapIdx=0&Save=Save";
            var response = client.Get(s);
            changed = true;
            s = "http://" + IP + "/userRpm/WlanMacFilterRpm.htm?Mac=" + HttpUtility.UrlEncode(mac) + "&Desc=" + HttpUtility.UrlEncode(description) + "&entryEnabled=" + (val ? "1" : "0") + "&Changed=" + (changed ? "1" : "0") + "&SelIndex=0&Page=1&vapIdx=0&Save=Save";
            response = client.Get(s);
        }
    }
}
