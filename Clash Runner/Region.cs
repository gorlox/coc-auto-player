using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clash_Runner
{
    public class Region
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int H { get; set; }
        public int W { get; set; }
        public float Similar { get; set; }
        public int CenterX { get { return X + (W / 2);  } }
        public int CenterY { get { return Y + (H / 2); } }

        public override string ToString()
        {
            return string.Format("Region{{{0}, {1}, {2}, {3}}}", X, Y, W, H);
        }
    }
}
