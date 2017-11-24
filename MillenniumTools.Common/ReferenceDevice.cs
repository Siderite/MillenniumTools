using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MillenniumTools.Common
{
    public class ReferenceDevice:Device
    {
        private string _nameOrAddress;
        private string _ip;

        public ReferenceDevice(string nameOrAddress)
            : base(nameOrAddress)
        {
            _nameOrAddress = nameOrAddress;
            getIp();
        }

        public bool CouldNotDetermineIp { get; set; }

        private void getIp()
        {
            IPAddress address;
            if (!IPAddress.TryParse(_nameOrAddress, out address))
            {
                Dns.GetHostAddressesAsync(_nameOrAddress).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        CouldNotDetermineIp = true;
                        return;
                    }
                    var addr = t.Result.Random(a => a.AddressFamily == AddressFamily.InterNetwork);
                    if (addr == null)
                    {
                        CouldNotDetermineIp = true;
                    }
                    else
                    {
                        _ip = addr.ToString();
                        CouldNotDetermineIp = false;
                    }
                });
            }
            else
            {
                _ip = address.ToString();
                CouldNotDetermineIp = false;
            }
        }

        public override Task<PingReplyWrapper> Ping(string ip=null,Action<PingReplyWrapper> action = null)
        {
            if (_ip == null)
            {
                getIp();
                return null;
            }
            var task=base.Ping(_ip,action).ContinueWith<PingReplyWrapper>(t=>{
                if (t.IsFaulted || t.Result == null) return null;
                if (t.Result.Status != IPStatus.Success)
                {
                    getIp();
                }
                return t.Result;
            });
            return task;
        }

        public ReferenceDevice.DeviceState GetState()
        {
            var result = new DeviceState();
            getIp();
            var client = getClient();
            //accessible
            var response = client.Get("http://" + IP);
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                case HttpStatusCode.MovedPermanently:
                case HttpStatusCode.Found:
                    //everything OK
                    result.Accessible = true;
                    break;
                default:
                    //something is wrong
                    result.Accessible = false;
                    return result;
            }
            return result;
        }

        private GoogleClient getClient()
        {
            return new GoogleClient(this);
        }


        public class GoogleClient : Device.GenericClient
        {
            public GoogleClient(Device googleDevice):base(googleDevice)
            {
            }
        }

        public class DeviceState
        {
            public bool Accessible { get; set; }
        }
    }
}
