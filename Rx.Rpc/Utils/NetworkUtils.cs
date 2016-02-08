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
	public static class NetworkUtils
	{
		public static int GetMaxMtu()
		{
			var maxMtu = 0;
			var nics = NetworkInterface.GetAllNetworkInterfaces();
			foreach (var nic in nics)
			{
				if (!nic.Supports(NetworkInterfaceComponent.IPv4))
					continue;

				var props = nic.GetIPProperties().GetIPv4Properties();
				if (props == null)
					continue;

				maxMtu = Math.Max(props.Mtu, maxMtu);
			}

			return maxMtu;
		}
	}
	
}
