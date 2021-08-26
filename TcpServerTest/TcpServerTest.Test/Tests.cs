using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using Xunit;

namespace TcpServerTest.Test
{
    public class Tests
    {
        [Fact]
        public void TestCircularBufferServer()
        {
            TestServerInNewThread(new ThreadStart(CircularBufferServer.Run));
        }

        [Fact]
        public void TestNetworkStreamServer()
        {
            TestServerInNewThread(new ThreadStart(NetworkStreamServer.Run));
        }

        [Fact]
        public void TestNetworkStreamAsyncServer()
        {
            TestServerInNewThread(new ThreadStart(() => NetworkStreamAsyncServer.Run(CancellationToken.None).GetAwaiter().GetResult()));
        }

        [Fact]
        public async void TestNetworkStreamAsyncServerTask()
        {
            throw new Exception("Wont work at present as need to use .NET6 cancellable socket methods in NetworkStreamAsyncServer");
            try
            {
                var cts = new CancellationTokenSource();
                var server = NetworkStreamAsyncServer.Run(cts.Token);
                SendReceiveData();
                cts.Cancel();
                await server;
            }
            catch (OperationCanceledException) { }
        }

        [Fact]
        public async void TestPipelineServerTask()
        {
            var cts = new CancellationTokenSource();
            try
            {
                var server = PipelineServer.Run(cts.Token);
                SendReceiveData();
                cts.Cancel();
                await server;
            }
            catch (OperationCanceledException) { }
        }

        private void TestServerInNewThread(ThreadStart start)
        {
            Thread t = new Thread(start);

            try
            {
                t.Start();
                SendReceiveData();
            }
            finally
            {
                t.Interrupt();
            }
        }

        private static void SendReceiveData()
        {
            // {u16: payload length} {u16: id} {u8: version} {4 bytes: IPv4 address} {16 bytes: IPv6 address} {u16: Port}
            var writeBuffer = new byte[] { 0, 25, 0, 1, 1, 192, 168, 178, 54, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 22, 46 };
            TcpClient client = new TcpClient();
            client.Connect("127.0.0.1", 11000);
            var stream = client.GetStream();
            stream.Write(writeBuffer);

            var readBuffer = new byte[writeBuffer.Length];
            stream.ReadNBytes(readBuffer, 0, readBuffer.Length);

            Assert.True(writeBuffer.SequenceEqual(readBuffer));
        }
    }
}
