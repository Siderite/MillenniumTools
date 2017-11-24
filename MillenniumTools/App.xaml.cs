using MillenniumTools.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MillenniumTools
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            //Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("it-IT");
        }
    }
}
