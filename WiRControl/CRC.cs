using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WiRControl
{
    internal enum InitialCrcValue
    {
        Zeros,
        NonZero1 = 65535,
        NonZero2 = 7439
    }

    internal class Crc16Ccitt
    {
        private const ushort poly = 4129;
        private ushort[] table = new ushort[256];
        private ushort initialValue = 0;
        public ushort ComputeChecksum(byte[] bytes, int from, int len)
        {
            ushort crc = this.initialValue;
            for (int i = 0; i < len; i++)
            {
                crc = (ushort)((int)crc << 8 ^ (int)this.table[crc >> 8 ^ (int)(255 & bytes[i + from])]);
            }
            return crc;
        }

        public Crc16Ccitt(InitialCrcValue initialValue)
        {
            this.initialValue = (ushort)initialValue;
            for (int i = 0; i < this.table.Length; i++)
            {
                ushort temp = 0;
                ushort a = (ushort)(i << 8);
                for (int j = 0; j < 8; j++)
                {
                    if (((temp ^ a) & 32768) != 0)
                    {
                        temp = (ushort)((int)temp << 1 ^ 4129);
                    }
                    else
                    {
                        temp = (ushort)(temp << 1);
                    }
                    a = (ushort)(a << 1);
                }
                this.table[i] = temp;
            }
        }
    }
}
