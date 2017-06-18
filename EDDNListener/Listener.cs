using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ionic.Zlib;

namespace EDDNListener
{
    public class Listener
    {
        public void Run()
        {
            try
            {
                while (true)
                {
                    using (SubscriberSocket sock = new SubscriberSocket())
                    {
                        sock.Connect("tcp://eddn-relay.elite-markets.net:9500");
                        sock.SubscribeToAnyTopic();

                        Msg msg = new Msg();
                        msg.InitEmpty();

                        while (sock.TryReceive(ref msg, TimeSpan.FromMinutes(10)))
                        {
                            Process(msg);
                            msg.Close();
                            msg.InitEmpty();
                        }
                    }
                }
            }
            finally
            {
                NetMQConfig.Cleanup();
            }
        }

        private void Process(Msg msg)
        {
            JObject jo;

            using (ZlibStream ds = new ZlibStream(new MemoryStream(msg.Data), CompressionMode.Decompress))
            {
                using (JsonReader rdr = new JsonTextReader(new StreamReader(ds, Encoding.UTF8)))
                {
                    jo = JObject.Load(rdr);
                }
            }

            Process(jo);
        }

        private void Process(JObject jo)
        {
            if (jo.Value<string>("$schemaRef") == "http://schemas.elite-markets.net/eddn/journal/1")
            {
                JObject header = jo["header"] as JObject;
                JObject body = jo["message"] as JObject;
                ProcessJournal(header, body);
            }
        }

        private void ProcessJournal(JObject header, JObject body)
        {
            if (body != null)
            {
                string evt = body.Value<string>("event");

                if (evt == "Scan")
                {
                    ProcessScan(header, body);
                }
                else if (evt == "FSDJump")
                {
                    ProcessFSDJump(header, body);
                }
                else if (evt == "Docked")
                {
                    ProcessDocked(header, body);
                }
            }
        }

        private void ProcessDocked(JObject header, JObject body)
        {

        }

        private void ProcessScan(JObject header, JObject body)
        {
        }

        private void ProcessFSDJump(JObject header, JObject body)
        {
            string sysname = body.Value<string>("StarSystem");
            JArray ca = (JArray)body["StarPos"];
            Vector3 syspos = new Vector3 { X = ca[0].Value<double>(), Y = ca[1].Value<double>(), Z = ca[2].Value<double>() };
            PGStarMatch sm = PGStarMatch.GetStarMatch(sysname, syspos);

            if (sm.Name != sysname)
            {
                Console.WriteLine($"Unknown system {sysname} received at {syspos}");
            }
        }
    }
}
