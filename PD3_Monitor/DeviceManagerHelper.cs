using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PD3_Monitor
{
    class DeviceManagerHelper
    {
        // Device ids
        const int DEVICE_MPU6500 = 1;
        const int DEVICE_LIS3DH = 2;
        const int DEVICE_ADLX263 = 3;

        // UI properties
        public bool enable_Accelerometer_setting_panel;
        public bool enable_Gyroscope_setting_panel;
        public string[] accelerometer_full_scales;
        public string[] gyroscope_full_scales;
        public bool enable_Click_Sensitivity;

        // Function properties
        public bool boolAcceleration;
        public bool boolGyroscope;
        public bool boolTemperature;
        public bool boolIgnoreGravity = false;

        // Common device values
        public double device_temperature;

        public int device_type = 1;
        public DeviceManagerHelper(int device_type)
        {
            this.device_type = device_type;
            set_device_property();
        }

        private void set_device_property()
        {
            switch (this.device_type)
            {
                case DEVICE_LIS3DH:
                    {
                        this.enable_Accelerometer_setting_panel = true;
                        this.enable_Gyroscope_setting_panel = false;
                        this.accelerometer_full_scales = new string[] { "2","4","8","16"};
                        this.enable_Click_Sensitivity = true;
                        this.boolAcceleration = true;
                        this.boolGyroscope = false;
                        this.boolTemperature = false;
                        break;
                    }
                case DEVICE_MPU6500:
                    {
                        this.enable_Accelerometer_setting_panel = true;
                        this.enable_Gyroscope_setting_panel = true;
                        this.accelerometer_full_scales = new string[] { "2", "4", "8", "16" };
                        this.gyroscope_full_scales = new string[] { "250", "500", "1000", "2000" };
                        this.enable_Click_Sensitivity = false;
                        this.boolAcceleration = true;
                        this.boolGyroscope = true;
                        this.boolTemperature = true;
                        break;
                    }
                case DEVICE_ADLX263:
                    {
                        this.enable_Accelerometer_setting_panel = true;
                        this.enable_Gyroscope_setting_panel = false;
                        this.accelerometer_full_scales = new string[] { "2", "4", "8", "16" };
                        this.enable_Click_Sensitivity = false;
                        this.boolAcceleration = true;
                        this.boolGyroscope = false;
                        this.boolTemperature = false;
                        break;
                    }
            }
        }
    }
}
