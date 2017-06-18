using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDDNListener
{
    [System.Diagnostics.DebuggerDisplay("({X},{Y},{Z})")]
    public struct UShortXYZ : IComparable<UShortXYZ>
    {
        public ushort X;
        public ushort Y;
        public ushort Z;

        public long ord
        {
            get
            {
                return X + (Y * 0x000010000L) + (Z * 0x100000000L);
            }
        }

        public UShortXYZ(ushort x, ushort y, ushort z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int CompareTo(UShortXYZ other)
        {
            return ord.CompareTo(other.ord);
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj is UShortXYZ && ord.Equals(((UShortXYZ)obj).ord);
        }

        public override int GetHashCode()
        {
            return ord.GetHashCode();
        }

        public static bool operator ==(UShortXYZ left, UShortXYZ right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UShortXYZ left, UShortXYZ right)
        {
            return !left.Equals(right);
        }

        public static readonly UShortXYZ Invalid = new UShortXYZ(65535, 65535, 65535);
    }
}
