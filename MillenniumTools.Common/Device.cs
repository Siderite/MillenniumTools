using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace MillenniumTools.Common
{
    public class Device:IDisposable
    {
        private bool _isNetworkAccessible;
        private PingWrapper _ping;

        public Device(string ip)
        {
            IP = ip;
            _ping = PingWrapper.GetInstance();
        }

        public string IP { get; set; }

        public bool IsNetworkAccessible()
        {
            var task = Ping();
            if (task != null)
            {
                try
                {
                    task.Wait();
                }
                catch { }
            }
            return _isNetworkAccessible;
        }

        public virtual Task<PingReplyWrapper> Ping(string ip=null, Action<PingReplyWrapper> action = null)
        {
            ip = ip ?? IP;
            //_ping.SendAsyncCancel();
            var task = _ping.SendPingAsync(ip, Config.Instance.PingTimeout)
                .ContinueWith<PingReplyWrapper>(t =>
                {
                    if (t!=null && !t.IsFaulted)
                    {
                        _isNetworkAccessible = t.Result != null && t.Result.Status == IPStatus.Success;
                        if (action != null) action(t.Result);
                        return t.Result;
                    }
                    else
                    {
                        if (action != null) action(null);
                        return null;
                    }
                });
            return task;
        }

        protected IDictionary<string,string> getDataFromHtmlRegex(Match m, Regex reg)
        {
            if (!m.Success) return null;
            var obj = new Dictionary<string, string>();
            foreach (var name in reg.GetGroupNames())
            {
                var val = HttpUtility.HtmlDecode(m.Groups[name].Value).Trim();
                obj[name] = val;
            }
            return obj;
        }



        public class GenericClient
        {
            protected Device _owner;
            protected HttpClient _client;
            protected CookieContainer _cookieContainer;

            public GenericClient(Device owner)
            {
                this._owner = owner;
                _cookieContainer = new CookieContainer();
                _client = new HttpClient(new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    CookieContainer = _cookieContainer
                });
                _client.Timeout = Config.Instance.ClientTimeout;

                _client.DefaultRequestHeaders.Add("Referer", Config.Instance.UserAgent);
                _client.DefaultRequestHeaders.Host = _owner.IP.ToString();
                _client.DefaultRequestHeaders.Add("Origin", "http://" + _owner.IP);
                _client.DefaultRequestHeaders.Referrer = new Uri("http://" + _owner.IP + "/");
            }

            internal HttpRequestHeaders DefaultRequestHeaders
            {
                get { return _client.DefaultRequestHeaders; }
            }

            internal HttpResponseMessage Get(string url)
            {
                try
                {
                    BeforeRequest();
                    var response = _client.GetAsync(url).Result;
                    ProcessResponse(response);
                    return response;
                }
                catch (Exception ex)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        Content = new StringContent(ex.Message),
                        ReasonPhrase = ex.Message
                    };
                }
            }

            internal Task<HttpResponseMessage> GetAsync(string url)
            {
                BeforeRequest();
                var task = _client.GetAsync(url).ContinueWith<HttpResponseMessage>(t =>
                {
                    var response = t.Result;
                    ProcessResponse(response);
                    return response;
                });
                return task;
            }

            internal HttpResponseMessage Post(string url, IEnumerable<KeyValuePair<string, string>> values)
            {
                try
                {
                    BeforeRequest();
                    var response = _client.PostAsync(url, new FormUrlEncodedContent(values)).Result;
                    ProcessResponse(response);
                    return response;
                }
                catch (Exception ex)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        Content = new StringContent(ex.Message),
                        ReasonPhrase = ex.Message
                    };
                }
            }

            protected virtual void ProcessResponse(HttpResponseMessage response)
            {
            }

            protected virtual void BeforeRequest()
            {
            }
        }

        public void Dispose()
        {
            _ping.Dispose();
            _ping = null;
        }

    }
}
