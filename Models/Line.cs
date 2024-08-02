using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public class Line
    {
        private Vector3 start;
        private Vector3 end;
        private float flow;
        public Vector3 Start { get => start; set => start = value; }
        public Vector3 End { get => end; set => end = value; }
        public float Flow { get => flow; set => flow = value; }
        public Line(Vector3 start, Vector3 end, float flow)
        {
            this.Start = start;
            this.End = end;
            this.Flow = flow;
        }

        public Line()
        {
        }
    }
}
