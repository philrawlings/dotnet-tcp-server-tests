# dotnet TCP Server Tests

A number of trials to determine the optimum way to write a TCP Server, handling streamed data messages with the format `{length}{payload}` such as:

![TCP Message](tcp-message.png?raw=true "TCP Message")

## Servers
----

### **CircularBufferServer**

Uses raw Socket functions and implements a circular buffer for data with functions to read fixed numbers of bytes.
The issue this tries to overcome is reading messages where all the data may not have arrived yet (i.e. length and payload may have been transmitted separately).

This has been written in a low level way to facilitate easy translation into C (hence why the `CircularBuffer` methods arent _in_ the class).

This probably could have been done in a simpler way by creating a socket read function/extension `ReadNBytes(byte[] readBuffer, int offset, int length)` (which blocks until all the bytes have been read) in a similar way to the `NetworkStream` extension of the same name created for `NetworkStreamServer`. Hindsight is a wonderful thing :-) ... Plus at least this version doesnt block on reading from the circular buffer (although the socket read functions _do_ block, so not really a huge win!)

### **NetworkStreamServer**

Variation on above, but uses NetworkStream to avoid using manually created circular buffer

### **NetworkStreamAsyncServer**

Uses async methods, however in .NET5 these are not cancellable. Cancellable socket methods will exist in .NET6, so expect this to be the right way forward for the future. No real benefit over the non-cancellable version for now.

### **PipelineServer**

Uses System.IO.Pipelines, which does allow cancellation. Might not be doing buffer management in the right way so may not be the most efficient, but was the only way I could get it to work. Seems to be quite a bit of faff and ceremony for no real benefit over NetworkStreamAsyncSever (when cancellable socket methods are available).
Not perfect anyway since socket.Accept isnt cancellable anyway.

## Testing
----

- Run tests in test project, which fires off each server individually and uses TcpClient to send data to it

OR

- Run TcpServerTest.Host and then run the LabVIEW Client Test VI.

## Conclusion
---
From a code cleanliness and readbility perspective, `NetworkStreamAsyncServer` will be the way to go, once Cancellable socket methods are available in .NET6. 
