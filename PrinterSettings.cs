using System;
using System.Collections.Generic;
using System.Text;


namespace PrinterDaemon
{
    public class PrinterSettings
    {
        public string PrinterName { get; set; } = "";

        public string SumatraPath { get; set; } = "";

        public string InputFolder { get; set; } = "";

        public string ProcessedFolder { get; set; } = "";

        public string ErrorFolder { get; set; } = "";

        public string FileFilter { get; set; } = "*.*";

        public bool DeleteAfterPrint { get; set; }

        public int Interval { get; set; }

        public int RetryCount { get; set; }

        public int RetryDelay { get; set; }
        public int SpoolDelay { get; set; }

        public string FontName { get; set; } = "Consolas";

        public float NormalFontSize { get; set; } = 14;

        public float HeaderFontSize { get; set; } = 14;

        public float TitleFontSize { get; set; } = 18;

        public float TotalFontSize { get; set; } = 16;

        public int ItemsPerPage { get; set; } = 30;
    }
}