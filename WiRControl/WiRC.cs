using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WiRControl
{
    class WiRC
    {
        bool disposed = false;
        public bool InControl = false;
        Task Listener, TCPListener;
        public IPAddress Address = null;
        NetworkStream TcpStream;
        public int SenderId = -1;
        public int CamsNumber = 0;
        ushort[] ChannelData = new ushort[12];
        ushort PCDPort;
        bool updateChannelData = false;
        DateTime _watchdogTime;

        const int minVal = 800;
        const int maxVal = 2200;

        public event Action<int, Image> PictureRecieved;

        public void SetChannel(int channel, float val)
        {
            if (val < -1 || val > 1)
                throw new Exception();

            ChannelData[channel] = (ushort)((val + 1f) / 2 * (maxVal - minVal) + minVal);
            updateChannelData = true;
        }

        public void Connect()
        {
            for (int i = 0; i < ChannelData.Length; i++)
                ChannelData[i] = 1500;

            if (Address == null)
                Address = WiRCMessages.Discover();

            TCPListener = Task.Run(() => StartTCPListener());
            Listener = Task.Run(() => StartUdpListener());

            ConnectTCP();
            //while (SenderId < 0)
            //    Thread.Sleep(10);

            //WiRCMessages.SendTCP(TcpStream, new WiRCMessage(WiRCCmd.AREQ, (byte)SenderId));

            //while (!_inControl)
            //    Thread.Sleep(10);

            Task.Run(() => StartControlLoop());
            Task.Run(() => StartWatchdogLoop());

            Task.Run(() => StartCameraLoop(0));


        }

        private void ConnectTCP()
        {
            TcpStream = WiRCMessages.ConnectTCP(Address);

            WiRCMessages.SendTCP(TcpStream, WiRCMessages.TL());
            WiRCMessages.SendTCP(TcpStream, WiRCMessages.CCFG());
            WiRCMessages.SendTCP(TcpStream, WiRCMessages.FCFG());
            WiRCMessages.SendTCP(TcpStream, WiRCMessages.STST(0));
            Console.WriteLine("Connected");

        }

        public void Disconnect()
        {
            disposed = true;
            WiRCMessages.SendTCP(TcpStream, new WiRCMessage(WiRCCmd.EST, 0));
        }


        private void StartWatchdogLoop()
        {
            while (!disposed)
                if ((_watchdogTime == default(DateTime) ||
                    (DateTime.Now - _watchdogTime).TotalMilliseconds < 500))
                    Thread.Sleep(100);
                else if (!disposed)
                {
                    _watchdogTime = default(DateTime);
                    ConnectTCP(); // try to reconnect on connection loss
                }
        }

        private void StartControlLoop()
        {
            while (!disposed)
            {
                updateChannelData = false;
                WiRCMessages.SendUdp(WiRCMessages.PCD(ChannelData), Address, PCDPort, 1990);
                var dt = DateTime.Now;
                while (!updateChannelData && (DateTime.Now - dt).TotalMilliseconds < 500)
                    Thread.Sleep(10);
            }
        }

        bool running;
        public string VideoPath;//= @"d:\ttt\jpeg3";
        private void StartCameraLoop(int camId)
        {
            int cntr = 0;
            using (UdpClient udpClient = new UdpClient(WiRCMessages.CameraPort + camId))
            {
                while (!disposed)
                {
                    //IPEndPoint object will allow us to read datagrams sent from any source.
                    var remote = new IPEndPoint(IPAddress.Any, 0);

                    // Blocks until a message returns on this socket from a remote host.
                    var buf = udpClient.Receive(ref remote);
                    _watchdogTime = DateTime.Now;

                    if (buf.Length == 0)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    var length = (int)WiRCMessages.FromBytes(buf, 12, 4, true);

                    if (VideoPath != null)
                        using (var f = File.OpenWrite(Path.Combine(VideoPath, "pic" + (cntr++).ToString("00000") + ".jpeg")))
                            f.Write(buf, 16, length);

                    var b = Bitmap.FromStream(new MemoryStream(buf, 16, length));

                    if (!running)
                    {
                        running = true;
                        Task.Run(() =>
                        {
                            try
                            {
                                if (PictureRecieved != null)
                                    PictureRecieved(camId, b);
                            }
                            finally { running = false; }
                        });
                    }
                }
            }
        }

        private void StartTCPListener()
        {
            Thread.Sleep(50);
            var buf = new byte[1024];
            while (!disposed)
            {
                var bytes = TcpStream.Read(buf, 0, buf.Length);
                if (bytes == 0)
                {
                    Thread.Sleep(50);
                    continue;
                }

                var msg = new WiRCMessage(buf);
                Console.WriteLine(msg.Cmd + " " + msg.Length + " of " + bytes);
                switch (msg.Cmd)
                {
                    case WiRCCmd.WST:
                        SenderId = msg.Data[0];
                        Console.WriteLine("Sender " + SenderId);
                        CamsNumber = msg.Data[1];
                        PCDPort = (ushort)((msg.Data[2] << 8) + msg.Data[3]);
                        break;
                    case WiRCCmd.AGR:
                        Console.WriteLine("AGR " + msg.Data[66]);
                        if (msg.Data[66] == 0)
                            InControl = true;
                        break;
                    default:
                        Console.Write(BitConverter.ToString(msg.Data));
                        break;
                }
            }
        }

        void StartUdpListener()
        {
            IPEndPoint endpoint;
            while (!disposed)
            {
                var msg = WiRCMessages.ReciveUdp(out endpoint);
                Console.WriteLine(msg.Cmd);
                switch (msg.Cmd)
                {
                    case WiRCCmd.BCSD:
                        // this is our own packet
                        break;
                    case WiRCCmd.BCSA:
                        Address = endpoint.Address;
                        break;
                    default:
                        Console.Write(BitConverter.ToString(msg.Data));
                        break;
                }
            }
        }

    }

}
