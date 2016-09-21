using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PD3_Monitor
{
    static class PacketParser
    {
        // return 'who am i' indicatior
        public static byte getWhoAmI( byte[] streamBytes)
        {
            return streamBytes[0];
        }
        // return 'accelerometer range'
        public static int getAccelRange(byte[] streamBytes)
        {
            int range = 2;
            switch (streamBytes[1])
            {
                case 0x00:
                    {
                        range = 2;
                        break;
                    }
                case 0x08:
                    {
                        range = 4;
                        break;
                    }
                case 0x10:
                    {
                        range = 8;
                        break;
                    }
                case 0x18:
                    {
                        range = 16;
                        break;
                    }
            }

            return range;
        }
        // return 'gyroscope range'
        public static int getGyroRange(byte[] streamBytes)
        {
            int range = 250;
            switch (streamBytes[2] & 0x18)
            {
                case 0x00:
                    {
                        range = 250;
                        break;
                    }
                case 0x08:
                    {
                        range = 500;
                        break;
                    }
                case 0x10:
                    {
                        range = 1000;
                        break;
                    }
                case 0x18:
                    {
                        range = 2000;
                        break;
                    }
            }

            return range;
        }
        // return 'accelerometer value'
        public static double getAccel(byte[] streamBytes, string str_xyz, int full_scale)
        {
            int idx = 3;
            switch (str_xyz)
            {
                case "X":
                case "x":
                    {
                        idx = 3;
                        break;
                    }
                case "Y":
                case "y":
                    {
                        idx = 5;
                        break;
                    }
                case "Z":
                case "z":
                    {
                        idx = 7;
                        break;
                    }
            }
            sbyte byte_h = (sbyte)streamBytes[idx];
            byte byte_l = streamBytes[idx + 1];

            Int32 raw_value = (byte_h << 8) | byte_l;
            return raw_value * 1.0 * full_scale / 32768.0;
        }
        // return 'gyroscope value'
        public static double getGyro(byte[] streamBytes, string str_xyz, int full_scale)
        {
            int idx = 9;
            switch (str_xyz)
            {
                case "X":
                case "x":
                    {
                        idx = 9;
                        break;
                    }
                case "Y":
                case "y":
                    {
                        idx = 11;
                        break;
                    }
                case "Z":
                case "z":
                    {
                        idx = 13;
                        break;
                    }
            }
            sbyte byte_h = (sbyte)streamBytes[idx];
            byte byte_l = streamBytes[idx + 1];

            Int32 raw_value = (byte_h << 8) | byte_l;
            return raw_value * 1.0 * full_scale / 32750.0;
        }
    }
}
