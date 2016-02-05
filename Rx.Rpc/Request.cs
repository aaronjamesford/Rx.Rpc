using System;
using System.Threading;
using System.Reactive.Linq;

namespace Rx.Rpc
{
	public class Request<TRequest, TResponse>
	{
		IObserver<TResponse> _responder;

		public Request(TRequest content, IObserver<TResponse> responder)
		{
			Content = content;
			_responder = responder;
		}

		public TRequest Content { get; }

		public void Respond(TResponse response)
		{
			var responder = Interlocked.Exchange(ref _responder, null);

			if (responder == null)
				throw new Exception("Request has already been responded to");
			
			responder.OnNext(response);
			responder.OnCompleted();
		}

		public void RespondError(Exception error)
		{
			var responder = Interlocked.Exchange(ref _responder, null);

			if (responder == null)
				throw new Exception("Request has already been responded to");
			
			responder.OnError(error);
		}
	}
	
}
