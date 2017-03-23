using System;

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

            var server = new ServerContext<IBattleContext, IRequestModel, IPushModel>(
                (context, requests) => context.Execute(requests),
                intervalMilliseconds,
                "172.21.1.24",
                7711,
                new Serializer());

            server.Begin();

            Console.ReadLine();

            server.End();
        }
    }

    public interface IBattleContext
    {
        IPushModel[] Execute(IRequestModel[] requests);
    }

    public interface IRequestModel { }

    public interface IPushModel { }

    public class Serializer : ISerializer
    {
        public byte[] Serialize<T>(T value)
        {
            throw new NotImplementedException();
        }

        public T Deserialize<T>(byte[] bytes)
        {
            throw new NotImplementedException();
        }
    }
}
