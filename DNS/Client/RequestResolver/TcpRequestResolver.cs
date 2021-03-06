﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Net;
using DNS.Protocol;

namespace DNS.Client.RequestResolver {
    public class TcpRequestResolver : IRequestResolver {
        private IPEndPoint dns;
        private IPEndPoint localEndPoint;

        public TcpRequestResolver(IPEndPoint dns, IPEndPoint localEndPoint = default(IPEndPoint)) {
            this.dns = dns;
            this.localEndPoint = localEndPoint;
        }

        private TcpClient InitTcpClient() {
            if (localEndPoint == default(IPEndPoint)) {
                return new TcpClient();
            } else {
                return new TcpClient(localEndPoint);
            }
        }

        public async Task<IResponse> Resolve(IRequest request) {
            using(TcpClient tcp = InitTcpClient()) {
                await tcp.ConnectAsync(dns.Address, dns.Port);

                Stream stream = tcp.GetStream();
                byte[] buffer = request.ToArray();
                byte[] length = BitConverter.GetBytes((ushort) buffer.Length);

                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(length);
                }

                await stream.WriteAsync(length, 0, length.Length);
                await stream.WriteAsync(buffer, 0, buffer.Length);

                buffer = new byte[2];
                await Read(stream, buffer);

                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(buffer);
                }

                buffer = new byte[BitConverter.ToUInt16(buffer, 0)];
                await Read(stream, buffer);

                IResponse response = Response.FromArray(buffer);
                return new ClientResponse(request, response, buffer);
            }
        }

        private static async Task Read(Stream stream, byte[] buffer) {
            int length = buffer.Length;
            int offset = 0;
            int size = 0;

            while (length > 0 && (size = await stream.ReadAsync(buffer, offset, length)) > 0) {
                offset += size;
                length -= size;
            }

            if (length > 0) {
                throw new IOException("Unexpected end of stream");
            }
        }
    }
}
