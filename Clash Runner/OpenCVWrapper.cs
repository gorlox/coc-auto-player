using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Clash_Runner
{
    public class OpenCVWrapper
    {
        private const int DEFAULT_SEARCH_DEPTH = 2;

        #region Constructor / Properties

        public CancellationTokenSource CancelToken { get; set; }

        public string ImageDir { get; set; }

        public Region FindRegion { get; set; }

        #endregion

        #region DLL Imports

        [Flags]
        public enum MouseEventFlags
        {
            LeftDown = 0x00000002,
            LeftUp = 0x00000004,
            MiddleDown = 0x00000020,
            MiddleUp = 0x00000040,
            Move = 0x00000001,
            Absolute = 0x00008000,
            RightDown = 0x00000008,
            RightUp = 0x00000010
        }

        [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out MousePoint lpMousePoint);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [StructLayout(LayoutKind.Sequential)]
        public struct MousePoint
        {
            public int X;
            public int Y;

            public MousePoint(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        #endregion

        #region Private Helper Functions

        private bool IsCancel()
        {
            if (CancelToken != null && CancelToken.IsCancellationRequested)
                return true;
            return false;
        }

        private string GetImagePath(string image)
        {
            return (ImageDir ?? string.Empty).Trim() + image;
        }

        #endregion  

        public void SetCursorPosition(int X, int Y)
        {
            SetCursorPos(X, Y);
        }

        public MousePoint GetCursorPosition()
        {
            MousePoint currentMousePoint;
            var gotPoint = GetCursorPos(out currentMousePoint);
            if (!gotPoint) { currentMousePoint = new MousePoint(0, 0); }
            return currentMousePoint;
        }

        public void MouseEvent(MouseEventFlags value)
        {
            MousePoint position = GetCursorPosition();

            mouse_event
                ((int)value,
                 position.X,
                 position.Y,
                 0,
                 0)
                ;
        }

        public void Type(string text)
        {
            SendKeys.SendWait(text);
        }

        public void Click(int x, int y)
        {
            SetCursorPosition(x, y);
            Thread.Sleep(10);
            MouseEvent(MouseEventFlags.LeftDown);
            Thread.Sleep(50);
            MouseEvent(MouseEventFlags.LeftUp);
        }

        public bool Click(Region reg)
        {
            if (reg == null)
                return false;
            if (!IsCancel())
                Click(reg.CenterX, reg.CenterY);
            return true;
        }

        public bool Click(string image, float similar)
        {
            return Click(image, DEFAULT_SEARCH_DEPTH, similar);
        }

        public bool Click(string image, int searchDepth, float similar)
        {
            Logger.Log(string.Format("Click: {0}, {1}", image, similar));
            image = GetImagePath(image);
            var vals = FindImage(image, searchDepth, similar);
            if (vals == null || vals.Count == 0)
            {
                Logger.Log("--> False");
                return false;
            }
            var pos = vals[0];
            if (!IsCancel())
                Click(pos.CenterX, pos.CenterY);
            Logger.Log("--> True");
            return true;
        }

        public bool ClickAll(string image, float similar, int pause)
        {
            return ClickAll(image, DEFAULT_SEARCH_DEPTH, similar, pause);
        }

        public bool ClickAll(string image, int searchDepth, float similar, int pause)
        {
            Logger.Log(string.Format("ClickAll: {0}, {1}, {2}", image, similar, pause));
            image = GetImagePath(image);
            var vals = FindImage(image, searchDepth, similar);
            if (vals == null || vals.Count == 0)
            {
                Logger.Log("--> False");
                return false;
            }
            foreach (var pos in vals)
            {
                if (!IsCancel())
                    Click(pos.CenterX, pos.CenterY);
                Thread.Sleep(pause);
            }
            Logger.Log("--> True");
            return true;
        }

        public Region Wait(string image, float similar, int wait)
        {
            return Wait(image, DEFAULT_SEARCH_DEPTH, similar, wait);
        }

        public Region Wait(string image, int searchDepth, float similar, int wait)
        {
            Logger.Log(string.Format("Wait: {0}, {1}, {2}", image, similar, wait));
            image = GetImagePath(image);
            DateTime start = DateTime.Now;
            while (true)
            {
                var vals = FindImage(image, searchDepth, similar);
                if (vals != null && vals.Count > 0)
                {
                    Logger.Log("--> " + vals[0].ToString());
                    return vals[0];
                }
                var span = DateTime.Now - start;
                if (IsCancel() || (wait > 0 && span.TotalMilliseconds >= wait))
                {
                    Logger.Log("--> Null");
                    return null;
                }
                Thread.Sleep(50);
            }
        }

        public bool WaitClick(string image, float similar, int wait)
        {
            return WaitClick(image, DEFAULT_SEARCH_DEPTH, similar, wait);
        }

        public bool WaitClick(string image, int searchDepth, float similar, int wait)
        {
            Logger.Log(string.Format("WaitClick: {0}, {1}, {2}", image, similar, wait));
            Region r = Wait(image, searchDepth, similar, wait);
            return Click(r);
        }

        public bool Exists(string image, float similar)
        {
            return Exists(image, DEFAULT_SEARCH_DEPTH, similar);
        }

        public bool Exists(string image, int searchDepth, float similar)
        {
            Logger.Log(string.Format("Exists: {0}, {1}", image, similar));
            image = GetImagePath(image);
            var reg = FindImage(image, searchDepth, similar);
            Logger.Log("--> " + reg == null || reg.Count == 0 ? "False" : "True");
            return reg != null && reg.Count > 0;
        }

        public bool WaitVanish(string image, float similar, int time)
        {
            return WaitVanish(image, DEFAULT_SEARCH_DEPTH, similar, time);
        }

        public bool WaitVanish(string image, int searchDepth, float similar, int time)
        {
            Logger.Log(string.Format("WaitVanish: {0}, {1}, {2}", image, similar, time));
            if (!ValidateImage((ImageDir + image).Replace("\\", "\\\\")))
                return true;
            if (time <= 0)
            {
                while (Exists(image, searchDepth, similar))
                    Thread.Sleep(100);
                return true;
            }
            while (time > 0 && Exists(image, searchDepth, similar))
            {
                Thread.Sleep(100);
                time -= 100;
            }
            return time > 0;
        }

        public List<Region> Find(string image, float similar)
        {
            return Find(image, DEFAULT_SEARCH_DEPTH, similar);
        }

        public List<Region> Find(string image, int searchDepth, float similar)
        {
            Logger.Log(string.Format("Find: {0}, {1}", image, similar));
            image = GetImagePath(image);
            var reg = FindImage(image, searchDepth, similar);
            Logger.Log("--> " + reg == null ? "null" : reg.Count.ToString());
            return reg;
        }

        private List<Region> FindImage(string image, int searchDepth, float similar)
        {
            List<Region> ret = new List<Region>();
            Image<Bgr, byte>[] sourceLevels = null;
            Image<Bgr, byte>[] templateLevels = null;
            using (Image<Bgr, byte> source = new Image<Bgr, byte>(GetScreenShot(FindRegion)))
            {
                using (Image<Bgr, byte> template = new Image<Bgr, byte>(image))
                {
                    // Get template measurements
                    var templateWidth = template.Width;
                    var templateHeight = template.Height;
                    // Make sure tempate is smaller than source
                    if (templateWidth > source.Width || templateHeight > source.Height)
                        return ret;

                    sourceLevels = source.BuildPyramid(searchDepth);
                    templateLevels = template.BuildPyramid(searchDepth);

                    Image<Gray, float> _res = null;
                    for (int i = searchDepth; i >= 0; i--)
                    {
                        var _ref = sourceLevels[i];
                        var _tpl = templateLevels[i];
                        try
                        {
                            if (i == searchDepth)
                            {
                                _res = _ref.MatchTemplate(_tpl, Emgu.CV.CvEnum.TM_TYPE.CV_TM_CCOEFF_NORMED);
                            }
                            else
                            {
                                Contour<Point> _contours = null;
                                using (var _mask8u = _res.Convert<Gray, byte>())
                                    _contours = _mask8u.FindContours(Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_NONE, Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_EXTERNAL);
                                var _cList = GetCountourTree(_contours);
                                _res.Dispose();
                                _res = new Image<Gray, float>(_ref.Size + new Size(1, 1) - _tpl.Size);
                                foreach (var rl in _cList)
                                {
                                    Rectangle r = new Rectangle(rl.BoundingRectangle.X * 2, rl.BoundingRectangle.Y * 2, rl.BoundingRectangle.Width * 2, rl.BoundingRectangle.Height * 2);
                                    // Rectangle r = rl.BoundingRectangle;
                                    using (var _newRef = new Image<Bgr, byte>(_ref.Data))
                                    {
                                        var _newRefRoi = new Rectangle(r.Location, r.Size + (_tpl.Size - new Size(1, 1)));
                                        _newRef.ROI = _newRefRoi;
                                        try
                                        {
                                            var _tempRes = _newRef.MatchTemplate(_tpl, Emgu.CV.CvEnum.TM_TYPE.CV_TM_CCOEFF_NORMED);
                                            _res.ROI = r;
                                            _tempRes.CopyTo(_res);
                                            _res.ROI = Rectangle.Empty;
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Log(ex.Message);
                                            if (ex.InnerException != null)
                                                Logger.Log(ex.InnerException.Message);
                                        }
                                    }
                                }
                            }
                            var tmp = _res.ThresholdToZero(new Gray(similar));
                            _res.Dispose();
                            _res = tmp;
                        }
                        finally
                        {
                            _ref.Dispose();
                            _tpl.Dispose();
                        }
                    }

                    var cList = GetContourList(_res);
                    foreach (var c in cList)
                        ret.Add(new Region()
                        {
                            X = c.BoundingRectangle.X + (FindRegion == null ? 0 : FindRegion.X),
                            Y = c.BoundingRectangle.Y + (FindRegion == null ? 0 : FindRegion.Y),
                            W = templateWidth,
                            H = templateHeight
                        });
                }
            }
            return ret;
        }

        public void Scroll(Region region, int x, int y)
        {
            Logger.Log(string.Format("Scroll: {0}, {1}", x, y));
            int movex = x;
            int movey = y;
            int pauseBeforeScroll = 100; // Duration of pause before scroll
            int scrollTime = 250; // Duration it takes for the mouse to move to it's new position
            int pauseAfterScroll = 250; // Duration of pause after scroll

            while (!IsCancel() && (movex != 0 || movey != 0))
            {
                int nmx = 200;
                int nmy = 200;
                if (movex < 0)
                    nmx = -nmx;
                if (movey < 0)
                    nmy = -nmy;
                if ((nmx > movex && movex > 0) || (nmx < movex && movex < 0) || movex == 0)
                    nmx = movex;
                if ((nmy > movey && movey > 0) || (nmy < movey && nmy < 0) || movey == 0)
                    nmy = movey;
                if (IsCancel())
                    return;
                SetCursorPosition(region.CenterX, region.CenterY);
                MouseEvent(OpenCVWrapper.MouseEventFlags.LeftDown);
                Thread.Sleep(pauseBeforeScroll);
                double count = 0;
                DateTime time = DateTime.Now;
                while (!IsCancel() && count < scrollTime)
                {
                    count = (DateTime.Now - time).TotalMilliseconds;
                    if (count > scrollTime)
                        count = scrollTime;
                    double percent = count / scrollTime;
                    int nmx2 = Convert.ToInt32(Math.Round(nmx * percent));
                    int nmy2 = Convert.ToInt32(Math.Round(nmy * percent));
                    if (!IsCancel())
                        SetCursorPosition(region.CenterX - nmx2, region.CenterY - nmy2);
                }
                if (!IsCancel())
                    SetCursorPosition(region.CenterX - nmx, region.CenterY - nmy);
                Thread.Sleep(pauseAfterScroll);
                MouseEvent(OpenCVWrapper.MouseEventFlags.LeftUp);
                Thread.Sleep(50);
                movex = movex - nmx;
                movey = movey - nmy;
            }
        }

        private List<Contour<Point>> GetContourList(Image<Gray, float> image)
        {
            using (var img = image.Convert<Gray, byte>())
            {
                var r = img.FindContours(Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_NONE, Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_EXTERNAL);
                return GetCountourTree(r);
            }
        }

        

        private Bitmap GetScreenShot(Region reg)
        {
            if (reg == null || reg.W <= 0 || reg.H <= 0)
                reg = new Region()
                {
                    X = Screen.PrimaryScreen.Bounds.X,
                    Y = Screen.PrimaryScreen.Bounds.Y,
                    W = Screen.PrimaryScreen.Bounds.Width,
                    H = Screen.PrimaryScreen.Bounds.Height
                };
            var bmpScreenshot = new Bitmap(reg.W,
                               reg.H,
                               PixelFormat.Format32bppArgb);

            // Create a graphics object from the bitmap.
            var gfxScreenshot = Graphics.FromImage(bmpScreenshot);

            // Take the screenshot from the upper left corner to the right bottom corner.
            gfxScreenshot.CopyFromScreen(reg.X,
                                        reg.Y,
                                        0,
                                        0,
                                        new Size(reg.W, reg.H),
                                        CopyPixelOperation.SourceCopy);
            return bmpScreenshot;
        }

        private List<Contour<Point>> GetCountourTree(Contour<Point> val)
        {
            List<Contour<Point>> ret = new List<Contour<Point>>();
            if (val == null)
                return ret;
            ret.Add(val);
            ret.AddRange(GetCountourTree(val.HNext));
            ret.AddRange(GetCountourTree(val.VNext));
            return ret;
        }

        private bool ValidateImage(string image)
        {
            if (string.IsNullOrEmpty(image))
                return false;
            if (!File.Exists(image))
                return false;
            return true;
        }
    }
}
