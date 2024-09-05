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
        private bool travel;
        public Vector3 Start { get => start; set => start = value; }
        public Vector3 End { get => end; set => end = value; }
        public float Flow { get => flow; set => flow = value; }
        public bool Travel { get => travel; set => travel = value; }

        public Line(Vector3 start, Vector3 end, float flow, bool travel)
        {
            this.Start = start;
            this.End = end;
            this.Flow = flow;
            this.travel = travel;
        }

        public Line()
        {
        }

        public double Length()
        {
            return (new Vector3(end.X, end.Y,0) - new Vector3(start.X, start.Y,0)).Length();
        }

        public double Distance(Line other)
        {
            return Math.Min(Math.Min((other.start-start).Length(),(other.end-end).Length()),Math.Min((other.start - end).Length(),(other.end-start).Length()));
        }

        public Line Flip()
        {
            return new Line(end, start, flow, travel);
        }
    }
}
