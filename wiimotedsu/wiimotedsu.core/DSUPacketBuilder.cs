using System.Buffers.Binary;
using System.Text;

namespace wiimotedsu.core
{
    public class DSUPacketBuilder
    {
        public static void WriteHeader(Span<byte> buffer, ushort payloadLength, uint messageType, uint serverId = 100200300)
        {
            // bytes 0-3 "Magic string — DSUS if it’s message by server (you), DSUC if by client (cemuhook)."
            byte[] magicString = Encoding.ASCII.GetBytes("DSUS");
            magicString.CopyTo(buffer.Slice(0, 4));

            // bytes 4-5 "Protocol version used in message. Currently 1001."
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(4, 2), 1001);

            // bytes 6-7 "Length of packet without header. Drop packet if it’s too short, truncate if it’s too long."
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(6, 2), payloadLength);

            // bytes 8-11 "CRC32 of whole packet while this field was zeroed out. Be careful with endianness here!"
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8, 4), 0);

            // bytes 12-15 "Client or server ID who sent this packet. Should stay the same on one run. Can be randomly generated on startup."
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(12, 4), serverId);

            // bytes 16-19 "Not actually part of header so it counts as length. Event type. Read below to learn possible ones."
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(16, 4), messageType);
        }

        public static void WriteProtocolVersionResponse(Span<byte> buffer, uint serverId = 100200300) 
        {
            // Protocol version information
            uint messageType = 0x100000;

            // Length of the payload (4 bytes message type + 2 bytes version = 6)
            ushort payloadLength = 6;

            WriteHeader(buffer, payloadLength, messageType, serverId);

            // bytes 20-21: Protocol version (1001)
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(20, 2), 1001);

            // Calculate CRC32 and override bytes 8-11
            uint crc = System.IO.Hashing.Crc32.HashToUInt32(buffer.Slice(0, 22));
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8, 4), crc);
        }

        public static void WritePortsInfoResponse(Span<byte> buffer, byte slotId, ReadOnlySpan<byte> macAddress = default, byte batteryStatus = 0x05, uint serverId = 100200300)
        {
            // Information about connected controllers
            uint messageType = 0x100001;
            ushort payloadLength = 16;

            WriteHeader(buffer, payloadLength, messageType, serverId);

            // byte 0 "Slot you’re reporting about. Must be the same as byte value you read."
            buffer[20] = slotId;

            // byte 1 "Slot state: 0 if not connected, 1 if reserved (?), 2 if connected."
            byte slotState = 2;
            buffer[21] = slotState;

            // byte 2 "Device model: 0 if not applicable, 1 if no or partial gyro 2 for full gyro. Value 3 exist but should not be used (go with VR, guys)."
            byte deviceModel = 2;
            buffer[22] = deviceModel;

            // byte 3 "Connection type: 0 if not applicable, 1 for USB, 2 for bluetooth."
            byte connectionType = 2;
            buffer[23] = connectionType;

            // bytes 4-9 "MAC address of device. It’s used to detect same device between launches. Zero out if not applicable."
            if (macAddress.Length == 6)
            {
                macAddress.CopyTo(buffer.Slice(24, 6));
            }
            else
            {
                buffer[24] = 0x00;
                buffer[25] = 0x11;
                buffer[26] = 0x22;
                buffer[27] = 0x33;
                buffer[28] = 0x44;
                buffer[29] = 0x55;
            }

            // byte 10 "Battery status. See below for possible values."
            buffer[30] = batteryStatus;

            // Calculate CRC32 and override bytes 8-11
            uint crc = System.IO.Hashing.Crc32.HashToUInt32(buffer);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8, 4), crc);
        }

        public static void WriteControllerDataResponse(Span<byte> buffer, byte slotId, uint packetNumber, ulong timestamp,
            float accX, float accY, float accZ,
            float gyroPitch, float gyroYaw, float gyroRoll,
            byte buttons1 = 0, byte buttons2 = 0, byte homeButton = 0, byte touchButton = 0,
            ReadOnlySpan<byte> macAddress = default, byte batteryStatus = 0x05, uint serverId = 100200300)
        {
            // Actual controllers data
            uint messageType = 0x100002;
            ushort payloadLength = 84;

            WriteHeader(buffer, payloadLength, messageType, serverId);

            // byte 0 "Slot you’re reporting about. Must be the same as byte value you read."
            buffer[20] = slotId;

            // byte 1 "Slot state: 0 if not connected, 1 if reserved (?), 2 if connected."
            byte slotState = 2;
            buffer[21] = slotState;

            // byte 2 "Device model: 0 if not applicable, 1 if no or partial gyro 2 for full gyro. Value 3 exist but should not be used (go with VR, guys)."
            byte deviceModel = 2;
            buffer[22] = deviceModel;

            // byte 3 "Connection type: 0 if not applicable, 1 for USB, 2 for bluetooth."
            byte connectionType = 2;
            buffer[23] = connectionType;

            // bytes 4-9 "MAC address of device. It’s used to detect same device between launches. Zero out if not applicable."
            if (macAddress.Length == 6)
            {
                macAddress.CopyTo(buffer.Slice(24, 6));
            }
            else
            {
                buffer[24] = 0x00;
                buffer[25] = 0x11;
                buffer[26] = 0x22;
                buffer[27] = 0x33;
                buffer[28] = 0x44;
                buffer[29] = 0x55;
            }

            // byte 10 "Battery status. See below for possible values."
            buffer[30] = batteryStatus;

            // byte 11 "Is controller connected (1 if connected, 0 if not)"
            byte isConnected = 1;
            buffer[31] = isConnected;

            // bytes 12-15 "Packet number (for this client)"
            uint packetNumberToWrite = packetNumber;
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(32, 4), packetNumberToWrite);

            // byte 16 "D-Pad Left, D-Pad Down, D-Pad Right, D-Pad Up, Options (?), R3, L3, Share (?)"
            buffer[36] = buttons1;

            // byte 17 "Y, B, A, X, R1, L1, R2, L2"
            buffer[37] = buttons2;

            // byte 18 "HOME Button (0 or 1)"
            buffer[38] = homeButton;

            // byte 19 "Touch Button (0 or 1)"
            buffer[39] = touchButton;

            // bytes 20-23 "Left stick X, Left stick Y, Right stick X, Right stick Y"
            byte leftStickX = 128; // Centered
            buffer[40] = leftStickX;

            byte leftStickY = 128; // Centered
            buffer[41] = leftStickY;

            byte rightStickX = 128; // Centered
            buffer[42] = rightStickX;

            byte rightStickY = 128; // Centered
            buffer[43] = rightStickY;

            // bytes 24-35 (offset 44-55) "Analog D-Pad, Analog buttons (0=released, 255=pressed)"
            // Analog D-Pad: Left, Down, Right, Up
            buffer[44] = (byte)((buttons1 & 0x80) != 0 ? 0xFF : 0x00); // D-Pad Left
            buffer[45] = (byte)((buttons1 & 0x40) != 0 ? 0xFF : 0x00); // D-Pad Down
            buffer[46] = (byte)((buttons1 & 0x20) != 0 ? 0xFF : 0x00); // D-Pad Right
            buffer[47] = (byte)((buttons1 & 0x10) != 0 ? 0xFF : 0x00); // D-Pad Up

            // Analog buttons: Y/Square, B/Cross, A/Circle, X/Triangle, R1, L1, R2, L2
            buffer[48] = (byte)((buttons2 & 0x80) != 0 ? 0xFF : 0x00); // Y / Square
            buffer[49] = (byte)((buttons2 & 0x40) != 0 ? 0xFF : 0x00); // B / Cross (or Circle)
            buffer[50] = (byte)((buttons2 & 0x20) != 0 ? 0xFF : 0x00); // A / Circle (or Cross)
            buffer[51] = (byte)((buttons2 & 0x10) != 0 ? 0xFF : 0x00); // X / Triangle
            buffer[52] = (byte)((buttons2 & 0x08) != 0 ? 0xFF : 0x00); // R1
            buffer[53] = (byte)((buttons2 & 0x04) != 0 ? 0xFF : 0x00); // L1
            buffer[54] = (byte)((buttons2 & 0x02) != 0 ? 0xFF : 0x00); // R2
            buffer[55] = (byte)((buttons2 & 0x01) != 0 ? 0xFF : 0x00); // L2

            // bytes 36-47 (offset 56-67) Touches & unused analog. Zeroed out.
            buffer.Slice(56, 12).Clear();

            // bytes 48-55 "Motion data timestamp in microseconds"
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(68, 8), timestamp);

            // bytes 56-59 "Accelerometer X axis"
            BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(76, 4), accX);

            // bytes 60-63 "Accelerometer Y axis"
            BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(80, 4), accY);

            // bytes 64-67 "Accelerometer Z axis"
            BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(84, 4), accZ);

            // bytes 68-71 "Gyroscope pitch"
            BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(88, 4), gyroPitch);

            // bytes 72-75 "Gyroscope yaw"
            BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(92, 4), gyroYaw);

            // bytes 76-79 "Gyroscope roll"
            BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(96, 4), gyroRoll);

            // Calculate CRC32 and override bytes 8-11
            uint crc = System.IO.Hashing.Crc32.HashToUInt32(buffer);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8, 4), crc);
        }
    }
}
