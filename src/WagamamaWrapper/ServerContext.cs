using System;
using System.IO;
using System.Linq;
using System.Text;
using Disquuun;

namespace WagamamaWrapper
{
    public interface IServerContext : IDisposable
    {
        void OnConnected(string connectionId);

        void OnUpdate();

        void OnMessage(string connectionId, string data);

        void OnMessage(string connectionId, byte[] data);

        void OnPublish(string[] connectionIds, byte[] data);

        void OnDisconnected(string connectionId, string reason = "");
    }

    public class DefaultServerContext : IServerContext
    {
        private readonly Disquuun.Disquuun _disquuun;
        private readonly ISerializer _serializer;

        private const int ConnectionIdLength = 36;

        private const char HeaderString = 's';
        private const char HeaderBinary = 'b';
        private const char HeaderControl = 'c';

        private const char StateConnect = '1';
        private const char StateStringMessage = '2';
        private const char StateBinaryMessage = '3';
        private const char StateDisconnectIntent = '4';
        private const char StateDisconnectAccidt = '5';

        public DefaultServerContext(
            string host,
            int port,
            string contextQueueIdentity,
            ISerializer serializer,
            Action<Exception> connectionOpenErrorHandler = null)
        {
            _serializer = serializer;
            _disquuun   = new Disquuun.Disquuun(
                host,
                port,
                1024,
                3,
                _ =>
                {
                    _disquuun
                        .GetJob(new[] { contextQueueIdentity }, "count", 1000)
                        .Loop((command, data) =>
                        {
                            var jobs = DisquuunDeserializer.GetJob(data);

                            var jobIds  = jobs.Select(x => x.jobId).ToArray();
                            var jobData = jobs.Select(x => x.jobData).ToArray();

                            _disquuun.FastAck(jobIds).Async((c, d) => { });

                            foreach (var job in jobData)
                            {
                                var length = job.Length;

                                if (length < 1 + 1 + ConnectionIdLength)
                                {
                                    continue;
                                }

                                var header = (char)job[0];

                                switch (header)
                                {
                                    case HeaderString:
                                    case HeaderBinary:
                                    case HeaderControl:
                                        break;
                                    default:
                                        continue;
                                }

                                var state = (char)job[1];

                                var connectionId = Encoding.ASCII.GetString(job, 2, ConnectionIdLength);

                                switch (state)
                                {
                                    case StateConnect:
                                        OnConnected(connectionId);
                                        break;
                                    case StateStringMessage:
                                        if (2 + ConnectionIdLength < length)
                                        {
                                            var message = Encoding.UTF8.GetString(job, 2 + ConnectionIdLength, length - (2 + ConnectionIdLength));

                                            OnMessage(connectionId, message);
                                        }
                                        break;
                                    case StateBinaryMessage:
                                        if (2 + ConnectionIdLength < length)
                                        {
                                            var dataLength = length - (2 + ConnectionIdLength);
                                            var bytes      = new byte[dataLength];

                                            Buffer.BlockCopy(job, 2 + ConnectionIdLength, bytes, 0, dataLength);

                                            OnMessage(connectionId, bytes);
                                        }
                                        break;
                                    case StateDisconnectIntent:
                                        OnDisconnected(connectionId, "intentional disconnect.");
                                        break;
                                    case StateDisconnectAccidt:
                                        OnDisconnected(connectionId, "accidential disconnect.");
                                        break;
                                    default:
                                        break;
                                }

                            }

                            return true;
                        });
                },
                (reason, ex) =>
                {
                    _disquuun?.Disconnect();

                    connectionOpenErrorHandler?.Invoke(ex);
                });
        }

        public void OnConnected(string connectionId)
        {
            OnPublish(new[] { connectionId }, Encoding.UTF8.GetBytes("hello!"));
        }

        public void OnUpdate()
        {
        }

        public void OnMessage(string connectionId, string data)
        {
            OnMessage(connectionId, Encoding.UTF8.GetBytes(data));
        }

        public void OnMessage(string connectionId, byte[] data)
        {
            var message = _serializer.Deserialize<string>(data) ?? Encoding.UTF8.GetString(data);

            var pushMessage = $"{connectionId} sends '{message}'";

            var pushData = _serializer.Serialize(pushMessage);

            OnPublish(new[] { connectionId }, pushData);
        }

        public void OnPublish(string[] connectionIds, byte[] data)
        {
            foreach (var connectionId in connectionIds)
            {
                _disquuun.AddJob(connectionId, data).Async((c, d) => { });
            }
        }

        public void OnDisconnected(string connectionId, string reason = "")
        {
        }

        ~DefaultServerContext()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _disquuun.Disconnect();
            }
        }
    }
}