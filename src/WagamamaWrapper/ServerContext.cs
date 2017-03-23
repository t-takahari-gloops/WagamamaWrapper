using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Disquuun;
using System.Text;

namespace WagamamaWrapper
{
    public class ServerContext<TContext, TRequestModel, TPushModel>
    {

        private const int ConnectionIdLength = 36;

        private const char HeaderString = 's';
        private const char HeaderBinary = 'b';
        private const char HeaderControl = 'c';

        private const char StateConnect = '1';
        private const char StateStringMessage = '2';
        private const char StateBinaryMessage = '3';
        private const char StateDisconnectIntent = '4';
        private const char StateDisconnectAccidt = '5';

        private readonly Func<TContext, TRequestModel[], TPushModel[]> _onUpdate;
        private readonly Disquuun.Disquuun _disquuun;
        private readonly Lazy<Timer> _timer;
        private readonly long _intervalMilliseconds;
        private readonly ISerializer _serializer;
        private readonly ConcurrentDictionary<string, IList<TRequestModel>> _requests;
        private readonly ConcurrentDictionary<string, IList<TPushModel>> _responses;

        private static readonly LockObject _lockObject = new LockObject();
        private static readonly ConcurrentDictionary<string, string> _roomIdsByConectionId = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, string> _userIdsByConnectionId = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, TContext> _battleContextsByRoomId = new ConcurrentDictionary<string, TContext>();

        public Action<string, Exception> OnFailDisqueConnection { get; set; }

        public Action<Exception> OnFailUpdate { get; set; }

        public ServerContext(
            Func<TContext, TRequestModel[], TPushModel[]> onUpdate,
            long intervalMilliseconds,
            string host,
            int port,
            ISerializer serializer)
        {
            _requests             = new ConcurrentDictionary<string, IList<TRequestModel>>();
            _responses            = new ConcurrentDictionary<string, IList<TPushModel>>();
            _intervalMilliseconds = intervalMilliseconds;
            _onUpdate             = onUpdate;
            _disquuun             = new Disquuun.Disquuun(host, port, 1024, 3, OnDisqueConnect);
            _timer                = new Lazy<Timer>(() => new Timer(OnTimerElapsed, _lockObject, Timeout.Infinite, 0));
            _serializer           = serializer;
        }

        public void Begin()
        {
            _timer.Value.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(_intervalMilliseconds));
        }

        public void End()
        {
            if (_timer.IsValueCreated)
            {
                _timer?.Value.Dispose();
            }

            _disquuun?.Disconnect();
        }

        private void OnConnected(string connectionId)
        {
            // ユーザーID取得してほげもげする
        }

        private void OnMessage(string connectionId, byte[] data)
        {
            var requestModel = _serializer.Deserialize<TRequestModel>(data);

            var roomId = _roomIdsByConectionId[connectionId];

            _requests.AddOrUpdate(
                roomId,
                key => new List<TRequestModel> { requestModel },
                (key, list) =>
                {
                    list.Add(requestModel);

                    return list;
                });
        }

        private void OnUpdate()
        {
            var results = _requests
                .AsParallel()
                .Select(r =>
                {
                    var context = _battleContextsByRoomId[r.Key];

                    var pushModels = _onUpdate(context, r.Value.ToArray());

                    _responses.AddOrUpdate(
                        r.Key,
                        key => pushModels,
                        (key, models) => pushModels);

                    return 0;
                })
                .ToArray();

            foreach (var responses in _responses)
            {
                var data = _serializer.Serialize(responses.Value);

                _disquuun.AddJob(responses.Key, data);
            }

            _requests.Clear();
            _responses.Clear();
        }

        private void OnDisconnected(string connectionId, string reason)
        {
            // 何しよ？
        }

        private class LockObject
        {
            public bool IsLocked { get; set; }
        }

        // コンストラクタが長くてアレなので private で切り出し
        private void OnTimerElapsed(object state)
        {
            var lockObj = (LockObject)state;

            if (!lockObj.IsLocked)
            {
                lockObj.IsLocked = true;

                try
                {
                    OnUpdate();
                }
                catch (Exception ex)
                {
                    OnFailUpdate?.Invoke(ex);
                }
            }

            lockObj.IsLocked = false;
        }

        private void OnDisqueConnect(string disqueId)
        {
            _disquuun
                .GetJob(new[] { disqueId }, "count", 100)
                .Loop((command, results) =>
                {
                    var jobs = DisquuunDeserializer.GetJob(results);

                    var jobIds  = jobs.Select(x => x.jobId).ToArray();
                    var jobData = jobs.Select(x => x.jobData).ToArray();

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

                                    //OnMessage(connectionId, message);
                                }
                                break;
                            case StateBinaryMessage:
                                if (2 + ConnectionIdLength < length)
                                {
                                    var dataLength = length - (2 + ConnectionIdLength);
                                    var bytes = new byte[dataLength];

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
        }
    }
}