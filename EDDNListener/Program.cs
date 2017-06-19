using System;
using System.Collections.Generic;
using System.IO;
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
            string basedir = args.Length > 0 ? args[0] : ".";
            uint structsize = SizeOf<PGStarMatch>();
            PGStarMatch sm = new PGStarMatch();
            PGStarMatch.LoadProcGenSectorsJson(Path.Combine(basedir, "ProcGen.json"));
            PGStarMatch.LoadNamedSystemsJson(Path.Combine(basedir, "edsystems-all-withcoords.json"));
            PGStarMatch.LoadEdsmSystemsJson(Path.Combine(basedir, "systemsWithCoordinates.json"));
            PGStarMatch.SaveProcGenSectorsJson(Path.Combine(basedir, "ProcGen-new.json"));
            PGStarMatch.LoadEddbSystemsCsv(Path.Combine(basedir, "systems.csv"));
            Listener listener = new Listener();
            listener.Run();
        }
    }
}
