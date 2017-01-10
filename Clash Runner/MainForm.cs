using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Clash_Runner
{
    public partial class MainForm : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        enum KeyModifier
        {
            None = 0,
            Alt = 1,
            Control = 2,
            Shift = 4,
            WinKey = 8
        }


        bool _running = false;
        delegate void SetTextCallback(object sender, LogEventArgs e);
        delegate void SetButtonTextCallback(string text);


        public MainForm()
        {
            InitializeComponent();

            Logger.Logged += Logger_Logged;
            int id = 0;     // The id of the hotkey. 
            RegisterHotKey(this.Handle, id, (int)KeyModifier.Control, Keys.Space.GetHashCode());
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == 0x0312)
            {
                /* Note that the three lines below are not needed if you only want to register one hotkey.
                 * The below lines are useful in case you want to register multiple keys, which you can use a switch with the id as argument, or if you want to know which key/modifier was pressed for some particular reason. */

                Keys key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);                  // The key of the hotkey that was pressed.
                KeyModifier modifier = (KeyModifier)((int)m.LParam & 0xFFFF);       // The modifier of the hotkey that was pressed.
                int id = m.WParam.ToInt32();                                        // The id of the hotkey that was pressed.
                this.WindowState = FormWindowState.Minimized;
                this.Show();
                this.WindowState = FormWindowState.Normal;
                startStopApp(false);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, 0);
        }

        private void StartStopButton_Click(object sender, EventArgs e)
        {
            startStopApp(false);
        }

        private void Logger_Logged(object sender, LogEventArgs e)
        {
            if (!_running && !e.Text.ToLower().Contains("stop"))
                return;
            if (this.logText.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(Logger_Logged);
                this.BeginInvoke(d, new object[] { sender, e });
            }
            else
            {
                var lines = new List<string>(this.logText.Lines);
                if (lines.Count > 1000)
                {
                    lines = lines.Skip(lines.Count - 100).ToList();
                }
                lines.Add(e.Text);
                this.logText.Lines = lines.ToArray();
                this.logText.SelectionStart = this.logText.Text.Length;
                this.logText.ScrollToCaret();
            }
        }

        private void SetButtonText(string text)
        {
            if (StartStopButton.InvokeRequired)
            {
                SetButtonTextCallback d = new SetButtonTextCallback(SetButtonText);
                this.BeginInvoke(d, new object[] { text });
            }
            else
            {
                StartStopButton.Text = text;
            }
        }

        private void startStopApp(bool forceStop)
        {
            if (!_running && !forceStop)
            {
                ClashLogic.Start();
                _running = true;
            }
            else
            {
                ClashLogic.Stop();
                _running = false;
            }

            if (_running)
            {
                //Task t = new Task(MonitorConnection);
                //t.Start();
            }
            SetButtonText(_running ? "Stop" : "Start");
        }


        private void MonitorConnection()
        {
            OpenCVWrapper cv = new OpenCVWrapper();
            string ImageDir = ConfigurationManager.AppSettings["ImageDir"];
            ImageDir = (ImageDir + "Common/reload_button.png").Replace("\\", "\\\\");
            while (_running)
            {
                var lst = cv.Find(ImageDir, 0.75f);
                if (lst != null && lst.Count > 0)
                {
                    break;
                }
            }
            var running = _running;
            if (_running)
            {
                startStopApp(true);
                Logger.Log("Connection Detected.  Resting 15 minutes...");
                Mailer m = new Mailer();
                m.SendMail("Connection Detected", "Connection Detected.  Resting 15 minutes");
                Thread.Sleep((1000 * 60 * 15) - (45 * 1000));
                m.SendMail("Starting Back in 45 Seconds", "Starting Back in 45 Seconds");
                Thread.Sleep(1000 * 45);
            }
            if (running && !_running)
            {
                Logger.Log("Starting back...");
                startStopApp(false);
            }
        }


        private void SendMail(string title, string msg)
        {
            System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();
            message.To.Add("jason.coc.player.1@gmail.com");
            message.Subject = title;
            message.From = new System.Net.Mail.MailAddress("jason.coc.player.1@gmail.com");
            message.Body = "This is the message body";
            System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient("yoursmtphost");
            smtp.Send(message);
        }

    }
}
