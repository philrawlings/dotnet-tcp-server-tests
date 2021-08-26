using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpServerTest
{
    public static class NetworkStreamExtensions
    {
        public static void ReadNBytes(this NetworkStream stream, byte[] readBuffer, int offset, int size)
        {
            int bytesRead = 0;

            do
            {
                bytesRead += stream.Read(readBuffer, offset + bytesRead, size);
                size -= bytesRead;
            } while (bytesRead < size);
        }

        public static async Task ReadNBytesAsync(this NetworkStream stream, byte[] readBuffer, int offset, int size)
        {
            int bytesRead = 0;

            do
            {
                bytesRead += await stream.ReadAsync(readBuffer, offset + bytesRead, size);
                size -= bytesRead;
            } while (bytesRead < size);
        }
    }
}
