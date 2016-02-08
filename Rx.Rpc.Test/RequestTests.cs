using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;

namespace Rx.Rpc.Test
{
	[TestFixture]
	public class RequestTests
	{
		[Test]
		public void RespondPublishesToResponder()
		{
			var observer = Substitute.For<IObserver<int>>();

			new Request<int, int>(5, observer)
				.Respond(13);

			observer.Received(1).OnNext(13);
		}

		[Test]
		public void RespondCompletesResponder()
		{
			var observer = Substitute.For<IObserver<int>>();

			new Request<int, int>(5, observer)
				.Respond(13);

			observer.Received(1).OnCompleted();
		}

		[Test]
		public void RespondErrorPublishesError()
		{
			var observer = Substitute.For<IObserver<int>>();

			var error = new Exception("This is an error");
			new Request<int, int>(5, observer)
				.RespondError(error);

			observer.Received(1).OnError(error);
			observer.DidNotReceive().OnCompleted();
		}

		[Test]
		public void CannotRespondTwice()
		{
			var observer = Substitute.For<IObserver<int>>();

			var request = new Request<int, int>(5, observer);
			request.Respond(13);

			Assert.That(() => request.Respond(13), Throws.Exception);
			observer.ReceivedWithAnyArgs(1).OnNext(0);
			observer.Received(1).OnCompleted();
		}

		[Test]
		public void CannotRespondErrorTwice()
		{
			var observer = Substitute.For<IObserver<int>>();

			var request = new Request<int, int>(5, observer);
			request.RespondError(new Exception());

			Assert.That(() => request.RespondError(new Exception()), Throws.Exception);
			observer.ReceivedWithAnyArgs(1).OnError(null);
		}
	}
}

