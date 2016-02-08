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

namespace Rx.Rpc.Utils
{
	public static class JsonUtils
	{
		public static IEnumerable<string> SplitJsonObjects(string str)
		{
			var stack = new Stack<char>();
			var start = 0;
			for(int i = 0; i < str.Length; ++i)
			{
				var l = str[i];
				switch(l)
				{
				case '{':
					if(stack.Count == 0 || stack.Peek() != '"')
						stack.Push(l);
					break;
				case '"':
					if(stack.Count > 0 && (stack.Peek() == '"' || stack.Peek() == '\\'))
						stack.Pop();
					else
						stack.Push(l);
					break;
				case '\\':
					stack.Push(l);
					break;
				case '}':
					if(stack.Count > 0 && stack.Peek() == '"')
						stack.Pop();
					else if(stack.Count > 0 && stack.Peek() == '{')
					{
						stack.Pop();
						if(stack.Count == 0)
						{
							yield return str.Substring(start, i - start + 1);
							start = i + 1;
						}
					}
					break;
				default:
					if(stack.Count > 0 && 	stack.Peek() == '\\')
						stack.Pop();
					break;
				}
			}
		}
	}
}
