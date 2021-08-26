using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpServerTest
{
    public class CircularBuffer
    {
        public int HeadPos { get; set; }
        public int TailPos { get; set; }
        public int BufferLength { get; set; }
        public byte[] Buffer { get; set; }
        public int DataLength { get; set; }
    }

    // Purposely outside circular buffer class for easier translation to C (expected that Circular Buffer class will be a struct in C)
    public static class CircularBufferFunctions
    {
        public static CircularBuffer InitialiseCircularBuffer(int bufferLength)
        {
            return new CircularBuffer
            {
                HeadPos = 0,
                TailPos = 0,
                BufferLength = bufferLength,
                DataLength = 0,
                Buffer = new byte[bufferLength]
            };
        }

        public static bool WriteToCircularBuffer(byte[] socketBuffer, CircularBuffer circularBuffer, int numBytes)
        {
            int emptyCapacity;
            if (circularBuffer.DataLength == 0)
                emptyCapacity = circularBuffer.BufferLength;
            else
                emptyCapacity = circularBuffer.BufferLength - ((circularBuffer.HeadPos - circularBuffer.TailPos) % circularBuffer.BufferLength);

            if (emptyCapacity < numBytes)
                return false;

            circularBuffer.DataLength += numBytes;

            if (circularBuffer.HeadPos + numBytes >= circularBuffer.BufferLength)
            {
                int endBufLen = circularBuffer.BufferLength - circularBuffer.HeadPos;
                Buffer.BlockCopy(socketBuffer, 0, circularBuffer.Buffer, circularBuffer.HeadPos, endBufLen);
                int startBufLen = numBytes - endBufLen;
                Buffer.BlockCopy(socketBuffer, endBufLen, circularBuffer.Buffer, 0, startBufLen);
            }
            else
            {
                Buffer.BlockCopy(socketBuffer, 0, circularBuffer.Buffer, circularBuffer.HeadPos, numBytes);
            }

            circularBuffer.HeadPos += numBytes;
            // Wrap head position
            circularBuffer.HeadPos = circularBuffer.HeadPos % circularBuffer.BufferLength;

            return true;
        }

        public static bool ReadFromCircularBuffer(CircularBuffer circularBuffer, byte[] readBuffer, int numBytes)
        {
            if (PeekFromCircularBuffer(circularBuffer, readBuffer, numBytes))
            {
                AdvanceCircularBufferReadPosition(circularBuffer, numBytes);
                return true;
            }
            else
            {
                return false;
            }
        }


        public static bool PeekFromCircularBuffer(CircularBuffer circularBuffer, byte[] readBuffer, int numBytes)
        {
            if (circularBuffer.DataLength < numBytes)
                return false;
            else
            {
                if (circularBuffer.TailPos + numBytes < circularBuffer.BufferLength)
                {
                    Buffer.BlockCopy(circularBuffer.Buffer, circularBuffer.TailPos, readBuffer, 0, numBytes);
                }
                else
                {
                    int endBufLen = circularBuffer.BufferLength - circularBuffer.TailPos;
                    Buffer.BlockCopy(circularBuffer.Buffer, circularBuffer.TailPos, readBuffer, 0, endBufLen);
                    int startBufLen = numBytes - endBufLen;
                    Buffer.BlockCopy(circularBuffer.Buffer, 0, readBuffer, endBufLen, startBufLen);
                }

                return true;
            }
        }

        public static void AdvanceCircularBufferReadPosition(CircularBuffer circularBuffer, int numBytes)
        {
            if (numBytes > circularBuffer.DataLength)
                throw new Exception("Cannot advance past write position");

            circularBuffer.TailPos += numBytes;
            circularBuffer.TailPos = circularBuffer.TailPos % circularBuffer.BufferLength;
            circularBuffer.DataLength -= numBytes;
        }
    }
}
