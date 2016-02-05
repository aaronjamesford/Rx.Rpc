using System;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Remoting.Messaging;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using System.Reactive.Threading.Tasks;
using System.Reactive.Linq;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

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

		private int _sequenceNumber = 1;

		TcpClient _client;
		IObservable<ResponseWrapper> _reader;

		private RxClient(TcpClient client, Type t)
			: base(t)
		{
			_client = client;

			var buffer = new byte[512];
			_reader = Observable.Defer(() => Observable.FromAsync(ct => _client.GetStream().ReadAsync(buffer, 0, buffer.Length, ct)))
				.TakeWhile(br => br > 0 && client.Connected)
				.Select(br => {
					var buf = new byte[br];
					Buffer.BlockCopy(buffer, 0, buf, 0, br);

					var j = JObject.Parse(Encoding.UTF8.GetString(buf));

					return new ResponseWrapper
					{
						SequenceNumber = j["SequenceNumber"].ToObject<int>(),
						Content = JsonConvert.SerializeObject(j["Content"].ToObject<object>())
					};
				})
				.Repeat();
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
			if (!method.ReturnType.Name.Contains("IObservable"))
				return new ReturnMessage (new NotSupportedException ("Method must return IObservable"), methodCall);

			var resultType = method.ReturnType.GetGenericArguments()[0];
			var seq = Interlocked.Add(ref _sequenceNumber, 1);

			var req = new RequestWrapper {
				SequenceNumber = seq,
				Content = methodCall.InArgs [0]
			};

			var buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req));
			_client.GetStream().WriteAsync(buf, 0, buf.Length).Wait();

			var resultObservable = _reader
				.Where(r => r.SequenceNumber == seq)
				.Timeout(TimeSpan.FromSeconds(5))
				.Select(r => JsonConvert.DeserializeObject(r.Content, resultType))
				.Take(1);

			var cast = typeof(Observable).GetMethod("Cast")
				.MakeGenericMethod(resultType);
			
			var result = cast.Invoke(null, new object[] { resultObservable });

			return new ReturnMessage(result, null, 0, methodCall.LogicalCallContext, methodCall);
		}
	}
}

