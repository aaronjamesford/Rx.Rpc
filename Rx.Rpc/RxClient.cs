using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rx.Rpc.Utils;

namespace Rx.Rpc
{
	public class RxClient : RealProxy
	{
		private class ResponseWrapper
		{
			public int SequenceNumber { get; set; }
			public string Content { get; set; }
		}

		private class RequestWrapper
		{
			public int SequenceNumber { get; set; }
			public object Content { get; set; }
		}

		private class ResultInfo
		{
			public Type ResultType { get; set; }
			public MethodInfo ObservableCast { get; set; }
		}

		private int _sequenceNumber = 0;
		private ConcurrentDictionary<string, ResultInfo> _resultInfo = new ConcurrentDictionary<string, ResultInfo> ();

		TcpClient _client;
		IObservable<ResponseWrapper> _reader;

		private RxClient(TcpClient client, Type t)
			: base(t)
		{
			_client = client;
			_reader = new StreamReader(_client.GetStream()).ToObservable(NetworkUtils.GetMaxMtu(), new EventLoopScheduler())
				.SelectMany(JsonUtils.SplitJsonObjects)
				.Select(str => {
					var j = JObject.Parse(str);
					return new ResponseWrapper
					{
						SequenceNumber = j["SequenceNumber"].ToObject<int>(),
						Content = JsonConvert.SerializeObject(j["Content"].ToObject<object>())
					};
				});
		}

		public static TType Create<TType>(TcpClient client)
		{
			return (TType) new RxClient(client, typeof(TType)).GetTransparentProxy();
		}

		public override IMessage Invoke(IMessage msg)
		{
			var methodCall = (IMethodCallMessage)msg;
			if (methodCall.InArgCount != 1)
				return new ReturnMessage(new NotSupportedException ("Must accept only one parameter"), methodCall);

			var method = (MethodInfo)methodCall.MethodBase;

			ResultInfo resultInfo;
			if (!_resultInfo.TryGetValue(method.Name, out resultInfo)) {
				if (!method.ReturnType.Name.Contains("IObservable"))
					return new ReturnMessage (new NotSupportedException ("Method must return IObservable"), methodCall);

				resultInfo = new ResultInfo ();

				resultInfo.ResultType = method.ReturnType.GetGenericArguments() [0];
				resultInfo.ObservableCast = typeof(Observable).GetMethod("Cast")
					.MakeGenericMethod(resultInfo.ResultType);

				_resultInfo.TryAdd(method.Name, resultInfo);
			}

			var seq = Interlocked.Add(ref _sequenceNumber, 1);

			var req = new RequestWrapper {
				SequenceNumber = seq,
				Content = methodCall.InArgs [0]
			};

			var buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req));

			var resultObservable = _reader
				.Where(r => r.SequenceNumber == seq)
				.Take(1)
				.Timeout(TimeSpan.FromSeconds(60))
				.Select(r => JsonConvert.DeserializeObject(r.Content, resultInfo.ResultType));

			var result = resultInfo.ObservableCast.Invoke(null, new object[] { resultObservable });

			_client.GetStream().WriteAsync(buf, 0, buf.Length);

			return new ReturnMessage(result, null, 0, methodCall.LogicalCallContext, methodCall);
		}
	}
}

