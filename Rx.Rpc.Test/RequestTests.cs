using System;
using NUnit.Framework;
using NSubstitute;
using Newtonsoft.Json;
using System.Net.Sockets;
using System.Text;
using System.Reactive.Linq;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Concurrency;
using System.Threading;
using System.Collections.Generic;

namespace Rx.Rpc.Test
{
	[TestFixture]
	public class RequestTests
	{
		[Test, Ignore("CauseISeadSooo")]
		public void IDontEven() {
			using (RxServer.Create<string, string>(3389)
				.Subscribe(
				      r => r.Respond("Hello, " + r.Content)
			      ))
			{
				var client = new TcpClient("127.0.0.1", 3389);
				var buf = Encoding.UTF8.GetBytes("\"Aaron\"");
				client.GetStream().Write(buf, 0, buf.Length);

				var recv = new byte[512];
				var br = client.GetStream().Read(recv, 0, 512);
				var str = JsonConvert.DeserializeObject<string>(Encoding.UTF8.GetString(recv, 0, br));

				Assert.That(str, Is.EqualTo("Hello, Aaron"));

				client.Close();
			}
		}

		[Test]
		public void UsingTheProxy() {
			using (RxServer.Create<string, string>(3389)
				.Subscribe(
					r => r.Respond("Hello, " + r.Content)
				))
			{
				var client = new TcpClient("127.0.0.1", 3389);

				var res = RxClient.Create<ISayHello>(client)
					.SayHello("Aaron")
					.Timeout(TimeSpan.FromSeconds(10))
					.Wait();

				Assert.That(res, Is.EqualTo("Hello, Aaron"));

				client.Close();
			}
		}

		public class ComplexResponseType
		{
			public string Name { get; set; }
			public string Question { get; set; }
		}

		public class ComplexRequestType
		{
			public string Name { get; set; }
			public int Age { get; set; }
		}

		public interface IQuestionTime
		{
			IObservable<ComplexResponseType> Introduce(ComplexRequestType intro);
		}

		[Test]
		public void ALittleBitMoreComplex() {
			using (RxServer.Create<ComplexRequestType, ComplexResponseType>(3389)
				.Subscribe(r => r.Respond(new ComplexResponseType {
											Name = r.Content.Name,
					Question = "How is it being " + r.Content.Age + " years old, " + r.Content.Name + "?"
			}))) {
				var c = new TcpClient ("localhost", 3389);
				var res = RxClient.Create<IQuestionTime>(c)
					.Introduce(new ComplexRequestType { Name = "Aaron", Age = 25 })
					.Wait();

				Assert.That(res.Name, Is.EqualTo("Aaron"));
				Assert.That(res.Question, Is.EqualTo("How is it being 25 years old, Aaron?"));
			}
		}

		private interface IAdder {
			IObservable<int> Add(Tuple<int, int> t);
		}

		[Test]
		public void ConcurrentTest() {
			using(RxServer.Create<Tuple<int,int>, int>(3389)
				.Subscribe(t => t.Respond(t.Content.Item1 + t.Content.Item2)))
			{
				var rand = new Random();

				var client = RxClient.Create<IAdder>(new TcpClient ("localhost", 3389));
				var l = new List<bool> ();

				for(int i = 0; i <1; i++)
				{
					//Observable.Interval(TimeSpan.FromMilliseconds(500), Scheduler.Default)
					//	.Take(20)
					Observable.Range(1, 1000, Scheduler.Default)
						.Select(__ => {
							var x = rand.Next(4000);
							var y = rand.Next(4000);

							return x + y == client.Add(Tuple.Create(x, y)).Wait();
						})
						.Subscribe(arr => l.Add(arr));
				}
//					.ToArray()
//					.Wait();
//
//				Console.WriteLine(ob.Length);
//
				Thread.Sleep(30000);
				Console.WriteLine(l.Count);
				Assert.That(() => l.Count, Is.EqualTo(50).Within(20).Seconds);
//				Assert.That(ob.All(b => b), Is.True);
			}
		}

		[Test]
		public void TryingToUseProxyThatDoesNotreturnObservable() {
			using (RxServer.Create<string, string>(3389)
				.Subscribe(
					r => r.Respond("Hello, " + r.Content)))
			{
				var client = new TcpClient("127.0.0.1", 3389);

				var c = RxClient.Create<INotAnObservable>(client);
				Assert.That(() => c.SayHello("Aaron"), Throws.InstanceOf<NotSupportedException>());

				client.Close();
			}
		}

		public interface ISayHello
		{
			IObservable<string> SayHello(string name);
		}

		public interface INotAnObservable
		{
			string SayHello(string name);
		}
		
//		[Test]
//		public void RespondPublishesToResponder()
//		{
//			var observer = Substitute.For<IObserver<int>>();
//
//			new Request<int, int>(5, observer)
//				.Respond(13);
//
//			observer.Received(1).OnNext(13);
//		}
//
//		[Test]
//		public void RespondCompletesResponder()
//		{
//			var observer = Substitute.For<IObserver<int>>();
//
//			new Request<int, int>(5, observer)
//				.Respond(13);
//
//			observer.Received(1).OnCompleted();
//		}
//
//		[Test]
//		public void RespondErrorPublishesError()
//		{
//			var observer = Substitute.For<IObserver<int>>();
//
//			var error = new Exception("This is an error");
//			new Request<int, int>(5, observer)
//				.RespondError(error);
//
//			observer.Received(1).OnError(error);
//			observer.DidNotReceive().OnCompleted();
//		}
//
//		[Test]
//		public void CannotRespondTwice()
//		{
//			var observer = Substitute.For<IObserver<int>>();
//
//			var request = new Request<int, int>(5, observer);
//			request.Respond(13);
//
//			Assert.That(() => request.Respond(13), Throws.Exception);
//			observer.ReceivedWithAnyArgs(1).OnNext(0);
//			observer.Received(1).OnCompleted();
//		}
//
//		[Test]
//		public void CannotRespondErrorTwice()
//		{
//			var observer = Substitute.For<IObserver<int>>();
//
//			var request = new Request<int, int>(5, observer);
//			request.RespondError(new Exception());
//
//			Assert.That(() => request.RespondError(new Exception()), Throws.Exception);
//			observer.ReceivedWithAnyArgs(1).OnError(null);
//		}


	}
}

