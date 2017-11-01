using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDProcGen
{
    public class HandAuthoredSectorCollection : List<HandAuthoredSector>
    {
        private Dictionary<string, List<HandAuthoredSector>> SectorsByName = new Dictionary<string, List<HandAuthoredSector>>(StringComparer.InvariantCultureIgnoreCase);

        public HandAuthoredSectorCollection() { }
        public HandAuthoredSectorCollection(IEnumerable<HandAuthoredSector> sectors) : base(sectors)
        {
            foreach (HandAuthoredSector sector in sectors)
            {
                if (!SectorsByName.ContainsKey(sector.name))
                {
                    SectorsByName[sector.name] = new List<HandAuthoredSector>();
                }

                SectorsByName[sector.name].Add(sector);
            }
        }

        public void Add(string name, double x, double y, double z, double radius, bool permitlocked = false, double x0 = Double.NaN, double y0 = Double.NaN, double z0 = Double.NaN)
        {
            HandAuthoredSector sector = new HandAuthoredSector
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
            };

            base.Add(sector);

            if (!SectorsByName.ContainsKey(name))
            {
                SectorsByName[name] = new List<HandAuthoredSector>();
            }

            SectorsByName[name].Add(sector);
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

        public HandAuthoredSector[] FindSector(string name)
        {
            return SectorsByName.ContainsKey(name) ? SectorsByName[name].ToArray() : null;
        }
    }
}
