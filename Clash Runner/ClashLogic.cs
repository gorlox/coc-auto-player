using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Clash_Runner
{
    public static class ClashLogic
    {
        #region DLL Import

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hWnd, ref RECT Rect);

        #endregion
        #region Private Variables

        static Task _task;
        static CancellationTokenSource tokenSource;
        static CancellationToken token;
        static Process _clash;
        static readonly string ImageDir = ConfigurationManager.AppSettings["ImageDir"];
        static Region _clashDementions = new Region();

        #endregion
        #region Logic Variables

        static int _accountIndex = -1;
        static int[] _accountList = new int[] { 1, 2, 3, 4, 5, 1, 6, 7, 8, 9, 10 };
        /*
        static int[] _accountList = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1, 11, 12, 13, 14, 15, 16, 17, 18, 19,
                                                1, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
                                                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39,
                                                1, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49 };

        static int[] _accountList = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1, 2, 3, 11, 12, 13, 14, 1, 2, 3, 15, 16, 17, 18, 19,
                                                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1, 2, 3, 20, 21, 22, 23, 1, 2, 3, 24, 25, 26, 27, 28, 29,
                                                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1, 2, 3, 30, 31, 32, 33, 1, 2, 3, 34, 35, 36, 37, 38, 39,
                                                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1, 2, 3, 40, 41, 42, 43, 1, 2, 3, 44, 45, 46, 47, 48, 49 };
        */
        #endregion

        public static void Start()
        {
            Logger.Log("Starting...");
            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;
            _task = Task.Factory.StartNew( () => RunClash());
        }

        public static void Stop()
        {
            Logger.Log("Stopping...");
            Task local = new Task(StopThread);
            local.Start();
        }

        private static void StopThread()
        {
            tokenSource.Cancel();
            try
            {
                /*
                try
                {
                    _clash.Kill();
                }
                catch { }
                var p = GetClashProcess();
                if (p != null)
                    p.Kill();
                */
            }
            catch
            {
                Logger.Log("Error closing clash process");
            }
            while (!_task.IsCompleted)
                Thread.Sleep(500);
            Logger.Log("Process Stopped");
        }

        private static Process GetClashProcess()
        {
            foreach (var p in Process.GetProcesses())
            {
                if (p.ProcessName == ConfigurationManager.AppSettings["ClashApp"])
                    return p;
            }
            return null;
        }

        public static bool RestartClash(bool killFirst)
        {
            try
            {
                Process p = null;
                if (killFirst)
                {
                    try
                    {
                        _clash.Kill();
                    }
                    catch { }
                    while ((p = GetClashProcess()) != null)
                        p.Kill();
                    Thread.Sleep(500);
                }
            }
            catch
            {
                Logger.Log("Error closing clash process");
            }
            Logger.Log("Starting Clash App");
            _clash = new Process
            {
                StartInfo =
                {
                    FileName = ConfigurationManager.AppSettings["ClashAppDir"],
                    CreateNoWindow = true,
                    UseShellExecute = false,
                }
            };
            try
            {
                Process p = null;
                var r = Screen.PrimaryScreen.Bounds;
                _clashDementions = new Region();

                while (p == null || _clashDementions.W == 0 || _clashDementions.H == 0)
                {
                    _clash.Start();
                    Sleep(10000);

                    p = GetClashProcess();
                    if (p != null)
                    {
                        RECT output = new RECT();
                        var handle = p.MainWindowHandle;
                        GetWindowRect(handle, ref output);
                        _clashDementions.X = output.left;
                        _clashDementions.Y = output.top;
                        _clashDementions.W = output.right - output.left;
                        _clashDementions.H = output.bottom - output.top;
                    }
                }
            }
            catch
            {
                Logger.Log("Error starting Clash App Process");
            }
            return false;
        }

        private static void RunClash()
        {
            OpenCVWrapper cv = new OpenCVWrapper();
            cv.ImageDir = ImageDir;
            RestartClash(true);
            cv.FindRegion = _clashDementions;
            cv.CancelToken = tokenSource;
            LaunchApp(cv, false);
            while (!tokenSource.IsCancellationRequested)
            {
                var account = GetNextAccount();
                LaunchAccount(cv, account);
                PreProcess(cv, account);
                for (int i = 0; i < 9; i++)
                {
                    if (tokenSource.IsCancellationRequested)
                        break;
                    if (i == 0)
                        cv.Scroll(_clashDementions, -1500, -1500);
                    else if (i % 3 == 0)
                        cv.Scroll(_clashDementions, -1500, 550);
                    else
                        cv.Scroll(_clashDementions, 550, 0);
                    Sleep(500);
                    SectionProcess(cv, account, i);
                }
                PostProcess(cv, account);
            }
        }

        private static void Sleep(int time)
        {
            if (tokenSource.IsCancellationRequested)
                return;
            Thread.Sleep(time);
        }

        private static int GetNextAccount()
        {
            if (++_accountIndex >= _accountList.Length)
                _accountIndex = 0;
            return _accountList[_accountIndex];
        }


        private static void LaunchApp(OpenCVWrapper sikuli, bool restartApp)
        {
            Logger.Log("Launching App - " + restartApp);
            Region icon = null;
            while (!tokenSource.IsCancellationRequested && icon == null)
            {
                Region reg = null;
                while (!tokenSource.IsCancellationRequested && reg == null)
                {
                    if (restartApp)
                    {
                        RestartClash(true);
                        sikuli.FindRegion = _clashDementions;
                    }
                    reg = sikuli.Wait("AppLaunch/App_Icon.png", 0.7f, 45000);
                    if (reg == null)
                    {
                        Logger.Log("Error finding start app icon...trying agin");
                        restartApp = true;
                        continue;
                    }
                }
                for (var x = 0; x < 30; x++)
                {
                    if (tokenSource.IsCancellationRequested)
                        return;
                    Thread.Sleep(100);
                }
                sikuli.Click(reg);
                if (!sikuli.WaitVanish("AppLaunch/App_Icon.png", 0.7f, 3000))
                    sikuli.Click(reg);
                icon = sikuli.Wait("AppLaunch/builder_icon.png", 0.7f, 90000);
                if (icon == null)
                {
                    Logger.Log("Error finding builder icon...trying again");
                    restartApp = true;
                    continue;
                }
                sikuli.Click("AppLaunch/okay_button.png", 0.7f);
                sikuli.WaitClick("AppLaunch/cancel_button.png", 0.7f, 500);
            }
        }


        private static void LaunchAccount(OpenCVWrapper sikuli, int account)
        {
            Logger.Log("Launching Account - " + account);
            sikuli.WaitClick("LaunchAccount/settings_icon.png", 0.7f, 1000);
            if (tokenSource.IsCancellationRequested)
                return;
            if (sikuli.Wait("LaunchAccount/exit_icon.png", 0.7f, 10000) == null)
            {
                sikuli.Click("LaunchAccount/settings_icon.png", 0.7f);
                sikuli.Wait("LaunchAccount/exit_icon.png", 0.7f, 10000);
            }
            if (tokenSource.IsCancellationRequested)
                return;
            sikuli.Click("LaunchAccount/settings_tab.png", 0.7f);
            if (sikuli.WaitClick("LaunchAccount/connected_button.png", 0.7f, 1000))
                sikuli.WaitVanish("LaunchAccount/connected_button.png", 0.7f, 10000);
            if (tokenSource.IsCancellationRequested)
                return;
            for (int i = 0; i < 10; i++)
            {
                if (tokenSource.IsCancellationRequested)
                    return;
                sikuli.Click("LaunchAccount/disconnected_button.png", 0.7f);
                sikuli.Wait("LaunchAccount/disconnected_button.png", 0.7f, 1000);
                if (sikuli.WaitVanish("LaunchAccount/disconnected_button.png", 0.7f, 5000))
                    break;

            }
            if (sikuli.Wait("LaunchAccount/choose_account_label.png", 0.7f, 30000) == null)
            {
                if (tokenSource.IsCancellationRequested)
                    return;
                LaunchApp(sikuli, true);
                LaunchAccount(sikuli, account);
                return;
            }
            Sleep(3000);
            int pgCt = account / 8 + 1;
            int down = account % 8;
            for (int i = 0; i < pgCt; i++)
            {
                if (tokenSource.IsCancellationRequested)
                    return;
                sikuli.Type("{PGDN}");
                Sleep(200);
            }
            for (int i = 0; i < down; i++)
            {
                if (tokenSource.IsCancellationRequested)
                    return;
                sikuli.Type("{DOWN}");
                Sleep(200);
            }
            Sleep(200);
            sikuli.Type("{ENTER}");
            for (int i = 0; i < 30; i++)
            {
                if (tokenSource.IsCancellationRequested)
                    return;
                if (sikuli.Exists("LaunchAccount/connected_button.png", 0.7f) || sikuli.Exists("LaunchAccount/load_button.png", 0.7f))
                    break;
                Sleep(1000);
            }
            if (account != 0 && sikuli.Exists("LaunchAccount/jason_village.png", 0.7f))
            {
                if (tokenSource.IsCancellationRequested)
                    return;
                LaunchApp(sikuli, true);
                LaunchAccount(sikuli, account);
                return;
            }
            if (sikuli.Exists("LaunchAccount/connected_button.png", 0.7f))
            {
                sikuli.Click("LaunchAccount/exit_icon.png", 0.7f);
                if (tokenSource.IsCancellationRequested)
                    return;
            }
            else if (sikuli.Exists("LaunchAccount/load_button.png", 0.7f))
            {
                if (sikuli.Click("LaunchAccount/load_button.png", 0.7f) && sikuli.WaitClick("LaunchAccount/confirm_input_box.png", 0.8f, 5000))
                {
                    Sleep(500);
                    sikuli.Type("CONFIRM");
                    Sleep(1000);
                    sikuli.Click("LaunchAccount/okay_button_small.png", 0.7f);
                    if (!sikuli.WaitVanish("LaunchAccount/okay_button_small.png", 0.7f, 1000))
                        sikuli.Click("LaunchAccount/okay_button_small.png", 0.7f);
                }
                sikuli.WaitVanish("LaunchAccount/builder_icon.png", 0.7f, 5000);
                sikuli.Wait("LaunchAccount/builder_icon.png", 0.7f, 90000);
                Sleep(1000);
                if (tokenSource.IsCancellationRequested)
                    return;
            }
            else
            {
                //App hung on changing account - try again
                if (tokenSource.IsCancellationRequested)
                    return;
                sikuli.Click("LaunchAccount/exit_icon.png", 0.7f);
                if (sikuli.Wait("AppLaunch/builder_icon.png", 0.7f, 5000) == null)
                    LaunchApp(sikuli, true);
                LaunchAccount(sikuli, account);
                return;
            }
            sikuli.Click("LaunchAccount/okay_button.png", 0.7f);
        }


        private static void PreProcess(OpenCVWrapper cv, int account)
        {
            if (account == 1)
                DonateTroops(cv, "giant", "giant", "giant", "giant", "giant", "giant", "giant", "poison");
        }

        private static void PostProcess(OpenCVWrapper cv, int account)
        {
            if (account > 10)
                UpgradeNextBuilding(cv);
        }

        private static void SectionProcess(OpenCVWrapper s, int account, int iteration)
        {
            CollectResources(s);
            RequestTroops(s, account);
            if (account == 0)
                return;
            RemoveTrees(s, account);
        }


        private static void CollectResources(OpenCVWrapper s)
        {
            s.ClickAll("Resources/gold.png", 0.5f, 100);
            s.ClickAll("Resources/elixer.png", 0.7f, 100);
            s.ClickAll("Resources/dark_elixer.png", 0.7f, 100);
            s.Click("Resources/grave.png", 0.85f);
            s.Click("Resources/dark_grave.png", 0.85f);
            if (s.Click("Resources/cart.png", 0.7f))
                s.WaitClick("Resources/collect_button.png", 0.7f, 1000);
            SelectNone(s);
        }

        private static void RequestTroops(OpenCVWrapper s, int account)
        {
            if (!s.Click("RequestTroops/clan_castle.png", 0.7f))
                return;
            // Collect Treasure
            if (account != 0 && !s.WaitClick("RequestTroops/treasury_button.png", 0.6f, 1000) ||
                !s.WaitClick("RequestTroops/collect_button.png", 0.7f, 1000) ||
                !s.WaitClick("RequestTroops/okay_button.png", 0.7f, 1000))
                s.Click("RequestTroops/close_button.png", 0.7f);
            // Request Troops
            if (s.WaitClick("RequestTroops/request_button.png", 0.6f, 1000))
            {
                if (!s.WaitClick("RequestTroops/send_button.png", 0.7f, 1000))
                    s.Click("RequestTroops/big_close_button.png", 0.7f);
            }
            // Close any boxes
            SelectNone(s);
        }

        private static void SelectNone(OpenCVWrapper s)
        {
            s.Click("Common/close_button.png", 0.7f);
            s.Click("Common/big_close_button.png", 0.7f);
            Region lastRegion = s.FindRegion;
            s.FindRegion = new Region()
            {
                X = _clashDementions.X + 300,
                Y = _clashDementions.Y + 525,
                W = 450,
                H = 90
            };
            for (int i = 0; i < 3; i++)
            {
                if (!s.Exists("Common/button_bottom.png", 0.7f))
                    break;
                s.Click(new Region()
                {
                    X = 500,
                    Y = 300,
                    W = 3,
                    H = 3
                });
                Thread.Sleep(750);
            }
            s.FindRegion = lastRegion;
        }

        private static int GetBuilderCount(OpenCVWrapper cv)
        {
            Region lastRegion = cv.FindRegion;
            int ct = 0;
            cv.FindRegion = new Region()
            {
                X = _clashDementions.X + 370,
                Y = _clashDementions.Y,
                W = 60,
                H = 85
            };
            if (cv.Exists("BuilderCount/5.png", 1, 0.7f))
                ct = 5;
            else if (cv.Exists("BuilderCount/4.png", 1, 0.7f))
                ct = 4;
            else if (cv.Exists("BuilderCount/3.png", 1, 0.7f))
                ct = 3;
            else if (cv.Exists("BuilderCount/2.png", 1, 0.7f))
                ct = 2;
            else if (cv.Exists("BuilderCount/1.png", 1, 0.7f))
                ct = 1;
            cv.FindRegion = lastRegion;
            return ct;
        }

        private static void RemoveTrees(OpenCVWrapper cv, int count)
        {
            if (count == 0)
                return;
            // To save time - only one tree is removed per call to function
            float[] values = new float[] { 0.7f, 0.8f, 0.85f, 0.7f, 0.65f, 0.7f, 0.7f };
            if (GetBuilderCount(cv) <= 0)
                return;
            for (int i = 0; i < values.Length; i++)
            {
                if (!cv.Click("Trees/" + i + ".png", values[i]))
                    continue;
                if (cv.WaitClick("Trees/remove_button.png", 0.7f, 750))
                    break;
            }
            SelectNone(cv);
        }

        private static void DonateTroops(OpenCVWrapper cv, params string[] troops)
        {
            // Donate troops if account 1 or 2
            cv.WaitClick("Common/chat_open_button.png", 0.7f, 500);
            Sleep(500);
            Region reg = null;
            for (int x = 0; x < 50; x++)
            {
                reg = cv.Wait("Donate/jason_request.png", 0.85f, 250);
                if (reg != null)
                    break;
                Region lastRegion = cv.FindRegion;
                cv.FindRegion = new Region()
                {
                    X = _clashDementions.X,
                    Y = _clashDementions.Y,
                    W = _clashDementions.W,
                    H = 300
                };
                bool found = cv.WaitClick("Donate/next_request_button.png", 0.7f, 500);
                cv.FindRegion = lastRegion;
                if (!found)
                    break;
            }

            if (reg != null)
            {
                // Sleep to let scrolling stop (if needed)
                Sleep(1000);
                reg = cv.Wait("Donate/jason_request.png", 0.85f, 250);
                if (reg != null)
                {
                    // Click the donate button
                    Region lastSearch = cv.FindRegion;
                    cv.FindRegion = new Region()
                    {
                        X = reg.X,
                        Y = reg.Y,
                        W = reg.W + 200,
                        H = reg.H + 100
                    };
                    bool clickOccured = cv.WaitClick("Donate/donate_button.png", 0.7f, 500);
                    cv.FindRegion = lastSearch;
                    if (clickOccured)
                    {
                        foreach (string troop in troops)
                            cv.WaitClick(string.Format("Donate/donate_{0}_button.png", troop), 0.7f, 500);
                    }
                    else // Quit
                        reg = null;
                }
            }

            // Quit if no request
            if (reg == null)
            {
                cv.WaitClick("Donate/donate_close_button.png", 0.6f, 500);
                cv.WaitClick("Common/chat_close_button.png", 0.7f, 500);
                SelectNone(cv);
                return;
            }

            // Finally, close the chat
            cv.WaitClick("Donate/donate_close_button.png", 0.6f, 500);
            cv.WaitClick("Common/chat_close_button.png", 0.7f, 500);
            // Build More Troops
            cv.WaitClick("Donate/create_troops_button.png", 0.7f, 500);
            cv.WaitClick("Donate/quick_train_tab.png", 0.7f, 1000);
            cv.Wait("Donate/train_button.png", 0.8f, 500);
            cv.ClickAll("Donate/train_button.png", 0.8f, 200);

            // Close window
            SelectNone(cv);
        }

        private static void UpgradeNextBuilding(OpenCVWrapper cv)
        {
            if (GetBuilderCount(cv) == 0)
                return;
            if (!cv.WaitClick("UpgradeBuilding/builder_icon.png", 0.7f, 500))
                return;
            if (!(cv.WaitClick("UpgradeBuilding/elixer.png", 1, 0.85f, 1000) ||
                cv.WaitClick("UpgradeBuilding/gold.png", 1, 0.85f, 500)))
                return;
            Sleep(1000);
            if (!cv.WaitClick("UpgradeBuilding/upgrade_button.png", 0.6f, 1000))
            {
                SelectNone(cv);
                return;
            }
            Sleep(500);
            cv.WaitClick("UpgradeBuilding/button_begin.png", 0.7f, 1000);
            cv.Click("Common/close_button.png", 0.7f);
            SelectNone(cv);
        }
    }
}
