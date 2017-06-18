using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDDNListener
{
    public class Vector3 : IComparable<Vector3>
    {
        public double X;
        public double Y;
        public double Z;

        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }

        public bool Equals(Vector3 other)
        {
            return other != null && this.X == other.X && this.Y == other.Y && this.Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as Vector3);
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
        }

        public int CompareTo(Vector3 other)
        {
            int res = this.Z.CompareTo(other.Z);

            if (res == 0)
            {
                res = this.Y.CompareTo(other.Y);
            }

            if (res == 0)
            {
                res = this.X.CompareTo(other.X);
            }

            return res;
        }
    }
}
