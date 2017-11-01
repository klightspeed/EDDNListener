using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDProcGen
{
    [System.Diagnostics.DebuggerDisplay("({X},{Y},{Z})")]
    public struct ByteXYZ : IComparable<ByteXYZ>
    {
        public sbyte X;
        public sbyte Y;
        public sbyte Z;

        public int ord
        {
            get
            {
                return X + (Y * 128) + (Z * 16384);
            }
        }

        public ByteXYZ(sbyte x, sbyte y, sbyte z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"({X},{Y},{Z})";
        }

        public int CompareTo(ByteXYZ other)
        {
            return ord.CompareTo(other.ord);
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj is ByteXYZ && ord.Equals(((ByteXYZ)obj).ord);
        }

        public override int GetHashCode()
        {
            return ord.GetHashCode();
        }

        public static bool operator==(ByteXYZ left, ByteXYZ right)
        {
            return left.Equals(right);
        }

        public static bool operator!=(ByteXYZ left, ByteXYZ right)
        {
            return !left.Equals(right);
        }

        public static readonly ByteXYZ Invalid = new ByteXYZ(-128, -128, -128);
    }
}
