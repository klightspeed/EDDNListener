using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDDNListener
{
    public class HandAuthoredSector
    {
        public string name;
        public double X;
        public double Y;
        public double Z;
        public double Radius;
        public bool PermitLocked;
        public double X0;
        public double Y0;
        public double Z0;

        public bool Contains(Vector3 pos)
        {
            double dx = pos.X - X;
            double dy = pos.Y - Y;
            double dz = pos.Z - Z;
            return (dx * dx + dy * dy + dz * dz) < (Radius * Radius);
        }

        public int[] GetBaseBlockCoords(int starclass)
        {
            int mult = 10 * (1 << (7 - starclass));
            return new int[] { (int)((X0 + 39 * 1280 + 65) / mult), (int)((Y0 + 32 * 1280 + 25) / mult), (int)((Z0 + 19 * 1280 - 215) / mult) };
        }

        public ByteXYZ GetBlockCoords(PGStarMatch m)
        {
            int starclass = m.StarClass;
            int[] v0 = GetBaseBlockCoords(starclass);
            int[] v = m.BlockCoords;
            int[] bc = new int[] { v[0] - v0[0], v[1] - v0[1], v[2] - v0[2] };
            if (bc[0] < 0 || bc[0] >= 128 || bc[1] < 0 || bc[1] >= 128 || bc[2] < 0 || bc[2] >= 128)
            {
                return ByteXYZ.Invalid;
            }
            else
            {
                ByteXYZ blockcoords = new ByteXYZ { X = (sbyte)bc[0], Y = (sbyte)bc[1], Z = (sbyte)bc[2] };
                string suffix = m.GetPgSuffix(blockcoords, m.StarClass, m.StarSeq);

                return blockcoords;
            }
        }
    }
}
