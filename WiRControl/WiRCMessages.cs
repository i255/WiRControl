using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;


namespace WiRControl
{
    public enum WiRCCmd
    {
        /** Broadcast Service Discovery Message (BCSD) */
        BCSD = 0x01
        , /** Broadcast Service Advertisement Message (BCSA) */
        BCSA = 0x02
        , /** Transmitter Login Message (TL) */
        TL = 0x11
        , /** Device Configuration Message (DCFG) */
        DCFG = 0x12
        , /** Channel Configuration Message (CCFG) */
        CCFG = 0x13
        , /** Failsafe Configuration Message (FCFG) */
        FCFG = 0x14
        , /** WRCD Startup Message (WST) */
        WST = 0x1A
        , /** Periodical Channel Data Message (PCD) */
        PCD = 0x21
        , /** Periodical Status Data Message (PSD) */
        PSD = 0x22
        , /** WiFi Configuration Message (WCFG) */
        WCFG = 0x31
        , /** Transmitter List Request Message (TLR) */
        TLR = 0x32
        , /** Transmitter List Message (TLST) */
        TLST = 0x33
        , /** Transmitter List End Message (TLEND) */
        TLEND = 0x34
        , /** Access Request Message (AREQ) */
        AREQ = 0x35
        , /** Access Granted Message (AGR) */
        AGR = 0x36
        , /** Firmware Update Message (FWUP) */
        FWUP = 0x37
        , /** Start Stream Message (STST) */
        STST = 0x41
        , /** End Stream Message (EST) */
        EST = 0x42
        , /** Extrenal Out Message (EXTOUT) */
        EXTOUT = 0x50
        , /** Error Message (ERR) */
        ERR = 0xFF
    }

    public class WiRCMessage
    {
        public WiRCCmd Cmd;
        public byte Length;
        public byte[] Data;
        public WiRCMessage(WiRCCmd cmd, params byte[] data)
        {
            Cmd = (WiRCCmd)cmd;
            Data = data;
        }

        public WiRCMessage(byte[] message)
        {
            if (message[0] != 170 || message[1] != 187)
                throw new FormatException("Bad message");

            Cmd = (WiRCCmd)message[2];
            Length = message[3];
            Data = new byte[Length];
            Array.Copy(message, 4, Data, 0, Length);
        }

        public byte[] Serialize()
        {
            var res = new byte[Data.Length + 6];

            res[0] = 170;
            res[1] = 187;
            res[2] = (byte)Cmd;
            res[3] = (byte)Data.Length;
            Data.CopyTo(res, 4);
            AddCRC(res);
            return res;

        }

        static void AddCRC(byte[] buf)
        {
            Crc16Ccitt crc = new Crc16Ccitt(InitialCrcValue.Zeros);
            var sum = crc.ComputeChecksum(buf, 2, buf.Length - 4);
            buf[buf.Length - 2] = (byte)(sum >> 8);
            buf[buf.Length - 1] = (byte)(sum);
        }
    }

    class WiRCMessages
    {
        const ushort Port = 1984;

        static IEnumerable<byte> ToBytes(ushort s)
        {
            return BitConverter.GetBytes(s).Reverse();
        }

        public static WiRCMessage BCSD()
        {
            return new WiRCMessage(WiRCCmd.BCSD, 0, 1, 0);
        }

        public static WiRCMessage TL()
        {
            var name = new byte[64];
            Encoding.ASCII.GetBytes("VoiD").CopyTo(name, 0);
            var data = new byte[] { 0, 1, 0, 255 }
                .Concat(name)
                .Concat(ToBytes(Port))
                .ToArray();

            return new WiRCMessage(WiRCCmd.TL, data);
        }

        public static WiRCMessage CCFG()
        {
            return new WiRCMessage(WiRCCmd.CCFG,
                Enumerable.Repeat((ushort)20000, 12)
                .SelectMany(x => ToBytes(x)).ToArray());
        }

        public static WiRCMessage FCFG()
        {
            return new WiRCMessage(WiRCCmd.FCFG,
                Enumerable.Repeat((ushort)1500, 12)
                .SelectMany(x => ToBytes(x)).ToArray());
        }

        public static WiRCMessage PCD(ushort[] data)
        {
            if (data.Length != 12)
                throw new Exception();

            return new WiRCMessage(WiRCCmd.PCD,
                data.SelectMany(x => ToBytes(x)).ToArray());
        }

        public const ushort CameraPort = 1985;
        public static WiRCMessage STST(byte camNum)
        {
            return new WiRCMessage(WiRCCmd.STST,
                new byte[] { camNum }
                .Concat(ToBytes((ushort)(CameraPort + camNum)))
                .ToArray());
        }

        public static void SendUdp(WiRCMessage msg, IPAddress destIp, ushort destPort, ushort localPort)
        {
            using (UdpClient udpClient = new UdpClient(localPort))
            {
                udpClient.Connect(destIp, destPort);
                var data = msg.Serialize();
                udpClient.Send(data, data.Length);
            }
        }


        public static IPAddress Discover()
        {
#if DEBUG
            throw new Exception("Discover only works if you build for Release");
#else
            var ip = IPAddress.Parse("192.168.1.255");
            SendUdp(WiRCMessages.BCSD(), ip, Port, Port);

            IPEndPoint endp;
            while (ReciveUdp(out endp).Cmd != WiRCCmd.BCSA) ;

            return endp.Address;
#endif
        }

        public static WiRCMessage ReciveUdp(out IPEndPoint remote)
        {
            using (UdpClient udpClient = new UdpClient(Port))
            {
                //IPEndPoint object will allow us to read datagrams sent from any source.
                remote = new IPEndPoint(IPAddress.Any, 0);

                // Blocks until a message returns on this socket from a remote host.
                return new WiRCMessage(udpClient.Receive(ref remote));
            }
        }

        public static NetworkStream ConnectTCP(IPAddress addr)
        {
            TcpClient c = new TcpClient();
            c.Connect(addr, Port);

            return c.GetStream();
        }

        public static void SendTCP(NetworkStream s, WiRCMessage msg)
        {
            var buf = msg.Serialize();
            s.Write(buf, 0, buf.Length);
        }

        public static ulong FromBytes(byte[] buffer, int startIndex, int bytesToConvert, bool bigEndian = false)
        {
            ulong ret = 0;
            if (bigEndian)
                for (int i = 0; i < bytesToConvert; i++)
                    ret = unchecked((ret << 8) | buffer[startIndex + i]);
            else
                for (int i = 0; i < bytesToConvert; i++)
                    ret = unchecked((ret << 8) | buffer[startIndex + bytesToConvert - 1 - i]);
            return ret;
        }

    }
}