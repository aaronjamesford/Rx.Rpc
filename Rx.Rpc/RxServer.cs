using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Rx.Rpc.Utils;

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
				.SelectMany(client => 
					new StreamReader(client.GetStream()).ToObservable(NetworkUtils.GetMaxMtu(), new EventLoopScheduler())
						.Select(str => Tuple.Create(client.GetStream(), str)))
				.SelectMany(t => {
					var str = t.Item2;
					return JsonUtils.SplitJsonObjects(str).Select(obj => Tuple.Create(t.Item1, obj)).ToObservable();
				})
				.Select(t => {
					var j = JObject.Parse(t.Item2);

					var seq = j["SequenceNumber"].Value<int>();
					var content = j["Content"].ToObject<TRequest>();

					var subject = new Subject<TResponse>();

					subject.Take(1)
						.Timeout(TimeSpan.FromSeconds(59))
						.Subscribe(resp => {
							var resWrapper = new ResponseWrapper
								{
									SequenceNumber = seq,
									Content = resp
								};

							var buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(resWrapper));
							t.Item1.WriteAsync(buf, 0, buf.Length);
						},
						e => {
							var resWrapper = new ResponseWrapper
								{
									SequenceNumber = seq,
									Content = new { Error = new { Message = e.Message } }
								};

								Console.WriteLine("Server - {0}", e);

							var buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(resWrapper));
							t.Item1.WriteAsync(buf, 0, buf.Length);
						});

					return new Request<TRequest, TResponse> (content, subject);
				})
				.Finally(() => listener.Stop());
		}
	}
}

