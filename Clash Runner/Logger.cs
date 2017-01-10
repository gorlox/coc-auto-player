using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clash_Runner
{
    public static class Logger
    {
        public static event EventHandler<LogEventArgs> Logged;
        static readonly object _lock = new object();

        public static void Log(string msg)
        {
            lock(_lock)
            {
                msg = DateTime.Now.ToString("MM / dd / yyyy hh: mm:ss.fff tt") + "  " + msg;
                Console.WriteLine(msg);
                Log(new LogEventArgs() { Text = msg });
            }
        }

        public static void Log(LogEventArgs e)
        {
            EventHandler<LogEventArgs> handler = Logged;
            if (handler != null)
            {
                handler(null, e);
            }
        }

    }

    public class LogEventArgs : EventArgs
    {
        public string Text { get; set; }
    }
}
