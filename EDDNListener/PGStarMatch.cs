using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;

namespace EDDNListener
{
    [System.Diagnostics.DebuggerDisplay("{Id}: {Name}")]
    public struct PGStarMatch
    {
        #region Lookup tables
        private static Dictionary<ByteXYZ, string> ProcGenSectorByCoords = new Dictionary<ByteXYZ, string>();
        private static Dictionary<string, ByteXYZ> ProcGenSectorByName = new Dictionary<string, ByteXYZ>(StringComparer.InvariantCultureIgnoreCase);
        private static Dictionary<string, List<long>> SystemsByName = new Dictionary<string, List<long>>(StringComparer.InvariantCultureIgnoreCase);
        private static Dictionary<long, string> IdToName = new Dictionary<long, string>();
        private static Dictionary<ByteXYZ, long[]> NamedSystemsBySector = new Dictionary<ByteXYZ, long[]>();
        private static Dictionary<ByteXYZ, string[]> SystemNamesBySector = new Dictionary<ByteXYZ, string[]>();
        private static Dictionary<long, PGStarMatch> SystemsById = new Dictionary<long, PGStarMatch>();
        private static long[] EdsmIdToSystemId = new long[0];

        public static readonly PGStarMatch Invalid = new PGStarMatch
        {
            _RelCoordsX = 0xFFFF,
            _RelCoordsY = 0xFFFF,
            _RelCoordsZ = 0xFFFF,
            _RegionCoordsX = -128,
            _RegionCoordsY = -128,
            _RegionCoordsZ = -128,
            _StarClass = 0xFF,
            _StarSeq = 0xFFFF,
            _HandAuthoredSectorIndex = 0
        };
        #endregion

        #region Fields
        private sbyte _RegionCoordsZ;
        private sbyte _RegionCoordsY;
        private sbyte _RegionCoordsX;
        private byte _StarClass;
        private ushort _RelCoordsZ;
        private ushort _RelCoordsY;
        private ushort _RelCoordsX;
        private ushort _StarSeq;
        private ushort _HandAuthoredSectorIndex;
        private ushort _NameIndexInSector;
        private uint _EdsmId;
        private uint _EddbId;
        #endregion

        #region Properties

        public Vector3 Coords
        {
            get
            {
                if (RegionCoords == ByteXYZ.Invalid || RegionRelCoords == UShortXYZ.Invalid)
                {
                    return new Vector3 { X = Double.NaN, Y = Double.NaN, Z = Double.NaN };
                }
                else
                {
                    return new Vector3 {
                        X = _RegionCoordsX * 1280.0 + _RelCoordsX * 0.03125 - 49985,
                        Y = _RegionCoordsY * 1280.0 + _RelCoordsY * 0.03125 - 40985,
                        Z = _RegionCoordsZ * 1280.0 + _RelCoordsZ * 0.03125 - 24105
                    };
                }
            }
        }

        public long Id
        {
            get
            {
                return GetProcGenId(RegionCoords, RegionRelCoords, _StarClass, _StarSeq);
            }
        }

        public string Name
        {
            get
            {
                ByteXYZ region = RegionCoords;
                if (_NameIndexInSector > 0 && SystemNamesBySector.ContainsKey(region) && _NameIndexInSector <= SystemNamesBySector[region].Length)
                {
                    return SystemNamesBySector[region][_NameIndexInSector - 1];
                }

                long id = Id;
                if (IdToName.ContainsKey(id))
                {
                    return IdToName[id];
                }
                else
                {
                    return HPGName;
                }
            }
        }

        public HandAuthoredSector HASector { get { return _HandAuthoredSectorIndex <= 0 ? null : HandAuthoredSectors.Sectors[_HandAuthoredSectorIndex - 1]; } }

        public int[] BlockCoords
        {
            get
            {
                int blocksize = 40960 >> StarClass;
                return new int[]
                {
                    (_RegionCoordsX * 40960 + _RelCoordsX) / blocksize,
                    (_RegionCoordsY * 40960 + _RelCoordsY) / blocksize,
                    (_RegionCoordsZ * 40960 + _RelCoordsZ) / blocksize
                };
            }
        }

        public ByteXYZ RegionCoords
        {
            get
            {
                return new ByteXYZ { X = _RegionCoordsX, Y = _RegionCoordsY, Z = _RegionCoordsZ };
            }
        }

        public UShortXYZ RegionRelCoords
        {
            get
            {
                return new UShortXYZ { X = _RelCoordsX, Y = _RelCoordsY, Z = _RelCoordsZ };
            }
        }

        public ByteXYZ RelBlockCoords
        {
            get
            {
                int blocksize = 40960 >> StarClass;
                return new ByteXYZ((sbyte)(_RelCoordsX / blocksize), (sbyte)(_RelCoordsY / blocksize), (sbyte)(_RelCoordsZ / blocksize));
            }
        }

        public int StarClass
        {
            get
            {
                return _StarClass;
            }
        }

        public int StarSeq
        {
            get
            {
                return _StarSeq;
            }
        }

        public string PGBlockSuffix
        {
            get
            {
                return GetPgSuffix(RelBlockCoords, StarClass);
            }
        }

        public string PGSuffix
        {
            get
            {
                return GetPgSuffix(RelBlockCoords, StarClass, StarSeq);
            }
        }

        public string PGRegion
        {
            get
            {
                if (ProcGenSectorByCoords.ContainsKey(this.RegionCoords))
                {
                    return ProcGenSectorByCoords[this.RegionCoords];
                }
                else
                {
                    return null;
                }
            }
        }

        public string PGBlock
        {
            get
            {
                string regionname = PGRegion;

                if (regionname == null)
                {
                    return $"{RegionCoords} {PGBlockSuffix}";
                }
                else
                {
                    return $"{regionname} {PGBlockSuffix}";
                }
            }
        }

        public string PGName
        {
            get
            {
                string regionname = PGRegion;

                if (regionname == null)
                {
                    return $"{RegionCoords} {PGSuffix}";
                }
                else
                {
                    return $"{regionname} {PGSuffix}";
                }
            }
        }

        public string HPGName
        {
            get
            {
                HandAuthoredSector sector = HASector;

                if (HASector != null)
                {
                    ByteXYZ blockcoords = sector.GetBlockCoords(this);

                    if (blockcoords != ByteXYZ.Invalid)
                    {
                        string pgsuffix = GetPgSuffix(blockcoords, StarClass, StarSeq);
                        return $"{sector.name} {pgsuffix}";
                    }
                }

                return PGName;
            }
        }

        #endregion

        #region Instance Methods
        public string GetPgSuffix(ByteXYZ blockcoords, int starclass)
        {
            int blocknr = blockcoords.X + ((int)blockcoords.Y << 7) + ((int)blockcoords.Z << 14);
            char v1 = (char)((blocknr % 26) + 'A');
            blocknr /= 26;
            char v2 = (char)((blocknr % 26) + 'A');
            blocknr /= 26;
            char v3 = (char)((blocknr % 26) + 'A');
            blocknr /= 26;
            char sc = (char)('h' - starclass);
            string v4 = blocknr == 0 ? "" : $"{blocknr}-";
            return $"{v1}{v2}-{v3} {sc}{v4}";
        }

        public string GetPgSuffix(ByteXYZ blockcoords, int starclass, int starseq)
        {
            string pgbsuffix = GetPgSuffix(blockcoords, starclass);
            return $"{pgbsuffix}{starseq}";
        }
        #endregion

        #region Static Methods
        public static ByteXYZ RegionCoordsFromId(long id)
        {
            sbyte x, y, z;
            ulong _id = (ulong)id;
            int _starclass = 7 - (int)(_id & 7);
            _id >>= _starclass + 3;
            z = (sbyte)((int)(_id & 127) - 19);
            _id >>= _starclass + 7;
            y = (sbyte)((int)(_id & 63) - 32);
            _id >>= _starclass + 6;
            x = (sbyte)((int)(_id & 127) - 39);
            return new ByteXYZ(x, y, z);
        }

        public static ByteXYZ BlockCoordsFromId(long id)
        {
            sbyte x, y, z;
            ulong _id = (ulong)id;
            int _starclass = 7 - (int)(_id & 7);
            _id >>= 3;
            z = (sbyte)(_id & ((1UL << _starclass) - 1));
            _id >>= _starclass + 7;
            y = (sbyte)(_id & ((1UL << _starclass) - 1));
            _id >>= _starclass + 6;
            x = (sbyte)(_id & ((1UL << _starclass) - 1));
            return new ByteXYZ(x, y, z);
        }

        public static int StarSeqFromId(long id)
        {
            ulong _id = (ulong)id;
            int _starclass = 7 - (int)(_id & 7);
            return (int)(id >> (23 + _starclass * 3));
        }

        public static int StarClassFromId(long id)
        {
            ulong _id = (ulong)id;
            return 7 - (int)(_id & 7);
        }

        public static long GetProcGenId(int[] coords, int starclass, int starseq)
        {
            return (7 - (long)starclass) |
                    ((long)coords[2] << 3) |
                    ((long)coords[1] << (10 + starclass)) |
                    ((long)coords[0] << (16 + starclass * 2)) |
                    ((long)starseq << (23 + starclass * 3));
        }

        public static long GetProcGenId(ByteXYZ regionCoords, UShortXYZ relCoords, int starclass, int starseq)
        {
            int blocksize = 40960 >> starclass;
            int[] coords = new int[]
            {
                (int)(regionCoords.X * 40960 + relCoords.X) / blocksize,
                (int)(regionCoords.Y * 40960 + relCoords.Y) / blocksize,
                (int)(regionCoords.Z * 40960 + relCoords.Z) / blocksize
            };
            return GetProcGenId(coords, starclass, starseq);
        }

        public static string GetProcGenRegionNameFromSystemName(string s, out int index, out int starclass, ref ByteXYZ blkcoords)
        {
            int i = s.Length - 1;
            int blknum = 0;
            index = 0;
            starclass = 0;

            string _s = s.ToLowerInvariant();

            if (i < 9) return null;                                    // a bc-d e0
            if (_s[i] < '0' || _s[i] > '9') return null;               // cepheus dark region a sector xy-z a1-[0]
            while (i > 8 && _s[i] >= '0' && _s[i] <= '9') i--;
            if (i < _s.Length - 6) return null;
            index = Int32.Parse(_s.Substring(i + 1));
            if (_s[i] == '-')                                          // cepheus dark region a sector xy-z a1[-]0
            {
                i--;
                int vend = i;
                while (i > 8 && _s[i] >= '0' && _s[i] <= '9') i--;     // cepheus dark region a sector xy-z a[1]-0
                if (i < vend - 4) return null;
                blknum = Int32.Parse(_s.Substring(i + 1, vend - i));
            }
            if (_s[i] < 'a' || _s[i] > 'h') return null;               // cepheus dark region a sector xy-z [a]1-0
            starclass = 'h' - _s[i];
            i--;
            if (_s[i] != ' ') return null;                             // cepheus dark region a sector xy-z[ ]a1-0
            i--;
            if (_s[i] < 'a' || _s[i] > 'z') return null;               // cepheus dark region a sector xy-[z] a1-0
            blknum = blknum * 26 + _s[i] - 'a';
            i--;
            if (_s[i] != '-') return null;                             // cepheus dark region a sector xy[-]z a1-0
            i--;
            if (_s[i] < 'a' || _s[i] > 'z') return null;               // cepheus dark region a sector x[y]-z a1-0
            blknum = blknum * 26 + _s[i] - 'a';
            i--;
            if (_s[i] < 'a' || _s[i] > 'z') return null;               // cepheus dark region a sector [x]y-z a1-0
            blknum = blknum * 26 + _s[i] - 'a';
            i--;
            if (_s[i] != ' ') return null;                             // cepheus dark region a sector[ ]xy-z a1-0
            i--;
            blkcoords = new ByteXYZ(
                (sbyte)(blknum & 127),
                (sbyte)((blknum >> 7) & 127),
                (sbyte)((blknum >> 14) & 127)
            );
            return s.Substring(0, i + 1);                                 // [cepheus dark region a sector] xy-z a1-0
        }

        public static PGStarMatch[] GetStarMatchByName(string sysname)
        {
            if (SystemsByName.ContainsKey(sysname))
            {
                return SystemsByName[sysname].Select(i => SystemsById[i]).ToArray();
            }
            else
            {
                int index = 0;
                int starclass = 0;
                ByteXYZ blkcoords = new ByteXYZ();
                int[] coords = null;
                HandAuthoredSector sector = null;

                string regionname = GetProcGenRegionNameFromSystemName(sysname, out index, out starclass, ref blkcoords);

                if (regionname != null)
                {
                    if (ProcGenSectorByName.ContainsKey(regionname))
                    {
                        ByteXYZ regioncoords = ProcGenSectorByName[regionname];
                        coords = new int[]
                        {
                            (regioncoords.X << starclass) + blkcoords.X,
                            (regioncoords.Y << starclass) + blkcoords.Y,
                            (regioncoords.Z << starclass) + blkcoords.Z
                        };
                    }
                    else
                    {
                        sector = HandAuthoredSectors.Sectors.FindSector(regionname)?.First();
                        if (sector != null)
                        {
                            int[] basecoords = sector.GetBaseBlockCoords(starclass);
                            coords = new int[]
                            {
                                basecoords[0] + blkcoords.X,
                                basecoords[1] + blkcoords.Y,
                                basecoords[2] + blkcoords.Z
                            };
                        }
                    }

                    if (coords != null)
                    {
                        long id = GetProcGenId(coords, starclass, index);

                        if (SystemsById.ContainsKey(id))
                        {
                            return new PGStarMatch[] { SystemsById[id] };
                        }
                    }
                }
            }

            return new PGStarMatch[0];
        }

        public static PGStarMatch GetStarMatch(string sysname, Vector3 starpos, uint edsmid = 0, uint eddbid = 0)
        {
            int index = 0;
            int starclass = 0;
            ByteXYZ blkcoords = new ByteXYZ();
            int[] coords = null;
            HandAuthoredSector sector = null;

            if (SystemsByName.ContainsKey(sysname))
            {
                List<PGStarMatch> matches = SystemsByName[sysname].Select(i => SystemsById[i]).ToList();

                if (matches.Count == 1)
                {
                    return matches[0];
                }

                foreach (PGStarMatch sm in matches)
                {
                    Vector3 smcoords = sm.Coords;
                    Vector3 diff = new Vector3 { X = smcoords.X - starpos.X, Y = smcoords.Y - starpos.Y, Z = smcoords.Z - starpos.Z };
                    double sqdist = diff.X * diff.X + diff.Y * diff.Y + diff.Z * diff.Z;
                    if (sqdist < 0.015625)
                    {
                        sector = sm.HASector;
                        return sm;
                    }
                }
            }

            string regionname = GetProcGenRegionNameFromSystemName(sysname, out index, out starclass, ref blkcoords);

            int blocksize = 40960 >> starclass;

            double vx = starpos.X + 49985;
            double vy = starpos.Y + 40985;
            double vz = starpos.Z + 24105;

            if (vx < 0 || vx >= 163840 || vy < 0 || vy > 163840 || vz < 0 || vz > 163840)
            {
                return PGStarMatch.Invalid;
            }

            int x = (int)(vx * 32 + 0.5);
            int y = (int)(vy * 32 + 0.5);
            int z = (int)(vz * 32 + 0.5);

            int cx = x / blocksize;
            int cy = y / blocksize;
            int cz = z / blocksize;

            if (regionname != null && starpos != null && !Double.IsNaN(starpos.X) && !Double.IsNaN(starpos.Y) && !Double.IsNaN(starpos.Z))
            {
                if (ProcGenSectorByName.ContainsKey(regionname))
                {
                    ByteXYZ regioncoords = ProcGenSectorByName[regionname];
                    coords = new int[]
                    {
                        (regioncoords.X << starclass) + blkcoords.X,
                        (regioncoords.Y << starclass) + blkcoords.Y,
                        (regioncoords.Z << starclass) + blkcoords.Z
                    };
                }
                else
                {
                    sector = HandAuthoredSectors.Sectors.FindSector(regionname)?.First();
                    if (sector != null)
                    {
                        int[] basecoords = sector.GetBaseBlockCoords(starclass);
                        coords = new int[]
                        {
                            basecoords[0] + blkcoords.X,
                            basecoords[1] + blkcoords.Y,
                            basecoords[2] + blkcoords.Z
                        };
                    }
                    else
                    {
                        int bx = (x % 40960) / blocksize;
                        int by = (y % 40960) / blocksize;
                        int bz = (z % 40960) / blocksize;

                        if (bx == blkcoords.X && by == blkcoords.Y && bz == blkcoords.Z)
                        {
                            ByteXYZ regioncoords = new ByteXYZ { X = (sbyte)(x / 40960), Y = (sbyte)(y / 40960), Z = (sbyte)(z / 40960) };

                            Console.WriteLine($"New region: {regionname} @ {regioncoords}");

                            ProcGenSectorByCoords[regioncoords] = regionname;
                            ProcGenSectorByName[regionname] = regioncoords;

                            coords = new int[] { cx, cy, cz };
                        }
                    }
                }

                if (coords == null)
                {
                    return PGStarMatch.Invalid;
                }

                int ix = coords[0];
                int iy = coords[1];
                int iz = coords[2];

                if (cx != ix || cy != iy || cz != iz)
                {
                    return PGStarMatch.Invalid;
                }

                long id = GetProcGenId(coords, starclass, index);

                if (SystemsById.ContainsKey(id))
                {
                    PGStarMatch sm = SystemsById[id];

                    if ((eddbid == 0 && edsmid != 0 && sm._EdsmId == 0) || (sm._EdsmId == edsmid && eddbid != 0 && sm._EddbId == 0))
                    {
                        if (eddbid == 0 && edsmid != 0 && sm._EdsmId == 0)
                        {
                            sm._EdsmId = edsmid;
                        }

                        if (sm._EdsmId == edsmid && eddbid != 0 && sm._EddbId == 0)
                        {
                            sm._EddbId = eddbid;
                        }

                        SystemsById[id] = sm;
                    }

                    return sm;
                }
                else
                {
                    PGStarMatch sm = new PGStarMatch
                    {
                        _RegionCoordsX = (sbyte)(x / 40960),
                        _RegionCoordsY = (sbyte)(y / 40960),
                        _RegionCoordsZ = (sbyte)(z / 40960),
                        _RelCoordsX = (ushort)(x % 40960),
                        _RelCoordsY = (ushort)(y % 40960),
                        _RelCoordsZ = (ushort)(z % 40960),
                        _StarSeq = (ushort)index,
                        _StarClass = (byte)starclass,
                        _HandAuthoredSectorIndex = (ushort)(HandAuthoredSectors.Sectors.IndexOf(sector) + 1),
                        _EdsmId = edsmid,
                        _EddbId = eddbid
                    };

                    SystemsById[id] = sm;
                    return sm;
                }
            }
            else
            {
                return PGStarMatch.Invalid;
            }
        }

        public static long GetProcGenId(string sysname, Vector3 starpos, out HandAuthoredSector sector)
        {
            int index = 0;
            int starclass = 0;
            ByteXYZ blkcoords = new ByteXYZ();
            int[] coords = null;
            sector = null;

            string regionname = GetProcGenRegionNameFromSystemName(sysname, out index, out starclass, ref blkcoords);

            if (regionname == null)
            {
                if (SystemsByName.ContainsKey(sysname))
                {
                    List<PGStarMatch> matches = SystemsByName[sysname].Select(s => SystemsById[s]).ToList();
                    foreach (PGStarMatch sm in matches)
                    {
                        Vector3 smcoords = sm.Coords;
                        Vector3 diff = new Vector3 { X = smcoords.X - starpos.X, Y = smcoords.Y - starpos.Y, Z = smcoords.Z - starpos.Z };
                        double sqdist = diff.X * diff.X + diff.Y * diff.Y + diff.Z * diff.Z;
                        if (sqdist < 0.015625)
                        {
                            sector = sm.HASector;
                            return sm.Id;
                        }
                    }
                }
                else
                {
                    return 0;
                }
            }
            else if (ProcGenSectorByName.ContainsKey(regionname))
            {
                ByteXYZ regioncoords = ProcGenSectorByName[regionname];
                coords = new int[]
                {
                    (regioncoords.X << starclass) + blkcoords.X,
                    (regioncoords.Y << starclass) + blkcoords.Y,
                    (regioncoords.Z << starclass) + blkcoords.Z
                };
            }
            else
            {
                sector = HandAuthoredSectors.Sectors.FirstOrDefault(s => s.name == regionname);
                if (sector != null)
                {
                    int[] basecoords = sector.GetBaseBlockCoords(starclass);
                    coords = new int[]
                    {
                        basecoords[0] + blkcoords.X,
                        basecoords[1] + blkcoords.Y,
                        basecoords[2] + blkcoords.Z
                    };
                }
            }

            if (coords != null)
            {
                long id = GetProcGenId(coords, starclass, index);

                if (id < 0)
                {
                    Console.WriteLine($"Error: {sysname} => {id} ([{coords[0]},{coords[1]},{coords[2]}]:{starclass}:{index})");
                }

                return id;
            }
            else
            {
                return 0;
            }
        }

        public static void LoadProcGenSectorsJson(string filename)
        {
            Console.WriteLine($"Loading procgen sectors from {filename}");
            using (Stream s = File.OpenRead(filename))
            {
                using (TextReader r = new StreamReader(s))
                {
                    using (JsonReader rdr = new JsonTextReader(r))
                    {
                        JArray ja = JArray.Load(rdr);
                        PGStarMatch.ProcGenSectorByCoords = ja.ToDictionary(jt => new ByteXYZ((sbyte)(jt.Value<sbyte>("x") + 39), (sbyte)(jt.Value<sbyte>("y") + 32), (sbyte)(jt.Value<sbyte>("z") + 19)), jt => jt.Value<string>("name"));
                        PGStarMatch.ProcGenSectorByName = ProcGenSectorByCoords.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
                    }
                }
            }
            Console.WriteLine("Done");
        }

        public static void SaveProcGenSectorsJson(string filename)
        {
            File.Delete(filename + ".tmp");
            using (Stream s = File.OpenWrite(filename + ".tmp"))
            {
                using (TextWriter w = new StreamWriter(s))
                {
                    using (JsonTextWriter writer = new JsonTextWriter(w))
                    {
                        writer.Formatting = Formatting.Indented;
                        writer.WriteStartArray();
                        foreach (KeyValuePair<ByteXYZ, string> kvp in ProcGenSectorByCoords)
                        {
                            writer.WriteStartObject();
                            writer.Formatting = Formatting.None;
                            writer.WritePropertyName("name");
                            writer.WriteValue(kvp.Value);
                            writer.WritePropertyName("x");
                            writer.WriteValue(kvp.Key.X - 39);
                            writer.WritePropertyName("y");
                            writer.WriteValue(kvp.Key.Y - 32);
                            writer.WritePropertyName("z");
                            writer.WriteValue(kvp.Key.Z - 19);
                            writer.WriteEndObject();
                            writer.Formatting = Formatting.Indented;
                        }
                        writer.WriteEndArray();
                    }
                }
            }

            File.Delete(filename);
            File.Move(filename + ".tmp", filename);
        }

        public static void LoadNamedSystemsJson(string filename)
        {
            Console.WriteLine($"Loading named systems from {filename}");
            using (Stream s = File.OpenRead(filename))
            {
                using (TextReader r = new StreamReader(s))
                {
                    using (JsonReader rdr = new JsonTextReader(r))
                    {
                        if (rdr.Read() && rdr.TokenType == JsonToken.StartArray)
                        {
                            int i = 0;

                            while (rdr.Read() && rdr.TokenType == JsonToken.StartObject)
                            {
                                JObject jo = JObject.Load(rdr);
                                long id = jo.Value<long>("id");
                                string pgname = jo.Value<string>("pgname");
                                string name = jo.Value<string>("name");
                                JArray ca = (JArray)jo["coords"];

                                if (name != null && ca != null)
                                {
                                    Vector3 coords = new Vector3 { X = ca[0].Value<double>(), Y = ca[1].Value<double>(), Z = ca[2].Value<double>() };
                                    IdToName[id] = name;
                                    PGStarMatch sm = GetStarMatch(pgname, coords);
                                    if (!SystemsById.ContainsKey(sm.Id))
                                    {
                                        SystemsById[sm.Id] = sm;
                                    }

                                    if (!SystemsByName.ContainsKey(name))
                                    {
                                        SystemsByName[name] = new List<long>();
                                    }

                                    SystemsByName[name].Add(sm.Id);
                                }

                                i++;
                                if (i % 10000 == 0)
                                {
                                    Console.Write(".");
                                    if (i % 500000 == 0)
                                    {
                                        Console.WriteLine("");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            NamedSystemsBySector = SystemsById.Values.GroupBy(s => s.RegionCoords).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Select(v => v.Id).ToArray());

            foreach (KeyValuePair<ByteXYZ, long[]> regionsys_kvp in NamedSystemsBySector.ToList())
            {
                SystemNamesBySector[regionsys_kvp.Key] = new string[regionsys_kvp.Value.Length];
                for (int i = 0; i < regionsys_kvp.Value.Length; i++)
                {
                    long id = regionsys_kvp.Value[i];
                    PGStarMatch sys = SystemsById[id];
                    SystemNamesBySector[regionsys_kvp.Key][i] = IdToName[sys.Id];
                    sys._NameIndexInSector = (ushort)(i + 1);
                    SystemsById[id] = sys;
                }
            }

            Console.WriteLine("Done");
        }

        public static void LoadEdsmSystemsJson(string filename)
        {
            Console.WriteLine($"Loading EDSM systems from {filename}");
            using (Stream s = File.OpenRead(filename))
            {
                using (TextReader r = new StreamReader(s))
                {
                    using (JsonReader rdr = new JsonTextReader(r))
                    {
                        if (rdr.Read() && rdr.TokenType == JsonToken.StartArray)
                        {
                            int i = 0;

                            while (rdr.Read() && rdr.TokenType == JsonToken.StartObject)
                            {
                                JObject jo = JObject.Load(rdr);
                                uint edsmid = jo.Value<uint>("id");
                                string name = jo.Value<string>("name");
                                JObject co = (JObject)jo["coords"];
                                if (co != null)
                                {
                                    Vector3 starpos = new Vector3 { X = co.Value<double>("x"), Y = co.Value<double>("y"), Z = co.Value<double>("z") };
                                    PGStarMatch sm = GetStarMatch(name, starpos, edsmid: edsmid);

                                    if (sm.RegionCoords == ByteXYZ.Invalid || sm.RegionRelCoords == UShortXYZ.Invalid)
                                    {
                                        Console.WriteLine($"Bad EDSM System: id={edsmid} name=\"{name}\" coords={starpos}");
                                    }
                                    else
                                    {
                                        if (EdsmIdToSystemId.Length <= edsmid)
                                        {
                                            Array.Resize(ref EdsmIdToSystemId, (int)edsmid + 100000);
                                        }

                                        EdsmIdToSystemId[edsmid] = sm.Id;
                                    }
                                }

                                i++;
                                if (i % 10000 == 0)
                                {
                                    Console.Write(".");
                                    if (i % 500000 == 0)
                                    {
                                        Console.WriteLine("");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Console.WriteLine("Done");
        }

        public static void LoadEddbSystemsCsv(string filename)
        {
            Console.WriteLine($"Loading EDDB systems from {filename}");

            using (Stream s = File.OpenRead(filename))
            {
                using (TextReader r = new StreamReader(s))
                {
                    using (CsvParser p = new CsvParser(r))
                    {
                        List<string> headers = p.Read().ToList();
                        int eddbidcol = headers.IndexOf("id");
                        int edsmidcol = headers.IndexOf("edsm_id");
                        int namecol = headers.IndexOf("name");
                        int xcol = headers.IndexOf("x");
                        int ycol = headers.IndexOf("y");
                        int zcol = headers.IndexOf("z");
                        int i = 0;
                        string[] fields;

                        while ((fields = p.Read()) != null)
                        {
                            uint edsmid;
                            uint eddbid;

                            if (UInt32.TryParse(fields[eddbidcol], out eddbid) && UInt32.TryParse(fields[edsmidcol], out edsmid))
                            {
                                if (edsmid < EdsmIdToSystemId.Length && edsmid != 0)
                                {
                                    long id = EdsmIdToSystemId[edsmid];
                                    PGStarMatch sm = SystemsById[id];
                                    sm._EddbId = eddbid;
                                    SystemsById[id] = sm;
                                }
                            }

                            i++;
                            if (i % 10000 == 0)
                            {
                                Console.Write(".");
                                if (i % 500000 == 0)
                                {
                                    Console.WriteLine("");
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine("Done");
        }

        #endregion
    }
}
