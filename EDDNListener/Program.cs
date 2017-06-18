using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace EDDNListener
{
    class Program
    {
        static uint SizeOf<T>()
        {
            var meth = new DynamicMethod("GetManagedSizeImpl", typeof(uint), null, true);
            var gen = meth.GetILGenerator();
            gen.Emit(OpCodes.Sizeof, typeof(T));
            gen.Emit(OpCodes.Ret);
            var func = (Func<uint>)meth.CreateDelegate(typeof(Func<uint>));
            return func();
        }

        static void Main(string[] args)
        {
            uint structsize = SizeOf<PGStarMatch>();
            PGStarMatch sm = new PGStarMatch();
            PGStarMatch.LoadProcGenSectorsJson("ProcGen.json");
            PGStarMatch.LoadNamedSystemsJson("edsystems-all-withcoords.json");
            PGStarMatch.LoadEdsmSystemsJson("systemsWithCoords.json");
            PGStarMatch.LoadEddbSystemsCsv("systems.csv");
            Listener listener = new Listener();
            listener.Run();
        }
    }
}
