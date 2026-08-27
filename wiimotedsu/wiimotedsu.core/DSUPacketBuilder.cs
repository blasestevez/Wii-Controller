using System.Buffers.Binary;
using System.Text;

namespace wiimotedsu.core
{
    public class DSUPacketBuilder
    {
        public static void WriteHeader(Span<byte> buffer, ushort payloadLength, uint messageType)
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
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(12, 4), 100200300);

            // bytes 16-19 "Not actually part of header so it counts as length. Event type. Read below to learn possible ones."
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(16, 4), messageType);
        }

        public static void WriteProtocolVersionResponse(Span<byte> buffer) 
        {
            // Protocol version information (doesn’t seem to be ever requested)
            uint messageType = 0x100000;

            // Length of the payload (4 bytes for the protocol version)
            ushort payloadLength = 4;

            WriteHeader(buffer, payloadLength, messageType);

            // Calculate CRC32 and override bytes 8-11
            uint crc = System.IO.Hashing.Crc32.HashToUInt32(buffer);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8, 4), crc);
        }

        public static void WritePortsInfoResponse(Span<byte> buffer, byte slotId)
        {
            // Information about connected controllers
            uint messageType = 0x100001;
            ushort payloadLength = 16;

            WriteHeader(buffer, payloadLength, messageType);

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
            buffer[24] = 0x00;
            buffer[25] = 0x11;
            buffer[26] = 0x22;
            buffer[27] = 0x33;
            buffer[28] = 0x44;
            buffer[29] = 0x55;

            // byte 10 "Battery status. See below for possible values."
            // 0x00	Not applicable
            // 0x01    Dying
            // 0x02    Low
            // 0x03    Medium
            // 0x04    High
            // 0x05    Full(or almost)
            // 0xEE    Charging
            // 0xEF    Charged
            byte batteryStatus = 0x05;
            buffer[30] = batteryStatus;

            // Calculate CRC32 and override bytes 8-11
            uint crc = System.IO.Hashing.Crc32.HashToUInt32(buffer);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8, 4), crc);
        }

        public static void WriteControllerDataResponse(Span<byte> buffer, byte slotId, uint packetNumber, ulong timestamp,
    float accX, float accY, float accZ,
    float gyroPitch, float gyroYaw, float gyroRoll)
        {
            // Actual controllers data
            uint messageType = 0x100002;
            ushort payloadLength = 84;

            WriteHeader(buffer, payloadLength, messageType);

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
            buffer[24] = 0x00;
            buffer[25] = 0x11;
            buffer[26] = 0x22;
            buffer[27] = 0x33;
            buffer[28] = 0x44;
            buffer[29] = 0x55;

            // byte 10 "Battery status. See below for possible values."
            // 0x00	Not applicable
            // 0x01    Dying
            // 0x02    Low
            // 0x03    Medium
            // 0x04    High
            // 0x05    Full(or almost)
            // 0xEE    Charging
            // 0xEF    Charged
            byte batteryStatus = 0x05;
            buffer[30] = batteryStatus;

            // byte 11 "Is controller connected (1 if connected, 0 if not)"
            byte isConnected = 1;
            buffer[31] = isConnected;

            // bytes 12-15 "Packet number (for this client)"
            uint packetNumberToWrite = packetNumber;
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(32, 4), packetNumberToWrite);

            // byte 16 "D-Pad Left, D-Pad Down, D-Pad Right, D-Pad Up, Options (?), R3, L3, Share (?)"
            buffer[36] = 0; // No buttons pressed

            // byte 17 "Y, B, A, X, R1, L1, R2, L2"
            buffer[37] = 0; // No buttons pressed

            // byte 18 "HOME Button (0 or 1)"
            byte homeButton = 0;
            buffer[38] = homeButton;

            // byte 19 "Touch Button (0 or 1)"
            byte touchButton = 0;
            buffer[39] = touchButton;

            // byte 20 "Left stick X (plus rightward)"
            byte leftStickX = 128; // Centered
            buffer[40] = leftStickX;

            // byte 21 "Left stick Y (plus upward)"
            byte leftStickY = 128; // Centered
            buffer[41] = leftStickY;

            // byte 22 "Right stick X (plus rightward)"
            byte rightStickX = 128; // Centered
            buffer[42] = rightStickX;

            // byte 23 "Right stick Y (plus upward)"
            byte rightStickY = 128; // Centered
            buffer[43] = rightStickY;

            // bytes 24-47 "Analog D-Pad, Analog buttons, and Touches. All zeroed out."
            buffer.Slice(44, 24).Clear();

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
