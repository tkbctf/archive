using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miocat
{
    class Program
    {
        public static readonly int DefaultPort = 8080;

        static void Main(string[] args)
        {
            int port;
            if (args.Length == 0 || !int.TryParse(args[0], out port))
                port = DefaultPort;
            Console.WriteLine("Starting server");
            var server = new HttpServer(port);
            server.Start();
            Console.WriteLine("Listening on port " + port);
            Console.ReadKey();
            Console.WriteLine("Shutting down...");
        }
    }
}
