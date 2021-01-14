using System;
using System.Collections.Generic;
using System.Text;

namespace WebSiteMonitoring
{
    public class EmailSettings
    {
        public string senderEmail { get; set; } = null;

        public string senderPassword { get; set; } = null;

        public List<string> receivers { get; set; } = null;

    }
}
