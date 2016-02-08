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

namespace Rx.Rpc.Utils
{
	public static class StreamExtensions
	{
		public static IObservable<string> ToObservable(this StreamReader source, int bufsize, IScheduler scheduler)
		{
			var strings = Observable.Create<string>(o => {
				var initialState = new StreamReaderState(source, bufsize);
				var sub = new SerialDisposable();
				Action<StreamReaderState, Action<StreamReaderState>> recursiveRead = (state, self) => {
					sub.Disposable = state.ReadNext().Subscribe(cr => {
						if(cr > 0)
						{
							o.OnNext(new string(state.Buffer, 0, cr));
							self(state);
						}
						else
						{
							o.OnCompleted();
						}
					}, ex => o.OnError(ex));
				};

				return new CompositeDisposable(sub, scheduler.Schedule(initialState, recursiveRead));
			}).Publish();

			strings.Connect();

			return strings;// Observable.Using(() => source, _ => strings);
		}

		private class StreamReaderState
		{
			private int _buffersize;
			private StreamReader _stream;

			public StreamReaderState(StreamReader source, int buffersize)
			{
				_buffersize = buffersize;
				_stream = source;

				Buffer = new char[buffersize];
			}

			public char[] Buffer { get; }

			public IObservable<int> ReadNext()
			{
				return Observable.FromAsync(() => _stream.ReadAsync(Buffer, 0, _buffersize));
			}
		}
	}
	
}
