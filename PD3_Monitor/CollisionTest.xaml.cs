using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Media.Media3D;

namespace PD3_Monitor
{
    /// <summary>
    /// Interaction logic for CollisionTest.xaml
    /// </summary>
    public partial class CollisionTest : Window
    {
        private DispatcherTimer CrashTimer, FlashTimer;
        double Ax1, Ay1, Az1, Ax, Ay, Az;
        int flash_cnt = 0, crash_part = -1, flash_times = 10;
        Color background_color;
        private String str_collision_values;

        public CollisionTest()
        {
            InitializeComponent();
            MainWindow.filter_type_rd = 2;
            MainWindow.first_detect_rd = true;
            if (MainWindow.filter_threshold_rd == null)
            {
                MainWindow.filter_threshold_rd = new double[] { 0.8, 0.8, 0.2 };
            }
            else
            {
                textBox_collision_threshold.Text = MainWindow.filter_threshold_rd[0].ToString() + "," + MainWindow.filter_threshold_rd[1].ToString() + "," + MainWindow.filter_threshold_rd[2].ToString();
            }
            if (MainWindow.filter_threshold_pry == null)
            {
                MainWindow.filter_threshold_pry = new double[] { 0.1, 0.1, 1.1 };
            }
            MainWindow.is_collision_test = true;
            MainWindow.is_new_filter = true;

            background_color = Colors.Black;
            initial_timer(50, 300);
        }

        private void initial_timer(int ms1, int ms2)
        {
            CrashTimer = new DispatcherTimer();
            CrashTimer.Interval = new TimeSpan(0, 0, 0, 0, ms1);
            CrashTimer.Tick += CrashTimer_Tick;
            CrashTimer.Start();

            FlashTimer = new DispatcherTimer();
            FlashTimer.Interval = new TimeSpan(0, 0, 0, 0, ms2);
            FlashTimer.Tick += FlashTimer_Tick;
            //FlashTimer.Start();
        }

        private void FlashTimer_Tick(object sender, EventArgs e)
        {
            flash_cnt++;            
            if (flash_cnt < flash_times)
            {
                change_crash_part_color(crash_part,false, flash_cnt, false);
            }
            else
            {
                flash_cnt = 0;
                change_crash_part_color(crash_part, true, flash_cnt);
                crash_part = -1;
                FlashTimer.Stop();
                CrashTimer.Start();
            }
        }

        private void CrashTimer_Tick(object sender, EventArgs e)
        {
            if (crash_part == -1)
            {
                set_Measurement_info(Ax, Ay, Az);
                str_collision_values = " " + Ax.ToString() + ", " + Ay.ToString() + ", " + Az.ToString() + "\r\n";
                crash_part = get_collision_part(this.Ax1, this.Ay1, this.Az1);
            }
            if(crash_part!=-1 && flash_cnt==0)
            {
                FlashTimer.Start();
                CrashTimer.Stop();
            }
            /*
            if(flash_cnt >= flash_times)
            {
                flash_cnt = 0;
                FlashTimer.Stop();
                change_crash_part_color(crash_part, true, flash_cnt);
                crash_part = -1;
            }
            */
        }

        private void set_Measurement_info(double Ax2, double Ay2, double Az2)
        {
            textBlock_measurement_info.Text = "Acceleration:  Ax=" + Ax2.ToString() + "  Ay=" + Ay2.ToString() + "  Az=" + Az2.ToString() + " g\r\n";
        }

        private void Collision_test_window_Closed(object sender, EventArgs e)
        {
            MainWindow.collision_test_window = null;
        }

        private void Collision_test_window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainWindow.is_collision_test = false;
        }

        private void button_collision_changethreshold_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.filter_threshold_rd = Convert2Double(textBox_collision_threshold.Text.Trim());
            MainWindow.is_new_filter = true;
            MessageBox.Show("Threshold has been changed");
        }

        private void button_topmost_Click(object sender, RoutedEventArgs e)
        {
            this.Topmost = !this.Topmost;
        }

        private void button_clear_Click(object sender, RoutedEventArgs e)
        {
            textBox_collision_record.Text = "";
        }

        public void set_Value(double Ax1, double Ay1, double Az1, double Ax, double Ay, double Az)
        {
            this.Ax1 = Ax1;
            this.Ay1 = Ay1;
            this.Az1 = Az1;

            this.Ax = Ax;
            this.Ay = Ay;
            this.Az = Az;
        }

        private void change_crash_part_color(int crash_part_id, bool reset = false, int cnt = 0, bool enable_flash = true)
        {
            Color bgr_color = Colors.OrangeRed;  
            if (enable_flash)
            {
                bgr_color = (cnt % 2) != 0 ? Colors.OrangeRed : background_color;
            }
            if (reset)
            {
                bgr_color = background_color;
            }
            switch (crash_part_id)
            {
                case 0:
                    {
                        sp_topleft.Background = new SolidColorBrush(bgr_color);
                        break;
                    }
                case 1:
                    {
                        sp_top.Background = new SolidColorBrush(bgr_color);
                        break;
                    }
                case 2:
                    {
                        sp_topright.Background = new SolidColorBrush(bgr_color);
                        break;
                    }
                case 3:
                    {
                        sp_left.Background = new SolidColorBrush(bgr_color);
                        break;
                    }
                case 4:
                    {
                        sp_right.Background = new SolidColorBrush(bgr_color);
                        break;
                    }
                case 5:
                    {
                        sp_bottomleft.Background = new SolidColorBrush(bgr_color);
                        break;
                    }
                case 6:
                    {
                        sp_bottom.Background = new SolidColorBrush(bgr_color);
                        break;
                    }
                case 7:
                    {
                        sp_bottomright.Background = new SolidColorBrush(bgr_color);
                        break;
                    }
            }
        }

        private int get_collision_part(double Ax1, double Ay1, double Az1)
        {
            Vector vc = new Vector(Ax1, Ay1);
            String str_crash_time = "\r\n" + DateTime.Now.ToShortDateString() + "  " + DateTime.Now.ToLongTimeString() + "   ";
            if (vc == new Vector(1, 1))
            {
                // top left
                textBox_collision_record.Text = (str_crash_time + "top left:" + str_collision_values) + textBox_collision_record.Text;
                return 0;
            }
            if (vc == new Vector(1, 0))
            {
                // top
                textBox_collision_record.Text = (str_crash_time + "top:"+ str_collision_values) + textBox_collision_record.Text;
                return 1;
            }
            if (vc == new Vector(1, -1))
            {
                // top right
                textBox_collision_record.Text = (str_crash_time + "top right:" + str_collision_values) + textBox_collision_record.Text;
                return 2;
            }
            if (vc == new Vector(0, 1))
            {
                // left
                textBox_collision_record.Text = (str_crash_time + "left:" + str_collision_values) + textBox_collision_record.Text;
                return 3;
            }
            if (vc == new Vector(0, -1))
            {
                // right
                textBox_collision_record.Text = (str_crash_time + "right:" + str_collision_values) + textBox_collision_record.Text;
                return 4;
            }
            if (vc == new Vector(-1, 1))
            {
                // bottom left
                textBox_collision_record.Text = (str_crash_time + "bottom left:" + str_collision_values) + textBox_collision_record.Text;
                return 5;
            }
            if (vc == new Vector(-1, 0))
            {
                // bottom
                textBox_collision_record.Text = (str_crash_time + "bottom:" + str_collision_values) + textBox_collision_record.Text;
                return 6;
            }
            if (vc == new Vector(-1, -1))
            {
                // bottom right
                textBox_collision_record.Text = (str_crash_time + "bottom right:" + str_collision_values) + textBox_collision_record.Text;
                return 7;
            }
            return -1;
        }

        private Vector3D get_force(double Ax, double Ay, double Az, double mass, double g)
        {
            return new Vector3D(mass * Ax * g, mass * Ay * g, mass * Az * g);
        }

        private double[] Convert2Double(String str)
        {
            String[] strs = str.Split(new string[] { "," }, StringSplitOptions.None);
            double[] dbs = new double[strs.Length];
            for (int i = 0; i < strs.Length; i++)
            {
                dbs[i] = Convert.ToDouble(strs[i].Trim());
            }
            return dbs;
        }
    }
}
