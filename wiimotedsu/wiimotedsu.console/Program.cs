using System.Net.Sockets;
using System.Buffers.Binary;
using wiimotedsu.core;

var udpClient = new UdpClient(26760);
Console.WriteLine("Listening for UDP packets on port 26760...");

while (true)
{
    try
    {
        var receivedResult = await udpClient.ReceiveAsync();
        var messageType = BinaryPrimitives.ReadUInt32LittleEndian(receivedResult.Buffer.AsSpan(16, 4));

        if (messageType == 0x100000)
        {
            Console.WriteLine("Received UDP packet from " + receivedResult.RemoteEndPoint + " with message type 0x100000");
            byte[] responseBuffer = new byte[24];
            DSUPacketBuilder.WriteProtocolVersionResponse(responseBuffer);
            await udpClient.SendAsync(responseBuffer, responseBuffer.Length, receivedResult.RemoteEndPoint);
        }
        else if (messageType == 0x100001)
        {
            Console.WriteLine("Received UDP packet from " + receivedResult.RemoteEndPoint + " with message type 0x100001");
            byte[] responseBuffer = new byte[32];
            DSUPacketBuilder.WritePortsInfoResponse(responseBuffer, 0);
            await udpClient.SendAsync(responseBuffer, responseBuffer.Length, receivedResult.RemoteEndPoint);
        }
        else if (messageType == 0x100002)
        {
            Console.WriteLine("Received UDP packet from " + receivedResult.RemoteEndPoint + " with message type 0x100002");
            byte[] responseBuffer = new byte[100];
            DSUPacketBuilder.WriteControllerDataResponse(responseBuffer, 0, 1);
            await udpClient.SendAsync(responseBuffer, responseBuffer.Length, receivedResult.RemoteEndPoint);
        }
    }
    catch (Exception ex) 
    {
        Console.WriteLine("Error receiving UDP packet: " + ex.Message);
    }
}