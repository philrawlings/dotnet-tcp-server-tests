using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
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

            Memory<byte> receiveBuffer = new byte[1024];            // Memory, not Span, so it's async compatible
            ArrayBufferWriter<byte> transmitBuffer = new(1024);     // IBufferWriter has a nice API

            try
            {
                while (true)
                {
                    var pipeBuffer = await pipe.Reader.ReadAsync(cancellationToken);
                    if (pipeBuffer.Buffer.Length >= 2)
                    {
                        pipeBuffer.Buffer.Slice(0, 2).CopyTo(receiveBuffer.Span);
                        var payloadLen = BinaryPrimitives.ReadUInt16BigEndian(receiveBuffer.Span.Slice(0, 2));
                        var msgLen = 2 + payloadLen;

                        if (pipeBuffer.Buffer.Length >= msgLen)
                        {
                            pipeBuffer.Buffer.Slice(0, msgLen).CopyTo(receiveBuffer.Span);      // Copy the ReadOnlySequence into a more managable Memory buffer
                            var msg = receiveBuffer.Slice(0, msgLen);                           // Get Memory slice with the exact length of the message
                            if (TryProcessMessage(msg, transmitBuffer))                         // TryProcessMessage isn't doing any IO or heavy computation so don't really need to make it awaitable
                            {
                                socket.Send(transmitBuffer.WrittenSpan);
                            }
                            transmitBuffer.Clear();
                            pipe.Reader.AdvanceTo(pipeBuffer.Buffer.Slice(0, msgLen).End);
                        }
                        else
                        {
                            pipe.Reader.AdvanceTo(pipeBuffer.Buffer.Start); // Dont advance
                        }
                    }
                    else
                    {
                        pipe.Reader.AdvanceTo(pipeBuffer.Buffer.Start); // Dont advance
                    }
                }
            }
            catch(Exception e)
            {

            }
            finally
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                listener.Close();
                await pipeWriter;
            }
        }


        private static bool TryProcessMessage(ReadOnlyMemory<byte> msg, IBufferWriter<byte> writeBuffer)
        {
            // Full message received
            var msgID = BinaryPrimitives.ReadUInt16BigEndian(msg.Span.Slice(2, 2));
            var msgVersion = msg.Span.Slice(4, 1);

            if (msgID == 0x1)
            {
                var ipv4Address = new IPAddress(msg.Span.Slice(5, 4));
                var ipv6Address = new IPAddress(msg.Span.Slice(9, 16));
                var port = BinaryPrimitives.ReadUInt16BigEndian(msg.Span.Slice(25, 2));

                // Build response (duplicate of message, regenerating content from parsed data rather than just socket.Send(msg) 
                BinaryPrimitives.WriteUInt16BigEndian(writeBuffer.GetSpan(2), (ushort)(msg.Length - 2)); writeBuffer.Advance(2);
                BinaryPrimitives.WriteUInt16BigEndian(writeBuffer.GetSpan(2), msgID); writeBuffer.Advance(2);
                writeBuffer.Write(msgVersion);
                writeBuffer.Write(ipv4Address.GetAddressBytes());
                writeBuffer.Write(ipv6Address.GetAddressBytes());
                BinaryPrimitives.WriteUInt16BigEndian(writeBuffer.GetSpan(2), port); writeBuffer.Advance(2);
                return true;
            }
            else
            {
                return false;
                //throw new Exception("Bad command"); // Probably should return an error code in the response
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
