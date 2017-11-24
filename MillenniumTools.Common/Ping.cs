using System.Net.Sockets;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace MillenniumTools.Common
{
    //public delegate void PingCompletedEventHandler(object sender, PingCompletedEventArgs e);

    public abstract class PingWrapper : Component
    {
        protected override bool CanRaiseEvents
        {
            get
            {
                return false;
            }
        }

        public static PingWrapper GetInstance()
        {
            return Config.Instance.UseStandardPing
                ? (PingWrapper)new StandardPingWrapper()
                : (PingWrapper)new BsPingWrapper();
        }

        public abstract Task<PingReplyWrapper> SendPingAsync(string hostnameOrAddress, TimeSpan timeout);

        public abstract void SendAsyncCancel();
    }

    public class PingReplyWrapper
    {
        public long RoundtripTime { get; set; }
        public System.Net.NetworkInformation.IPStatus Status { get; set; }
    }

    public class StandardPingWrapper : PingWrapper
    {
        //private System.Net.NetworkInformation.Ping _ping;

        public StandardPingWrapper()
        {
            //_ping = new System.Net.NetworkInformation.Ping();
        }

        public override Task<PingReplyWrapper> SendPingAsync(string hostnameOrAddress, TimeSpan timeout)
        {
            ensureNotDisposed();
            var _ping = new System.Net.NetworkInformation.Ping();
            return _ping.SendPingAsync(hostnameOrAddress, (int)timeout.TotalMilliseconds).ContinueWith<PingReplyWrapper>(t =>
            {
                _ping.Dispose();
                return new PingReplyWrapper
                {
                    RoundtripTime = t.Result.RoundtripTime,
                    Status = t.Result.Status
                };
            });
        }

        public override void SendAsyncCancel()
        {
            //ensureNotDisposed();
            //ping.SendAsyncCancel();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                ensureNotDisposed();
                //_ping.Dispose();
                //_ping = null;
            }
        }

        private void ensureNotDisposed()
        {
            //if (_ping == null)
            //{
            //    throw new ObjectDisposedException("Cannot use disposed Ping object");
            //}
        }

    }

    public class PingerWrapper : PingWrapper
    {
        private Pinger _ping;
        private Task<PingReplyWrapper> _task;
        public PingerWrapper()
        {
            _ping = new Pinger();
            _ping.Open();
        }

        public override Task<PingReplyWrapper> SendPingAsync(string hostnameOrAddress, TimeSpan timeout)
        {
            ensureNotDisposed();
            var factory = new TaskFactory<PingReplyWrapper>();
            _task = factory.StartNew(() =>
            {
                var result = _ping.Send(hostnameOrAddress, timeout);
                var reply = new PingReplyWrapper
                {
                    RoundtripTime = result != TimeSpan.MaxValue
                        ? (long)result.TotalMilliseconds
                        : 0,
                    Status = result != TimeSpan.MaxValue
                        ? System.Net.NetworkInformation.IPStatus.Success
                        : System.Net.NetworkInformation.IPStatus.IcmpError
                };
                return reply;
            });
            return _task;
        }

        public override void SendAsyncCancel()
        {
            ensureNotDisposed();
            if (_task != null && _task.Status == TaskStatus.Running)
            {
                _task.Wait();
            }
        }

        protected override void Dispose(bool disposing)
        {
            ensureNotDisposed();
            if (disposing)
            {
                _ping.Close();
                _ping = null;
                if (_task != null)
                {
                    _task.Dispose();
                    _task = null;
                }
            }
            base.Dispose(disposing);
        }

        private void ensureNotDisposed()
        {
            if (_ping == null)
            {
                throw new ObjectDisposedException("Cannot use disposed Ping object");
            }
        }
    }

    public class BsPingWrapper : PingWrapper
    {
        private BS.Utilities.Ping _ping;
        private Task<PingReplyWrapper> _task;

        public BsPingWrapper()
        {
            _ping = this.Container == null
                ? new BS.Utilities.Ping()
                : new BS.Utilities.Ping(this.Container);
        }

        public override Task<PingReplyWrapper> SendPingAsync(string hostnameOrAddress, TimeSpan timeout)
        {
            var factory = new TaskFactory<PingReplyWrapper>();
            _task = factory.StartNew(() =>
            {
                _ping.PingTimeout = (int)timeout.TotalMilliseconds;
                var result = _ping.PingHost(hostnameOrAddress);
                var reply = new PingReplyWrapper
                {
                    RoundtripTime = result.AverageTime,
                    Status = result.PingResult == BS.Utilities.PingResponseType.Ok
                        ? System.Net.NetworkInformation.IPStatus.Success
                        : System.Net.NetworkInformation.IPStatus.IcmpError
                };
                return reply;
            });
            return _task;
        }

        public override void SendAsyncCancel()
        {
            if (_task != null && _task.Status == TaskStatus.Running)
            {
                _task.Wait();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _ping.Dispose();
                _ping = null;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Ping functionality in .Net 
    /// </summary>
    public class Pinger
    {
        protected Socket _socket;
        protected bool _isOpen;
        protected ManualResetEvent _readComplete;
        protected byte _lastSequenceNr = 0;
        protected byte[] _pingCommand;
        protected byte[] _pingResult;
        private Stopwatch _stopWatch;

        /// <summary>
        /// Initializes a new instance of the <see cref="Pinger"/> class.
        /// </summary>
        public Pinger()
        {
            _pingCommand = new byte[8];
            _pingCommand[0] = 8; // Type
            _pingCommand[1] = 0; // Subtype
            _pingCommand[2] = 0; // Checksum
            _pingCommand[3] = 0;
            _pingCommand[4] = 1; // Identifier
            _pingCommand[5] = 0;
            _pingCommand[6] = 0; // Sequence number
            _pingCommand[7] = 0;

            _pingResult = new byte[_pingCommand.Length + 1000];
            _stopWatch = new Stopwatch();
        }

        /// <summary>
        /// Opens this instance.
        /// </summary>
        public void Open()
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
            _isOpen = true;
            _readComplete = new ManualResetEvent(false);
        }

        /// <summary>
        /// Closes this instance.
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            _socket.Close();
            _readComplete.Close();
            _socket.Dispose();
        }

        /// <summary>
        /// Sends the Ping to the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        public TimeSpan Send(string address, TimeSpan timeout)
        {
            //try
            //{
            while (_socket.Available > 0)
                _socket.Receive(_pingResult, Math.Min(_socket.Available, _pingResult.Length), SocketFlags.None);

            _readComplete.Reset();
            _pingCommand[6] = _lastSequenceNr++;
            SetChecksum(_pingCommand);
            _stopWatch.Restart();
            int iSend = _socket.SendTo(_pingCommand, new IPEndPoint(IPAddress.Parse(address), 0));

            try
            {
                _socket.BeginReceive(_pingResult, 0, _pingResult.Length, SocketFlags.None, new AsyncCallback(CallBack), null);
            }
            catch (ObjectDisposedException)
            {
            }

            if (_readComplete.WaitOne(timeout, false))
            {
                _stopWatch.Stop();
                if ((_pingResult[20] == 0)
                    && _pingCommand.Skip(4).Take(4)
                    .SequenceEqual(_pingResult.Skip(24).Take(4)))
                {
                    return _stopWatch.Elapsed;
                }
            }

            _stopWatch.Stop();
            //}
            //catch { }
            return TimeSpan.MaxValue;
        }

        /// <summary>
        /// CallBack.
        /// </summary>
        /// <param name="result">The result.</param>
        protected void CallBack(IAsyncResult result)
        {
            if (_isOpen)
            {
                try
                {
                    try
                    {
                        _socket.EndReceive(result);
                    }
                    catch (ArgumentException)
                    {
                    }
                    _readComplete.Set();
                }
                catch (ObjectDisposedException) { }
            }
        }

        /// <summary>
        /// Sets the checksum.
        /// </summary>
        /// <param name="tel">The tel.</param>
        private void SetChecksum(byte[] tel)
        {
            tel[2] = 0;
            tel[3] = 0;
            uint cs = 0;

            for (int i = 0; i < _pingCommand.Length; i = i + 2)
                cs += BitConverter.ToUInt16(_pingCommand, i);
            cs = ~((cs & 0xffffu) + (cs >> 16));
            tel[2] = (byte)cs;
            tel[3] = (byte)(cs >> 8);
        }
    }

   
}
