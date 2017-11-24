using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace MillenniumTools.Common
{
    public class Router : Device
    {
        private static Regex _regAuthcookie = new Regex(@"^xAuth_SESSION_ID=(?<Authcookie>[^;]+);", RegexOptions.Compiled|RegexOptions.ExplicitCapture);
        private static Regex _regNonce = new Regex(@"var nonce = ""(?<nonce>[^""]+)"";", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        private static Regex _regState = new Regex(@"\<td[^\>]*>Servizio ADSL\<\/td\>\s*\<td[^\>]*\>(?<ADSL>[^\>]*)\<\/td\>\s*\<td[^\>]*\>Telegestione\<\/td\>\s*\<td[^\>]*\>(?<Telegestione>[^\>]*)\<\/td\>\s*\<\/tr\>\s*\<tr[^\>]*\>\s*\<td[^\>]*\>Velocità trasmissione\<\/td\>\s*\<td[^\>]*\>(?<ADSLTxSpeed>[^\>]*)\<\/td\>\s*\<td[^\>]*\>Velocità ricezione\<\/td\>\s*\<td[^\>]*\>(?<ADSLRxSpeed>[^\>]*)\<\/td\>\s*\<\/tr\>\s*\<tr[^\>]*\>\s*\<td[^\>]*\>Modalità ADSL\<\/td\>\s*\<td[^\>]*\>(?<ADSLMode>[^\>]*)\<\/td\>\s*\<td[^\>]*\>VPI/VCI\<\/td\>\s*\<td[^\>]*\>(?<VPIVCI>[^\>]*)\<\/td\>\s*\<\/tr\>\s*\<tr[^\>]*\>\s*\<td[^\>]*\>Modalità\<\/td\>\s*\<td[^\>]*\>(?<InternetMode>[^\>]*)\<\/td\>\s*\<td[^\>]*\>Profilo tariffario\<\/td\>\s*\<td[^\>]*\>(?<InternetBiz>[^\>]*)\<\/td\>\s*\<\/tr\>\s*\<tr[^\>]*\>\s*\<td[^\>]*\>Protocollo di Connessione\<\/td\>\s*\<td[^\>]*\>(?<ConnectionProtocol>[^\>]*)\<\/td\>\s*\<td[^\>]*\>Modalità di connessione\<\/td\>\s*\<td[^\>]*\>(?<ConnectionMode>[^\>]*)\<\/td\>\s*\<\/tr\>\s*\<tr[^\>]*\>\s*\<td[^\>]*\>Dettagli connessione\<\/td\>\s*\<\/tr\>\s*\<tr[^\>]*\>\s*\<td[^\>]*\>IP pubblico\<\/td\>\s*\<td[^\>]*\>(?<PublicIP>[^\>]*)\<\/td\>\s*\<td[^\>]*\>Stato connessione da modem\<\/td\>\s*\<td[^\>]*\>(?<Connected>[^\>]*)\<\/td\>\s*\<\/tr\>\s*\<tr[^\>]*\>\s*\<td[^\>]*\>DNS Primario in uso\<\/td\>\s*\<td[^\>]*\>(?<DNS1>[^\>]*)\<\/td\>\s*\<td[^\>]*\>DNS Secondario in uso\<\/td\>\s*\<td[^\>]*\>(?<DNS2>[^\>]*)\<\/td\>\s*\<\/tr\>\s*\<tr[^\>]*\>\s*\<td[^\>]*\>DNS Primario default\<\/td\>\s*\<td[^\>]*\>(?<DNS1d>[^\>]*)\<\/td\>\s*\<td[^\>]*\>DNS Secondario default\<\/td\>\s*\<td[^\>]*\>(?<DNS2d>[^\>]*)\<\/td\>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
        private static Regex _regUsers = new Regex(@"\<tr\>\s*\<td[^\>]*\>(?<Type>(Ethernet|Wi-Fi))\<\/td\>\s*\<td[^\>]*\>(?<Name>[^\<]*)\<\/td\>\s*\<td[^\>]*\>(?<MacAddress>[^\<]*)\<\/td\>\s*\<td[^\>]*\>(?<Ip>[^\<]*)\<\/td\>\s*\<td[^\>]*\>(?<XXX>[^\<]*)\<\/td\>\s*\<\/tr\>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
        private static Regex _regStatistics = new Regex(@"<td[^\>]*\>Ricevuto\<\/td\>\s*<td[^\>]*\>\<a href=""lanStatus.lp""\>LAN\<\/a\>\<\/td\>\s*<td[^\>]*\>(?<Receive>\d+)\<\/td\>\s*<td[^\>]*\>\d+\<\/td\>\s*\<\/tr\>\s*\<tr\>\s*<td[^\>]*\>Inviato\<\/td\>\s*<td[^\>]*\>(?<Transmit>\d+)\<\/td\>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

        private List<ThroughputResult> _throughputs;

        public Router(string ip)
            : base(ip)
        {
            _throughputs = new List<ThroughputResult>();
        }

        public string Authcookie { get; set; }

        public List<ThroughputResult> Throughputs
        {
            get
            {
                return _throughputs;
            }
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
                case HttpStatusCode.Found:
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

            var deviceStateTask = client.GetAsync("http://" + IP + "/wanStatus.lp");
            var usersTask = client.GetAsync("http://" + IP + "/lanStatus.lp");
            var statsTask = client.GetAsync("http://" + IP + "/statisticsAG.lp");
            var tasks = new[] { deviceStateTask, usersTask, statsTask };
            Task.WaitAny(tasks);
            if (tasks.Where(t=>t.IsCompleted).Any(t=>!t.Result.IsSuccessStatusCode)) {
                result.Accessible = false;
                return result;
            }
            Task.WaitAll(tasks);
            if (tasks.Any(t=>!t.Result.IsSuccessStatusCode)) {
                result.Accessible = false;
                return result;
            }
            var contentTasks = new[] { deviceStateTask.Result.Content.ReadAsStringAsync(),
                usersTask.Result.Content.ReadAsStringAsync(),
                statsTask.Result.Content.ReadAsStringAsync()
            };
            Task.WaitAll(contentTasks);
            parseDeviceState(result, contentTasks[0].Result);
            parseUsers(result, contentTasks[1].Result);
            parseStatistics(result, contentTasks[2].Result);

            return result;
        }

        private void parseStatistics(DeviceState result, string html)
        {
            html = Regex.Replace(html, @"\<!--.*?--\>", "");
            var m = _regStatistics.Match(html);
            var data = getDataFromHtmlRegex(m, _regStatistics);
            _throughputs.Add(new ThroughputResult
            {
                Transmit = long.Parse(data["Transmit"]) * 1508,
                Receive = long.Parse(data["Receive"]) * 1508,
                Time = DateTime.Now
            });
            if (_throughputs.Count < 2) return;
            var last = _throughputs[_throughputs.Count - 1];
            var prev = _throughputs[_throughputs.Count - 2];
            /*if (last.Transmit < prev.Transmit || last.Receive < prev.Receive)
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
                var first=_throughputs.Last();
                for (var i = _throughputs.Count - 1; i >= 0; i--)
                {
                    if (first.Time < _throughputs[i].Time
                        || first.UpTime < _throughputs[i].UpTime
                        || first.Transmit < _throughputs[i].Transmit
                        || first.Receive < _throughputs[i].Receive
                        ) break;
                    first = _throughputs[i];
                    if (last.Time - first.Time >= TimeSpan.FromMinutes(10)) break;
                }
                result.AverageTransmit =  ((last.Transmit - first.Transmit) / (last.Time - first.Time).TotalSeconds).Constrain(0,null);
                result.AverageReceive = ((last.Receive - first.Receive) / (last.Time - first.Time).TotalSeconds).Constrain(0, null);
            }

        }

        public string Authenticate(string username, string password)
        {
            var client = getClient();
            var response = client.Get("http://" + IP + "/loginMask.lp");
            if (response.StatusCode != HttpStatusCode.Found) return response.ReasonPhrase;
            response = client.Get("http://" + IP + "/index_auth.lp");
            if (!response.IsSuccessStatusCode) return response.ReasonPhrase;;
            var nonce = parseNonce(response.Content.ReadAsStringAsync().Result);
            var hidepw = computePassword(username, password, nonce);
            response = client.Post("http://" + IP + "/index_auth.lp", new[] {
                new KeyValuePair<string,string>("rn",Authcookie),
                new KeyValuePair<string,string>("hidepw",hidepw)
            });
            if (response.StatusCode != HttpStatusCode.Found) return response.ReasonPhrase;
            return null;
        }

        public string ResetWifi()
        {
            var client = getClient();
            var response = client.Get("http://" + IP + "/wifiRestart.lp");
            this.LogInfo("Resetting Wi-Fi: " + response.StatusCode);
            if (!response.IsSuccessStatusCode) return response.ReasonPhrase;
            return null;
        }

        public string RestartModem()
        {
            var client = getClient();
            var response=client.Get("http://" + IP + "/resetAG.lp?action=saveRestart");
            this.LogInfo("Restarting router: " + response.StatusCode);
            if (response.StatusCode != HttpStatusCode.InternalServerError) return "Connection ended with " + response.StatusCode + " which is not normal for a modem restart";
            return null;
        }

        private RouterClient getClient()
        {
            return new RouterClient(this);
        }

        private string parseNonce(string html)
        {
            var m = _regNonce.Match(html);
            return m.Groups["nonce"].Value;
        }

        private string computePassword(string username, string password, string nonce)
        {
            var ha1 = md5(username + ":Technicolor Gateway:" + password);
            var ha2 = md5("GET:/index_auth.lp");
            var hidepwd = md5(ha1 + ":" + nonce + ":00000001:xyz:auth:" + ha2);
            return hidepwd;
        }

        private string md5(string text)
        {
            var m = MD5.Create();
            var hash = m.ComputeHash(Encoding.UTF8.GetBytes(text));
            return string.Join("", hash.Select(b => b.ToString("x2")));
        }

        private void parseDeviceState(DeviceState state, string html)
        {
            var m = _regState.Match(html);
            var data = getDataFromHtmlRegex(m, _regState);
            state.ADSL = data["ADSL"] == "Attivo";
            state.Telegestione = data["Telegestione"] == "Attiva";
            state.ADSLTxSpeed = data["ADSLTxSpeed"];
            state.ADSLRxSpeed = data["ADSLRxSpeed"];
            state.ADSLMode = data["ADSLMode"];
            state.VPIVCI = data["VPIVCI"];
            state.InternetMode = data["InternetMode"];
            state.InternetBiz = data["InternetBiz"];
            state.Connected = data["Connected"] == "Attiva";
            state.PublicIP = data["PublicIP"];
            state.ConnectionMode = data["ConnectionMode"];
            state.ConnectionProtocol = data["ConnectionProtocol"];
            state.DNS1 = data["DNS1"];
            state.DNS2 = data["DNS2"];
            state.DNS1d = data["DNS1d"];
            state.DNS2d = data["DNS2d"];
        }

        private void parseUsers(DeviceState result, string html)
        {
            html = Regex.Replace(html, @"\<!--.*?--\>", "");
            var m = _regUsers.Match(html);
            result.Users.Clear();
            while (m.Success)
            {
                var data = getDataFromHtmlRegex(m, _regUsers);
                var user = new User
                {
                    Name=data["Name"],
                    MacAddress = data["MacAddress"],
                    Ip=data["Ip"],
                    Type = data["Type"]
                };
                result.Users.Add(user);

                m = m.NextMatch();
            }
        }

        private class RouterClient:GenericClient
        {
            public RouterClient(Router router):base(router)
            {
            }

            protected override void BeforeRequest()
            {
                ensureCookie();
            }

            protected override void ProcessResponse(HttpResponseMessage response)
            {
                var authHeaders = response.Headers.Where(h => h.Key == "Set-Cookie").ToList();
                if (authHeaders.Any())
                {
                    var val = authHeaders.First().Value.First();
                    var m = _regAuthcookie.Match(val);
                    var router = (Router)_owner;
                    router.Authcookie = m.Groups["Authcookie"].Value;
                }

            }

            private void ensureCookie()
            {
                var router = (Router)_owner;
                if (!string.IsNullOrWhiteSpace(router.Authcookie))
                {
                    _cookieContainer.Add(new Uri("http://" + router.IP), new Cookie("xAuth_SESSION_ID", router.Authcookie));
                }
            }
        }

        public class DeviceState
        {
            public DeviceState()
            {
                Users = new List<User>();
            }

            public bool Authenticated { get; set; }

            public bool Accessible { get; set; }

            public bool ADSL { get; set; }

            public bool Telegestione { get; set; }

            public string ADSLTxSpeed { get; set; }

            public string ADSLRxSpeed { get; set; }

            public string ADSLMode { get; set; }

            public string VPIVCI { get; set; }

            public string InternetMode { get; set; }

            public string InternetBiz { get; set; }

            public bool Connected { get; set; }

            public string PublicIP { get; set; }

            public string ConnectionMode { get; set; }
            public string ConnectionProtocol { get; set; }
            public string DNS1 { get; set; }
            public string DNS2 { get; set; }
            public string DNS1d { get; set; }
            public string DNS2d { get; set; }

            public List<User> Users { get; set; }


            public double AverageTransmit { get; set; }

            public double AverageReceive { get; set; }
        }

    }
}
