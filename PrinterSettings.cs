using System;
using System.Collections.Generic;
using System.Text;


namespace PrinterDaemon
{
    public class PrinterSettings
    {
        public string PrinterName { get; set; } = "";
        public string WatchFolder { get; set; } = "";
        public string FileFilter { get; set; } = "*.txt";
        public bool DeleteAfterPrint { get; set; } = true;
        public int Interval { get; set; } = 1000;
    }
}

