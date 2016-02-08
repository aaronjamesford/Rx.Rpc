using System;
using NUnit.Framework;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Linq;

namespace Rx.Rpc.Test
{
	[TestFixture]
	public class IntergrationTests
	{
		[Test]
		public void SimpleUsageTest() {
			using (RxServer.Create<string, string>(3389).Subscribe(r => r.Respond("Hello, " + r.Content)))
			using(var socket = new TcpClient("127.0.0.1", 3389))
			{
				var res = RxClient.Create<ISayHello>(socket)
					.SayHello("Aaron")
					.Timeout(TimeSpan.FromSeconds(10))
					.Wait();

				Assert.That(res, Is.EqualTo("Hello, Aaron"));
			}
		}

		[Test]
		public void SimpleTestWithMoreComplexTypes() {
			using (RxServer.Create<ComplexRequestType, ComplexResponseType>(3389)
				.Subscribe(r => r.Respond(new ComplexResponseType 
					{
						Name = r.Content.Name,
						Question = "How is it being " + r.Content.Age + " years old, " + r.Content.Name + "?"
					})
				))
			using(var socket = new TcpClient ("localhost", 3389))
			{
				var res = RxClient.Create<IComplexClient>(socket)
					.Introduce(new ComplexRequestType { Name = "Aaron", Age = 25 })
					.Timeout(TimeSpan.FromSeconds(10))
					.Wait();

				Assert.That(res.Name, Is.EqualTo("Aaron"));
				Assert.That(res.Question, Is.EqualTo("How is it being 25 years old, Aaron?"));
			}
		}

		[Test]
		public void ManyRequestsWithConcurrencyTest()
		{
			using (RxServer.Create<Tuple<int, int>, int>(3389).Subscribe(r => r.Respond(r.Content.Item1 + r.Content.Item2)))
			using(var socket = new TcpClient("127.0.0.1", 3389))
			{
				var res = new ConcurrentBag<bool> ();
				var rand = new Random();
				var client = RxClient.Create<IAddingClient>(socket);
				var s = new IScheduler[20];
				for (int i = 0; i < 20; ++i) {
					s [i] = new EventLoopScheduler ();
				}

				foreach (var i in Enumerable.Range(1, 10000))
				{
					s[i%20].Schedule(() => {
						var x = rand.Next(4000);
						var y = rand.Next(4000);
						var result = client.Add(Tuple.Create(x, y)).Wait();

						res.Add(x + y == result);
					});
				}

				Assert.That(() => res, Has.Count.EqualTo(10000).After(10000, 250));
				Assert.That(res, Has.All.EqualTo(true));
			}
		}

		[Test]
		public void TryingToUseProxyThatDoesNotreturnObservable() {
			using (RxServer.Create<string, string>(3389).Subscribe(r => r.Respond("Hello, " + r.Content)))
			using(var client = new TcpClient("127.0.0.1", 3389))
			{
				var c = RxClient.Create<INotAnObservable>(client);
				Assert.That(() => c.SayHello("Aaron"), Throws.InstanceOf<NotSupportedException>());
			}
		}

		private class ComplexResponseType
		{
			public string Name { get; set; }
			public string Question { get; set; }
		}

		private class ComplexRequestType
		{
			public string Name { get; set; }
			public int Age { get; set; }
		}

		private interface IComplexClient
		{
			IObservable<ComplexResponseType> Introduce(ComplexRequestType intro);
		}

		public interface ISayHello
		{
			IObservable<string> SayHello(string name);
		}

		public interface INotAnObservable
		{
			string SayHello(string name);
		}

		private interface IAddingClient
		{
			IObservable<int> Add(Tuple<int, int> t);
		}
	}
}

