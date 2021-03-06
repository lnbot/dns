﻿using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.IO;
using DNS.Protocol;
using DNS.Protocol.Utils;

namespace DNS.Client.RequestResolver {
    public class UdpRequestResolver : IRequestResolver {
        private int timeout;
        private IRequestResolver fallback;
        private IPEndPoint dns;
        private IPEndPoint localEndPoint;

        public UdpRequestResolver(IPEndPoint dns, IRequestResolver fallback, int timeout = 5000, IPEndPoint localEndPoint = default(IPEndPoint)) {
            this.dns = dns;
            this.fallback = fallback;
            this.timeout = timeout;
            this.localEndPoint = localEndPoint;
        }

        public UdpRequestResolver(IPEndPoint dns, int timeout = 5000, IPEndPoint localEndPoint = default(IPEndPoint)) {
            this.dns = dns;
            this.fallback = new NullRequestResolver();
            this.timeout = timeout;
            this.localEndPoint = localEndPoint;
        }

        private UdpClient InitUdpClient() {
            if (localEndPoint == default(IPEndPoint)) {
                return new UdpClient();
            } else {
                return new UdpClient(localEndPoint);
            }
        }

        public async Task<IResponse> Resolve(IRequest request) {
            using(UdpClient udp = InitUdpClient()) {
                await udp
                    .SendAsync(request.ToArray(), request.Size, dns)
                    .WithCancellationTimeout(timeout);

                UdpReceiveResult result = await udp.ReceiveAsync().WithCancellationTimeout(timeout);
                if(!result.RemoteEndPoint.Equals(dns)) throw new IOException("Remote endpoint mismatch");
                byte[] buffer = result.Buffer;
                Response response = Response.FromArray(buffer);

                if (response.Truncated) {
                    return await fallback.Resolve(request);
                }

                return new ClientResponse(request, response, buffer);
            }
        }
    }
}
