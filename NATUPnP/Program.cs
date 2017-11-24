using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using System.Xml;
using System.IO;
using UPnP;
using System.Net.Sockets;

namespace NATUPnP
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine(UPnP.NAT.Discover());
                System.Windows.Forms.MessageBox.Show("You have an UPnP-enabled router and your IP is: "+UPnP.NAT.GetExternalIP());
            }
            catch
            {
                System.Windows.Forms.MessageBox.Show("You do not have an UPnP-enabled router.");
            }
            return;
        }
    }
}
