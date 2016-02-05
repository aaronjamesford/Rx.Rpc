using System;
using System.Threading;
using System.Reactive.Linq;
using System.Net.Sockets;
using System.Net;
using System.Reactive.Disposables;
using System.Text;
using Newtonsoft.Json;
using System.Reactive.Subjects;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace Rx.Rpc
{
	public class RxServer
	{
		private class RequestWrapper
		{
			public int SequenceNumber { get; set; }
			public JObject Content { get; set; }
		}

		private class ResponseWrapper
		{
			public int SequenceNumber { get; set; }
			public object Content { get; set; }
		}
		
		public static IObservable<Request<TRequest, TResponse>> Create<TRequest, TResponse>(int port)
		{
			var listener = new TcpListener(IPAddress.Any, port);
			listener.Start();

			return Observable.Defer(() => Observable.StartAsync(listener.AcceptTcpClientAsync))
				.Repeat()
				.SelectMany(client => {
					var stream = client.GetStream();
					var buffer = new byte[512];
					return Observable.Defer(() => Observable.StartAsync(ct => stream.ReadAsync(buffer, 0, buffer.Length)))
						.TakeWhile(br => br > 0 && client.Connected)
						.Select(br => {
							var res = new byte[br];
							Buffer.BlockCopy(buffer, 0, res, 0, br);
							return Tuple.Create(stream, res);
						})
						.Repeat();
				})
				.Select(t => {
					var str = Encoding.UTF8.GetString(t.Item2);
					var j = JObject.Parse(str);

					var seq = j["SequenceNumber"].Value<int>();
					var content = j["Content"].ToObject<TRequest>();

					var subject = new Subject<TResponse>();

					subject.Take(1)
						.Timeout(TimeSpan.FromSeconds(2))
						.Subscribe(
						async resp => {
							var resWrapper = new ResponseWrapper
								{
									SequenceNumber = seq,
									Content = resp
								};

							var buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(resWrapper));
							await t.Item1.WriteAsync(buf, 0, buf.Length);
						},
						e => {
							var resWrapper = new ResponseWrapper
								{
									SequenceNumber = seq,
									Content = new { Error = new { Message = e.Message } }
								};

								Console.WriteLine(e);

							var buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(resWrapper));
							t.Item1.WriteAsync(buf, 0, buf.Length);
						});

					return new Request<TRequest, TResponse> (content, subject);
				})
				.Finally(() => listener.Stop());
		}
	}
}

