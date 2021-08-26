using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static TcpServerTest.CircularBufferFunctions;

namespace TcpServerTest
{
    public class CircularBufferServer
    {
        public static void Run()
        {
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 11000);

            // Create a Socket that will use Tcp protocol      
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // A Socket must be associated with an endpoint using the Bind method  
            listener.Bind(localEndPoint);
            // Specify how many requests a Socket can listen before it gives Server busy response.  
            // We will listen 10 requests at a time  
            listener.Listen(10);

            Socket socket = listener.Accept();

            // Data buffers
            int bufferLen = 1024;
            byte[] socketBuffer = new byte[bufferLen]; // Buffer filled by socket read
            CircularBuffer circularBuffer = InitialiseCircularBuffer(bufferLen); // Socket data copied to circular buffer
            byte[] readBuffer = new byte[bufferLen]; // Contiguous data for consumption after reading from circular buffer

            try
            {
                while (true)
                {
                    var recBytes = socket.Receive(socketBuffer);

                    if (recBytes > 0)
                    {
                        if (!WriteToCircularBuffer(socketBuffer, circularBuffer, recBytes))
                            throw new Exception("Circular buffer is full");

                        if (PeekFromCircularBuffer(circularBuffer, readBuffer, 2))
                        {
                            var msgLen = BinaryPrimitives.ReadUInt16BigEndian(readBuffer);

                            if (PeekFromCircularBuffer(circularBuffer, readBuffer, msgLen + 2))
                            {
                                AdvanceCircularBufferReadPosition(circularBuffer, 2 + msgLen); // Consume length + payload

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

                                    socket.Send(response);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                listener.Close();
            }
        }
    }
}
