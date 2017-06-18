using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDDNListener
{
    public class HandAuthoredSectorCollection : List<HandAuthoredSector>
    {
        public HandAuthoredSectorCollection() { }
        public HandAuthoredSectorCollection(IEnumerable<HandAuthoredSector> sectors) : base(sectors) { }

        public void Add(string name, double x, double y, double z, double radius, bool permitlocked = false, double x0 = Double.NaN, double y0 = Double.NaN, double z0 = Double.NaN)
        {
            base.Add(new HandAuthoredSector
            {
                name = name,
                X = x,
                Y = y,
                Z = z,
                Radius = radius,
                PermitLocked = permitlocked,
                X0 = double.IsNaN(x0) ? (x - radius) : x0,
                Y0 = double.IsNaN(y0) ? (y - radius) : y0,
                Z0 = double.IsNaN(z0) ? (z - radius) : z0,
            });
        }

        public HandAuthoredSector FindSector(Vector3 pos)
        {
            foreach (HandAuthoredSector sector in this)
            {
                if (sector.Contains(pos))
                {
                    return sector;
                }
            }

            return null;
        }
    }
}
