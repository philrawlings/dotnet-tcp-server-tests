using System;
using System.Threading;
using System.Threading.Tasks;
using TcpServerTest;

namespace TcpServerTest.Host
{
    class Program
    {
        static void Main(string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            // Note, cancellation only works currently for the pipeline server, and only AFTER a client has connected

            Console.WriteLine("Server running");
            //CircularBufferServer.Run();
            //NetworkStreamServer.Run();

            try
            {
                //NetworkStreamAsyncServer.Run(cts.Token).GetAwaiter().GetResult();
                PipelineServer.Run(cts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) { }

        }
    }
}
