using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WagamamaWrapper
{
    public class Program
    {
        public static void Main(string[] args)
        {
            long intervalMilliseconds;

            if (args == null || args.Length < 1 || !long.TryParse(args[0], out intervalMilliseconds))
            {
                intervalMilliseconds = 15; // frame
            }

            var context = new DefaultServerContext(
                "172.24.212.21",
                7711,
                "wildcard_disque_sample_context",
                new SimpleMessageSerializer(),
                ex => Console.WriteLine(ex));

            var updater = new Updater(
                intervalMilliseconds,
                () => context.OnUpdate(),
                () => context.Dispose(),
                exception => Console.WriteLine(exception));

            updater.Begin();

            Console.ReadLine();

            updater.End();
        }
    }
}
