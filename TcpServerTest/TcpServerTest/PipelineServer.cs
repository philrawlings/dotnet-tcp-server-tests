using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpServerTest
{
    public class PipelineServer
    {
        public static async Task Run(CancellationToken cancellationToken)
        {
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 11000);

            // Create a Socket that will use Tcp protocol      
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // A Socket must be associated with an endpoint using the Bind method  
            listener.Bind(localEndPoint);
            // Specify how many requests a Socket can listen before it gives Server busy response.  
            // We will listen 10 requests at a time  
            listener.Listen(10);

            Socket socket = await listener.AcceptAsync();

            var pipe = new Pipe();
            Task pipeWriter = FillPipeAsync(socket, pipe.Writer); // Runs a task filling the pipe

            try
            {
                while (true)
                {
                    var result = await pipe.Reader.ReadAsync(cancellationToken);
                    if (result.Buffer.Length >= 2)
                    {
                        var readBuffer = result.Buffer.Slice(0, 2).ToArray();
                        var msgLen = BinaryPrimitives.ReadUInt16BigEndian(readBuffer);

                        if (result.Buffer.Length >= 2 + msgLen)
                        {
                            var msgSeq = result.Buffer.Slice(0, 2 + msgLen);
                            readBuffer = msgSeq.ToArray();
                            var response = await Task.FromResult(ProcessMessage(readBuffer, msgLen)); // Have to process in non-async methods as Span<T> not allowed in async method - compile error

                            pipe.Reader.AdvanceTo(msgSeq.End);

                            socket.Send(response);
                        }
                        else
                        {
                            pipe.Reader.AdvanceTo(result.Buffer.Start); // Dont advance
                        }
                    }
                    else
                    {
                        pipe.Reader.AdvanceTo(result.Buffer.Start); // Dont advance
                    }
                }
            }
            finally
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                listener.Close();
                await pipeWriter;
            }
        }

        private static byte[] ProcessMessage(byte[] readBuffer, ushort msgLen)
        {
            var msg = new Span<byte>(readBuffer);

            // Full message received
            var msgID = BinaryPrimitives.ReadUInt16BigEndian(msg.Slice(2, 2));
            var msgVersion = msg[4];

            if (msgID == 0x1)
            {
                var ipv4Address = new IPAddress(msg.Slice(5, 4));
                var ipv6Address = new IPAddress(msg.Slice(9, 16));
                var port = BinaryPrimitives.ReadUInt16BigEndian(msg.Slice(25, 2));

                // Build response (duplicate of message, regenerating content from parsed data rather than just socket.Send(msg) 
                byte[] response = new byte[2 + msgLen];
                BinaryPrimitives.WriteUInt16BigEndian(response, msgLen);

                var idData = new byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(idData, msgID);
                Buffer.BlockCopy(idData, 0, response, 2, 2);

                response[4] = msgVersion;
                Buffer.BlockCopy(ipv4Address.GetAddressBytes(), 0, response, 5, 4);
                Buffer.BlockCopy(ipv6Address.GetAddressBytes(), 0, response, 9, 16);
                var portData = new byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(portData, port);
                Buffer.BlockCopy(portData, 0, response, 25, 2);

                return response;
            }
            else
            {
                throw new Exception("Bad command"); // Probably should return an error code in the response
            }
        }

        private static async Task FillPipeAsync(Socket socket, PipeWriter writer)
        {
            const int minimumBufferSize = 512;

            while (true)
            {
                // Allocate at least 512 bytes from the PipeWriter
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    // Tell the PipeWriter how much was read from the Socket
                    writer.Advance(bytesRead);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error occurred in {nameof(FillPipeAsync)}: {ex.Message}");
                    break;
                }

                // Make the data available to the PipeReader
                FlushResult result = await writer.FlushAsync();

                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Tell the PipeReader that there's no more data coming
            writer.Complete();
        }
    }
}
