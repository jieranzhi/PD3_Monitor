using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.IO.Ports;
using System.Collections;
using System.IO;
using HelixToolkit.Wpf;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using System.Windows.Media.Animation;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace PD3_Monitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Global Variables
        // Device manager
        DeviceManagerHelper DMH = null;
        Accelerometer ACM = null;
        Gyroscope GYR = null;

        // Serial Port
        public SerialPort port = null;
        // Auto save data log
        private const string AUTO_SAVE_PATH = "auto_save.txt";
        // 3D model file path
        private const string MODEL_PATH = "models\\su-37\\Su-37_Terminator.obj";
       
        // Flag indicates stop/start the serial commnication
        private bool stopFlag = true;
        
        // Timer for auto rotate
        private DispatcherTimer dispatcherTimer, dispatcherTimer2, dispatcherTimer_scanning, dispatcherTimer_ble_streaming;
        
        // Data list for charting the acceleration
        ObservableDataSource<Point> source_Ax = null;
        ObservableDataSource<Point> source_Ay = null;
        ObservableDataSource<Point> source_Az = null;
        ObservableDataSource<Point> source_gx = null;
        ObservableDataSource<Point> source_gy = null;
        ObservableDataSource<Point> source_gz = null;
        ObservableDataSource<Point> source_ox = null;
        ObservableDataSource<Point> source_oy = null;
        ObservableDataSource<Point> source_oz = null;
        ObservableDataSource<Point> source_ow = null;

        // story board for animations
        Storyboard myStoryboard = null;
        
        // pitch, roll, yaw of the 3d model
        double global_pitch, global_roll, global_yaw;

        // source
        enum DataSource
        {
            DS_SERIAL,
            DS_BLE,
        };

        DataSource DS = DataSource.DS_SERIAL;

        /// <summary>
        /// Filters
        /// </summary>
        Filters Filter_X, Filter_Y, Filter_Z;
        Filters Filter_GX, Filter_GY, Filter_GZ;
        Filters Filter_Pitch, Filter_Roll, Filter_Yaw;
        public static int filter_type_rd = 0, filter_length_rd = 10;
        public static int filter_type_pry = 0, filter_length_pry = 10;
        public static int filter_type_gd = 0, filter_length_gd = 10;
        public static double[] filter_threshold_rd, filter_threshold_pry, filter_threshold_gd;
        public static bool is_new_filter = false, is_reverse_rd = false, is_reverse_pry = false, is_reverse_gd = false, first_detect_rd = false, first_detect_pry = false, first_detect_gd = false, is_locked_rd = false, is_locked_gd = false, is_locked_pry = false, is_collision_test = false;
       
        // collision test
        public static CollisionTest collision_test_window;
        public static FilterWindow FilterWindow;
       
        // 3d view
        GridLinesVisual3D glinesv3d;
        LinesVisual3D trajectory = new LinesVisual3D();
        string app_dir;
        // Bluetooth
        bgtask ble_task = null;
        string str_selected_device = "";

        // source
        enum SystemState
        {
            SS_START, // start tracing trajectory
            SS_IDLE, // stop tracing trajectory
            SS_RSSI, // mapping rssi
        };

        SystemState SS = SystemState.SS_IDLE;

        #endregion

        public MainWindow()
        {
            InitializeComponent(); 
        }

        private void Window_monitor_Loaded(object sender, RoutedEventArgs e)
        {
            app_dir = Environment.CurrentDirectory;

            this.Cursor = new Cursor(app_dir + "\\Busy.ani");

            SetPort_in_ComboBox();
            Setup3D_Display(MODEL_PATH);
            reset_3D_view(-65, 0, 0, 0);

            initial_linegraph();

            ACM = new Accelerometer();
            GYR = new Gyroscope();

            initial_timer(1, 50);

            DMH = new DeviceManagerHelper(1);
            setup_ui_accrodding_to_DMH();
            // start display camera info
            dispatcherTimer2.Start();
        }

        #region Common-use functions and events

        /// <summary>
        /// create an animation that controls the value changes from 'from' to 'to'
        /// </summary>
        /// <param name="from">start value</param>
        /// <param name="to">finish value</param>
        /// <param name="duration">animation duration</param>
        /// <returns>animation</returns>
        private DoubleAnimation create_Animation(double from, double to, int duration)
        {
            DoubleAnimation myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.From = from;
            myDoubleAnimation.To = to;

            myDoubleAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(duration));

            return myDoubleAnimation;
        }

        /// <summary>
        /// apply animation to the object by name
        /// </summary>
        /// <param name="myDoubleAnimation">animation</param>
        /// <param name="ctrl_name">object name</param>
        private void Apply_Animation(DoubleAnimation myDoubleAnimation, string ctrl_name, PropertyPath property_path)
        {
            myStoryboard = new Storyboard();
            myStoryboard.Children.Add(myDoubleAnimation);
            Storyboard.SetTargetName(myDoubleAnimation, ctrl_name);
            Storyboard.SetTargetProperty(myDoubleAnimation, property_path);

        }

        /// <summary>
        /// swich to serial communication
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void radioButton_serial_Checked(object sender, RoutedEventArgs e)
        {
            DS = DataSource.DS_SERIAL;
            // set serial port to the comboBox
            EnableSerial_Parameter(true);
            try
            {
                SetPort_in_ComboBox();
            }
            catch
            {
                return;
            }
        }

        /// <summary>
        /// switch to bluetooth communication
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void radioButton_bluetooth_Checked(object sender, RoutedEventArgs e)
        {
            DS = DataSource.DS_BLE;
            EnableSerial_Parameter(false);
        }

        /// <summary>
        /// start or end communication from selected source
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_STARTEND_Click(object sender, RoutedEventArgs e)
        {
            bool success = false;
            if (button_STARTEND.Content.Equals("START"))
            {
                EnableSerial_Bluetooth(false);
                // START serial communication
                success = InitialSerialPort();

                if (success)
                {
                    button_STARTEND.Content = "STOP";
                    if(DS == DataSource.DS_SERIAL)
                        Grid_main.ColumnDefinitions[0].Width = new GridLength(0, GridUnitType.Pixel);
                }
            }
            else
            {
                if (radioButton_serial.IsChecked == true)
                {
                    // STOP serial communication
                    DisposeSerialPort();
                }
                else if (radioButton_bluetooth.IsChecked == true)
                {
                    // STOP bluetooth communication
                }
                stopFlag = true;
                button_STARTEND.Content = "START";
                EnableSerial_Bluetooth(true);
            }
        }

        /// <summary>
        /// show/hide the control panel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_dock_Click(object sender, RoutedEventArgs e)
        {
            button_dock.Content = button_dock.Content.Equals("<") ? ">" : "<";
            //DockPanel_Control.Width = (DockPanel_Control.Width==0)?183:0;
            Grid_main.ColumnDefinitions[0].Width = (Grid_main.ColumnDefinitions[0].Width.Value == 0) ? new GridLength(183, GridUnitType.Pixel) : new GridLength(0, GridUnitType.Pixel);
        }

        // choose different models
        private void image_plane_MouseEnter(object sender, MouseEventArgs e)
        {
            image_plane.Opacity = 0.9;
        }

        private void image_plane_MouseLeave(object sender, MouseEventArgs e)
        {
            image_plane.Opacity = 0.2;
        }

        private void image_plane_MouseUp(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog opfd = new OpenFileDialog();
            opfd.Filter = "Model Files(*.3ds, *.lwo, *.stl, *.obj) | *.3ds; *.lwo; *.stl; *.obj";
            if (opfd.ShowDialog() == true)
            {
                Setup3D_Display(opfd.FileName);
            }
        }

        // initialization of line serial graph 
        private void initial_linegraph()
        {
            // Create first source
            source_Ax = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source_Ax.SetXYMapping(p => p);

            // Create second source
            source_Ay = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source_Ay.SetXYMapping(p => p);

            // Create third source
            source_Az = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source_Az.SetXYMapping(p => p);

            // Create third source
            source_Az = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source_Az.SetXYMapping(p => p);

            // Create gyro_x source
            source_gx = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source_gx.SetXYMapping(p => p);

            // Create third source
            source_gy = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source_gy.SetXYMapping(p => p);

            // Create gyro_z source
            source_gz = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source_gz.SetXYMapping(p => p);

            // Create gyro_z source
            source_ox = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source_ox.SetXYMapping(p => p);

            // Create gyro_z source
            source_oy = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source_oy.SetXYMapping(p => p);

            // Create gyro_z source
            source_oz = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source_oz.SetXYMapping(p => p);

            // Create gyro_z source
            source_ow = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source_ow.SetXYMapping(p => p);

            // Add all three graphs. Colors are not specified and chosen random
            plotter.AddLineGraph(source_Ax, Colors.Aquamarine, 1.5, "Ax");
            plotter.AddLineGraph(source_Ay, Colors.RoyalBlue, 1.5, "Ay");
            plotter.AddLineGraph(source_Az, Colors.Salmon, 1.5, "Az");
            plotter.Legend.Visibility = Visibility.Hidden;
            plotter.Legend.Foreground = new SolidColorBrush(Colors.Black);
            plotter.Legend.LegendTop = 2;
            plotter.Legend.LegendLeft = 2;
            plotter.Legend.Opacity = 0.9;
            plotter.AxisGrid.Visibility = Visibility.Hidden;

            plotter_gyro.AddLineGraph(source_gx, Colors.Aquamarine, 1.5, "Gyro_x");
            plotter_gyro.AddLineGraph(source_gy, Colors.RoyalBlue, 1.5, "Gyro_y");
            plotter_gyro.AddLineGraph(source_gz, Colors.Salmon, 1.5, "Gyro_z");
            plotter_gyro.Legend.Visibility = Visibility.Hidden;
            plotter_gyro.Legend.Foreground = new SolidColorBrush(Colors.Black);
            plotter_gyro.Legend.LegendTop = 2;
            plotter_gyro.Legend.LegendLeft = 2;
            plotter_gyro.Legend.Opacity = 0.9;
            plotter_gyro.AxisGrid.Visibility = Visibility.Hidden;

            plotter_orientation.AddLineGraph(source_ox, Colors.Aquamarine, 1.5, "Orien_x");
            plotter_orientation.AddLineGraph(source_oy, Colors.RoyalBlue, 1.5, "Orien_y");
            plotter_orientation.AddLineGraph(source_oz, Colors.Salmon, 1.5, "Orien_z");
            plotter_orientation.AddLineGraph(source_ow, Colors.LightPink, 1.5, "Orien_w");
            plotter_orientation.Legend.Visibility = Visibility.Hidden;
            plotter_orientation.Legend.Foreground = new SolidColorBrush(Colors.Black);
            plotter_orientation.Legend.LegendTop = 2;
            plotter_orientation.Legend.LegendLeft = 2;
            plotter_orientation.Legend.Opacity = 0.9;
            plotter_orientation.AxisGrid.Visibility = Visibility.Hidden;
        }

        private void plotter_MouseEnter(object sender, MouseEventArgs e)
        {
            plotter.Legend.Visibility = Visibility.Visible;
            plotter_gyro.Legend.Visibility = Visibility.Visible;
            plotter_orientation.Legend.Visibility = Visibility.Visible;
        }

        private void plotter_MouseLeave(object sender, MouseEventArgs e)
        {
            plotter.Legend.Visibility = Visibility.Hidden;
            plotter_gyro.Legend.Visibility = Visibility.Hidden;
            plotter_orientation.Legend.Visibility = Visibility.Hidden;
        }

        private void plotter_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                source_Ax.Collection.Clear();
                source_Ay.Collection.Clear();
                source_Az.Collection.Clear();
                source_gx.Collection.Clear();
                source_gy.Collection.Clear();
                source_gz.Collection.Clear();
            }
        }

        // show and hide line graph
        private void image_showplot_MouseEnter(object sender, MouseEventArgs e)
        {
            image_showplot.Opacity = 0.9;
        }

        private void image_showplot_MouseLeave(object sender, MouseEventArgs e)
        {
            image_showplot.Opacity = 0.2;
        }

        private void image_showplot_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if(plotter_grid.IsVisible == true)
            {
                plotter_grid.Visibility = Visibility.Hidden;
            }
            else
            {
                plotter_grid.Visibility = Visibility.Visible;
            }
        }

        // Reset 3D view
        private void image_reset3dview_MouseEnter(object sender, MouseEventArgs e)
        {
            image_reset3dview.Opacity = 0.9;
        }

        private void image_reset3dview_MouseLeave(object sender, MouseEventArgs e)
        {
            image_reset3dview.Opacity = 0.2;
        }

        private void image_reset3dview_MouseUp(object sender, MouseButtonEventArgs e)
        {
            reset_3D_view(-65, 0, 0, 1500);
        }

        // save data
        private void image_savedata_MouseEnter(object sender, MouseEventArgs e)
        {
            image_savedata.Opacity = 0.9;
        }

        private void image_savedata_MouseLeave(object sender, MouseEventArgs e)
        {
            image_savedata.Opacity = 0.2;
        }

        private void image_savedata_MouseUp(object sender, MouseButtonEventArgs e)
        {
            SaveFileDialog opfd = new SaveFileDialog();
            opfd.Filter = "Data Files(*.txt, *.log) | *.txt; *.log";
            if (opfd.ShowDialog() == true)
            {
                string dir = opfd.FileName;
                Savedata(dir,0);
            }
        }
        /// <summary>
        /// save data to file
        /// </summary>
        /// <param name="dir"></param>
        private void Savedata(string dir, double type = 0)
        {
            int cnt = source_Ax.Collection.Count;

            Point[] ptAx = new Point[cnt];
            Point[] ptAy = new Point[cnt];
            Point[] ptAz = new Point[cnt];

            Point[] ptGx = new Point[cnt];
            Point[] ptGy = new Point[cnt];
            Point[] ptGz = new Point[cnt];

            source_Ax.Collection.CopyTo(ptAx, 0);
            source_Ay.Collection.CopyTo(ptAy, 0);
            source_Az.Collection.CopyTo(ptAz, 0);
   

            String all_text = "";
            for (int i = 0; i < cnt; i++)
            {
                String str_column = ptAx[i].X.ToString() + "\t" + ptAx[i].Y.ToString() + "\t" + ptAy[i].Y.ToString() + "\t" + ptAz[i].Y.ToString()+"\t"
                    + ptGx[i].Y.ToString() + "\t" + ptGy[i].Y.ToString() + "\t" + ptGz[i].Y.ToString() + "\r\n";
                all_text += str_column;
            }
            if (type == 0)
            {
                File.WriteAllText(dir, all_text);
            }
            else
            {
                File.AppendAllText(dir, all_text);
            }
        }

        // show filter window
        private void image_filter_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (is_collision_test)
            {
                MessageBox.Show("Access Denied: In Collision Test Mode");
                return;
            }
            if (FilterWindow == null)
            {
                FilterWindow = new FilterWindow();
                FilterWindow.Show();
            }
            
        }

        private void image_filter_MouseEnter(object sender, MouseEventArgs e)
        {
            image_filter.Opacity = 0.9;
        }

        private void image_filter_MouseLeave(object sender, MouseEventArgs e)
        {
            image_filter.Opacity = 0.2;
        }

        private void image_collision_test_MouseEnter(object sender, MouseEventArgs e)
        {
            image_collision_test.Opacity = 0.9;
        }

        private void image_collision_test_MouseLeave(object sender, MouseEventArgs e)
        {
            image_collision_test.Opacity = 0.2;
        }

        private void image_collision_test_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (FilterWindow != null)
            {
                FilterWindow.Close();
                FilterWindow = null;
            }

            if (collision_test_window == null)
            {
                collision_test_window = new CollisionTest();
            }
            collision_test_window.Show();
        }

        /// <summary>
        /// remove all items in the combobox and rebuild the items
        /// </summary>
        /// <param name="CB"></param>
        /// <param name="items"></param>
        private void setup_combox(ComboBox CB, string[] items)
        {
            CB.Items.Clear();
            foreach (String str_item in items)
            {
                CB.Items.Add(str_item);
            }
            CB.SelectedIndex = 0;
        }

        /// <summary>
        /// set up user ui according to the context in DMH
        /// </summary>
        private void setup_ui_accrodding_to_DMH()
        {
            try
            {
                wrap_panel_acceleramtersetting.Visibility = DMH.enable_Accelerometer_setting_panel ? Visibility.Visible : Visibility.Collapsed;
                wrap_panel_gyroscopesetting.Visibility = DMH.enable_Gyroscope_setting_panel ? Visibility.Visible : Visibility.Collapsed;
                //textBox_collision_sensitivity.IsEnabled = DMH.enable_Click_Sensitivity;
                //button_apply_collision_sen.IsEnabled = DMH.enable_Click_Sensitivity;
                wrap_panel_clk_sensitivity.Visibility = DMH.enable_Click_Sensitivity? Visibility.Visible : Visibility.Collapsed;

                setup_combox(comboBox_FullScale, DMH.accelerometer_full_scales);
                setup_combox(comboBox_GyroFullScale, DMH.gyroscope_full_scales);
            }
            catch
            {
                return;
            }
        }

        private void comboBox_hardware_list_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DMH = new DeviceManagerHelper(((ComboBox)sender).SelectedIndex+1);
            setup_ui_accrodding_to_DMH();
        }
        #endregion

        /* /////////////////////////////////////////////////////////////////////////////////////////////
                                   Following Code for Serial Communication
       ///////////////////////////////////////////////////////////////////////////////////////////// */
        #region Serial Communication

        /// <summary>
        /// Enable / Disable the parameter setting controller for Serial Port
        /// </summary>
        /// <param name="tf">true: enable / false: disable</param>
        private void EnableSerial_Parameter(bool tf)
        {
            try
            {
                //comboBox_Port.IsEnabled = tf;
                //comboBox_Baud.IsEnabled = tf;
                //button_refresh_portlist.IsEnabled = tf;
                //wrap_panel_serialsetting.Visibility = tf?Visibility.Visible: Visibility.Collapsed;
                wrap_panel_bluetoothsetting.Visibility = tf ? Visibility.Collapsed : Visibility.Visible;
            }
            catch
            {
                return;
            }
        }

        /// <summary>
        /// Enable / Disable the source selector
        /// </summary>
        /// <param name="tf">true: enable / false: disable</param>
        private void EnableSerial_Bluetooth(bool tf)
        {
            try
            {
                radioButton_serial.IsEnabled = tf;
                radioButton_bluetooth.IsEnabled = tf;
            }
            catch
            {
                return;
            }
        }

        /// <summary>
        /// Initialization of Serial Communication
        /// </summary>
        private bool InitialSerialPort()
        {
            try
            {
                string portName = this.comboBox_Port.SelectedItem.ToString();
                int baud = Convert.ToInt32(((ComboBoxItem)(comboBox_Baud.SelectedItem)).Content.ToString().Trim());
                port = new SerialPort(portName, baud);
                port.Encoding = Encoding.UTF8;
                port.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                port.Open();

                textBlock_info.Text = "Initialization completed";
                stopFlag = false;
                return true;
            }
            catch (Exception ex)
            {
                stopFlag = true;
                EnableSerial_Bluetooth(true);
                MessageBox.Show("Error occurs during serial initialization：" + ex.Message, "Tips");
                return false;
            }
        }

        /// <summary>
        /// Get the COM port list in the System and display in the ComboBox
        /// </summary>
        public void SetPort_in_ComboBox()
        {
            this.comboBox_Port.Items.Clear();
            RegistryKey keyCom = Registry.LocalMachine.OpenSubKey("Hardware\\DeviceMap\\SerialComm");
            if (keyCom != null)
            {
                string[] sSubKeys = keyCom.GetValueNames();
                this.comboBox_Port.Items.Clear();
                foreach (string sName in sSubKeys)
                {
                    string sValue = (string)keyCom.GetValue(sName);
                    this.comboBox_Port.Items.Add(sValue);
                }
                this.comboBox_Port.SelectedIndex = 0;
            }
        }

        // refresh port list
        private void button_refresh_portlist_Click(object sender, RoutedEventArgs e)
        {
            SetPort_in_ComboBox();
        }

        /// <summary>
        /// Called each time when data transition is over
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            if (DS == DataSource.DS_SERIAL)
            {
                data_process_from_serial();
            }
            else
            {
                data_process_from_ble();
            }
        }

        private void data_process_from_serial()
        {
            if (!stopFlag)
            {
                try
                {
                    string value = this.ReadSerialData();
                    String[] strs_port_value = GetSerialData(value);

                    if (DMH.boolAcceleration)
                    {
                        double acc_range = Convert.ToDouble(strs_port_value[0]);
                        double Ax = Convert.ToDouble(strs_port_value[1]);
                        double Ay = Convert.ToDouble(strs_port_value[2]);
                        double Az = Convert.ToDouble(strs_port_value[3]);
                        string str_click = strs_port_value[4];

                        ACM.update_value(acc_range, Ax, Ay, Az, str_click);

                        if (DMH.boolIgnoreGravity)
                        {
                            Az = Az - 1;
                        }

                        if (filter_type_rd != 0)
                        {
                            if (is_new_filter)
                            {
                                initial_filters(1, filter_type_rd, filter_length_rd, filter_threshold_rd, is_reverse_rd);
                                initial_filters(2, filter_type_pry, filter_length_pry, filter_threshold_pry, is_reverse_pry);
                                // initial_filters(3, filter_type_gd, filter_length_gd, filter_threshold_gd, is_reverse_gd);
                                is_new_filter = false;
                            }

                            double Ax1 = applyFilter(filter_type_rd, ref Filter_X, filter_length_rd, Ax, first_detect_rd, ref is_locked_rd, is_collision_test);
                            double Ay1 = applyFilter(filter_type_rd, ref Filter_Y, filter_length_rd, Ay, first_detect_rd, ref is_locked_rd, is_collision_test);
                            double Az1 = applyFilter(filter_type_rd, ref Filter_Z, filter_length_rd, Az, first_detect_rd, ref is_locked_rd, is_collision_test);

                            AddAcceleration2Chart(Ax1, Ay1, Az1);
                            #region TEST
                            /*
                            switch (filter_type_rd)
                            {
                                case 1:
                                    {
                                        // simple moving average filter
                                        Filter_X.simple_moving_average_filter.add_Sample(Ax);
                                        Filter_Y.simple_moving_average_filter.add_Sample(Ay);
                                        Filter_Z.simple_moving_average_filter.add_Sample(Az);

                                        if (Filter_X.simple_moving_average_filter.data_cnt >= filter_length_rd)
                                        {
                                            Ax = Filter_X.simple_moving_average_filter.get_Value();
                                            Ay = Filter_Y.simple_moving_average_filter.get_Value();
                                            Az = Filter_Z.simple_moving_average_filter.get_Value();
                                        }

                                        AddAcceleration2Chart(Ax, Ay, Az);
                                        break;
                                    }
                                case 2:
                                    {
                                        // zero-one filter
                                        double Ax1 = Filter_X.zerone_filter.get_Value(Ax);
                                        double Ay1 = Filter_Y.zerone_filter.get_Value(Ay);
                                        double Az1 = Filter_Z.zerone_filter.get_Value(Az);

                                        if (first_detect_rd)
                                        {
                                            DateTime tn = DateTime.Now;
                                            double tspan = Filter_X.zerone_filter.get_timespan(tn);
                                            if (!is_locked_rd)
                                            {
                                                if (Ax1 == 1 || Ay1 == 1 || Az1 == 1)
                                                {
                                                    if (tspan > 1000)
                                                    {
                                                        Filter_X.zerone_filter.lock_value(Ax1, tn);
                                                        Filter_Y.zerone_filter.lock_value(Ay1, tn);
                                                        Filter_Z.zerone_filter.lock_value(Az1, tn);
                                                        is_locked_rd = true;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Ax1 = Filter_X.zerone_filter.get_lock_Value();
                                                Ay1 = Filter_Y.zerone_filter.get_lock_Value();
                                                Az1 = Filter_Z.zerone_filter.get_lock_Value();
                                                if (tspan > 1000)
                                                {
                                                    is_locked_rd = false;
                                                }
                                            }

                                            if (is_collision_test)
                                            {
                                                collision_test_window.set_Value(Ax1, Ay1, Az1, Ax, Ay, Az);
                                            }
                                        }
                                        AddAcceleration2Chart(Ax1, Ay1, Az1);

                                        break;
                                    }
                            }
                            */
                            #endregion
                        }
                        else
                        {
                            AddAcceleration2Chart(Ax, Ay, Az);
                        }
                    }

                    if (DMH.boolGyroscope)
                    {
                        double gyro_range = Convert.ToDouble(strs_port_value[5]);
                        double Gx = Convert.ToDouble(strs_port_value[6]);
                        double Gy = Convert.ToDouble(strs_port_value[7]);
                        double Gz = Convert.ToDouble(strs_port_value[8]);

                        GYR.update_value(gyro_range, Gx, Gy, Gz);

                        AddGyro2Chart(Gx, Gy, Gz);
                    }

                    if (DMH.boolTemperature)
                    {
                        DMH.device_temperature = Convert.ToDouble(strs_port_value[9]);
                    }

                }
                catch (Exception ex)
                {
                    return;
                }
            }
        }

        private void data_process_from_ble()
        {
            try
            {
                //string strs = serial_port.ReadExisting();
                //setText(strs);
                Byte[] inData = new Byte[port.BytesToRead];

                // read all available bytes from serial port in one chunk
                port.Read(inData, 0, port.BytesToRead);

                // parse all bytes read through BGLib parser
                for (int i = 0; i < inData.Length; i++)
                {
                    ble_task.bglib.Parse(inData[i]);
                }

                // DEBUG: display bytes read
                //setText(String.Format("<= RX ({0}) [ {1}]", inData.Length, ByteArrayToHexString(inData)));
            }
            catch
            {
            }
        }

        /// <summary>
        ///  translate the data from the serial port to str array
        /// </summary>
        /// <param name="str_acceleration"></param>
        /// <returns></returns>
        private String[] GetSerialData(string str_serial_data)
        {
            // LIS3DH: acc_scale/Ax/Ay/Az/click
            // MPU6500: acc_scale/Ax/Ay/Az/click=0||gyro_scale/Gx/Gy/Gz||temperature
            String[] strs = str_serial_data.Split(new string[] { "/","||","\r" }, StringSplitOptions.None);
            return strs;
        }

        /// <summary>
        /// Get data from serial port
        /// </summary>
        /// <returns></returns>
        private string ReadSerialData()
        {
            // ADD CODE FOR DATA PROCESSING BELOW
            string value = "";
            try
            {
                if (port != null && port.BytesToRead > 0)
                {
                    value = port.ReadLine();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error occurs while reading serial：" + ex.Message, "tips");
            }

            return value;
        }

        /// <summary>
        /// Close and destroy the Serial instance
        /// </summary>
        private void DisposeSerialPort()
        {
            if (port != null)
            {
                try
                {
                    stopFlag = true;
                    port.WriteLine("serial stop");
                    //timer1.Stop();
                    if (port.IsOpen)
                    {
                        port.Close();
                    }
                    port.Dispose();
                }
                catch (Exception ex)
                {
                    EnableSerial_Bluetooth(true);
                    MessageBox.Show("Errors occurs during port closure" + ex.Message, "Tips");
                    return;
                }
            }
        }

        // click sensitivity setting (LIS3DH only)
        private void button_apply_collision_sen_Click(object sender, RoutedEventArgs e)
        {
            String str_collision_sensitivity = textBox_collision_sensitivity.Text.Trim();
            double db_collision_sensitivity = Convert.ToDouble(str_collision_sensitivity);
            int sensitivity = (int)((db_collision_sensitivity / 1000) * 128 / ACM.range);
            sensitivity = sensitivity == 0 ? 1 : sensitivity;
            try
            {
                Send_Command("c:", sensitivity.ToString());
                textBlock_info.Text = "C/S: " + sensitivity.ToString() + " / 128";
            }
            catch(Exception ex)
            {
                return;
            }
        }

        /// <summary>
        /// Send command to the Arduino, change inner setting
        /// </summary>
        /// <param name="str_cmd">command indicator</param>
        /// <param name="param"> command param</param>
        private void Send_Command(String str_cmd, String param)
        {
            String str_fullscale = str_cmd + param;
            try
            {
                if (port != null)
                {
                    port.WriteLine(str_fullscale);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error occurs while writting serial：" + ex.Message, "tips");
                return;
            }
        }

        #endregion

        /* /////////////////////////////////////////////////////////////////////////////////////////////
                                    Following Code for Gyroscope
        ///////////////////////////////////////////////////////////////////////////////////////////// */
        #region Gyrosope
        private void comboBox_GyroFullScale_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                String str_fullscale = ((ComboBoxItem)(comboBox_GyroFullScale.SelectedItem)).Content.ToString();
                if (DS == DataSource.DS_SERIAL)
                {
                    double db_range = Convert.ToDouble(str_fullscale);
                    if (GYR.range != db_range)
                    {
                        Send_Command("g:", str_fullscale);
                    }
                }
                else if (DS == DataSource.DS_BLE)
                {
                    byte[] cmmand_bytes = new byte[] { 0x04};
                    // CONTROL_POINT
                    switch (str_fullscale)
                    {
                        case "250":
                            {
                                cmmand_bytes = new byte[] { 0x04 };
                                break;
                            }
                        case "500":
                            {
                                cmmand_bytes = new byte[] { 0x05 };
                                break;
                            }
                        case "1000":
                            {
                                cmmand_bytes = new byte[] { 0x06 };
                                break;
                            }
                        case "2000":
                            {
                                cmmand_bytes = new byte[] { 0x07 };
                                break;
                            }
                    }
                    ble_task.Write_Atrribute(34, ble_task.current_connection, cmmand_bytes);
                }
            }
            catch (Exception ex)
            {
                return;
            }
        }

        #endregion

        /* /////////////////////////////////////////////////////////////////////////////////////////////
                                            Following Code for Bluetooth Communication
        ///////////////////////////////////////////////////////////////////////////////////////////// */
        #region Bluetooth Communication

        private bool checkBLE_condition()
        {
            if (port != null && port.IsOpen)
            {
                if (ble_task == null)
                {
                    ble_task = new bgtask(port, textBox_receiveingData);
                }

                return true;
                // ble_task.Connect_Device();
            }
            else
            {
                MessageBox.Show("please open serial port!");
                return false;
            }
        }

        private void button_bt_search_Click(object sender, RoutedEventArgs e)
        {
            // scan nearby devices
            if (checkBLE_condition())
            {
                ble_task.Scanning_Devices();
                dispatcherTimer_scanning.Start();
            }
        }

        private void button_bt_connect_Click(object sender, RoutedEventArgs e)
        {
            if (button_bt_connect.Content.ToString() == "Connect")
            {
                // connect
                if (checkBLE_condition())
                {
                    string[] strs = str_selected_device.Split(new string[] { ":" }, StringSplitOptions.None);
                    byte[] target_address = new byte[strs.Length];
                    for (int i = 0; i < strs.Length; i++)
                    {
                        target_address[strs.Length - 1 - i] = Convert.ToByte(strs[i], 16);
                    }
                    ble_task.target_device_address = target_address;
                    ble_task.Connect_Device();

                    Thread check_connection_thread = new Thread(() => check_connection(true));
                    check_connection_thread.Start();
                }
            }
            else
            {
                // disconnect
                if (MessageBox.Show("Intended to disconnect the sensor?", "Disconnect Warning", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    try
                    {
                        ble_task.Disconnect_Device();
                        Thread check_connection_thread = new Thread(() => check_connection(false));
                        check_connection_thread.Start();
                    }
                    catch
                    {
                        button_bt_connect.Content = "Connect";
                    }

                }
            }
        }

        private void setRSSIView(int rssi)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action<int>(setRSSIView), new object[] { rssi });
                return;
            }
            progressBar_RSSI.Value = 100 + rssi * 100 / 150;
            textBlock_rssi.Text = rssi.ToString();
        }

        private void listBox_bluetoothdevices_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
               
            }
            catch (Exception ex)
            {
                textBlock_bt_status.Text = "Status: " + ex.Message;
                textBlock_bt_status.ToolTip = "Status: " + ex.Message;
            }
        }

        private void check_connection(bool connecting)
        {
            if (connecting)
            {
                while (ble_task.app_state != bgtask.App_State.STATE_CONNECTED)
                {

                }

                setButtonText(button_bt_connect, "Disconnect");
                MessageBox.Show("Device connected");
            }
            else
            {
                while (ble_task.app_state != bgtask.App_State.STATE_DISCONNECTED)
                {

                }

                setButtonText(button_bt_connect, "Connect");
                MessageBox.Show("Device disconnected");
            }
        }

        private void setButtonText(Button btn, string text)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action<Button, string>(setButtonText), new object[] { btn, text });
                return;
            }
            btn.Content = text;
        }

        private void listBox_bluetoothdevices_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = (sender as ListBox).SelectedItem;
            if (item != null)
            {
                string curr_text = item.ToString();
                string[] strs = curr_text.Split(new string[] { ":|" }, StringSplitOptions.None);
                label_device_selected.Content = strs[0];
                str_selected_device = strs[0];
            }
        }

        private void button_bt_start_streaming_Click(object sender, RoutedEventArgs e)
        {
            ble_task.app_state = bgtask.App_State.STATE_CONNECTED;
            startStreaming();
        }

        private void button_bt_stop_streaming_Click(object sender, RoutedEventArgs e)
        {
            ble_task.app_state = bgtask.App_State.STATE_CONNECTED;
            stopStreaming();
        }

        private void startStreaming()
        {
            ble_task.Write_Atrribute(41, ble_task.current_connection, new byte[] { 0x01, 0x00 });
            ble_task.app_state = bgtask.App_State.STATE_STREAMING;
            dispatcherTimer_ble_streaming.Start();
        }

        private void stopStreaming()
        {
            ble_task.Write_Atrribute(31, ble_task.current_connection, new byte[] { 0x00 });
            ble_task.app_state = bgtask.App_State.STATE_CONNECTED;
            dispatcherTimer_ble_streaming.Stop();
        }
        #endregion

        /* /////////////////////////////////////////////////////////////////////////////////////////////
                                            Following Code for 3D Display
        ///////////////////////////////////////////////////////////////////////////////////////////// */
        #region 3D Display
        private void Setup3D_Display(string model_dir)
        {
            viewPort3d.Children.Clear();
            // set the 3D device and add the view to the 3D viewport
            ModelVisual3D device3D = new ModelVisual3D();
            device3D.Content = Display3d(model_dir);
            viewPort3d.Children.Add(device3D);
            //HelixToolkit.Wpf.DirectionalHeadLight dhl = new HelixToolkit.Wpf.DirectionalHeadLight();
            //dhl.Color = Colors.White;
            //dhl.Brightness = 300;
            //viewPort3d.Children.Add(dhl); // new HelixToolkit.Wpf.DefaultLights()
            viewPort3d.Children.Add(new HelixToolkit.Wpf.DefaultLights());
    
            glinesv3d = new HelixToolkit.Wpf.GridLinesVisual3D();
            glinesv3d.Thickness = 0.05;
            glinesv3d.Length = 600;
            glinesv3d.Width = 600;
            glinesv3d.MinorDistance = 10;
            glinesv3d.MajorDistance = 20;
            glinesv3d.Fill = new SolidColorBrush(Colors.Gray);
            glinesv3d.Visible = false;
            viewPort3d.Children.Add(glinesv3d);
        }
        
        private void checkBox_Showground_Checked(object sender, RoutedEventArgs e)
        {
            if (glinesv3d != null)
            {
                glinesv3d.Visible = true;
            }
        }

        private void checkBox_Showground_Unchecked(object sender, RoutedEventArgs e)
        {
            if (glinesv3d != null)
            {
                glinesv3d.Visible = false;
            }
        }

        /// <summary>
        /// Display 3D Model
        /// </summary>
        /// <param name="model">Path to the Model file</param>
        /// <returns>3D Model Content</returns>
        private Model3D Display3d(string model)
        {
            Model3D device = null;
            try
            {
                //Adding a gesture here
                viewPort3d.RotateGesture = new MouseGesture(MouseAction.LeftClick);

                //Import 3D model file
                ModelImporter import = new ModelImporter();

                //Load the 3D model file
                device = import.Load(model);
            }
            catch (Exception e)
            {
                // Handle exception in case can not file 3D model
                MessageBox.Show("Exception Error : " + e.StackTrace);
            }
            return device;
        }

        private void viewPort3d_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = (this.WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal);
            this.WindowStyle = (this.WindowStyle== WindowStyle.None? WindowStyle.SingleBorderWindow : WindowStyle.None);
        }

        private void button_start_MouseEnter(object sender, MouseEventArgs e)
        {
            button_start.Opacity = 1;
        }

        private void button_start_MouseLeave(object sender, MouseEventArgs e)
        {
            button_start.Opacity = 0.6;
        }

        private void viewPort3d_MouseEnter(object sender, MouseEventArgs e)
        {
        }

        private void textBox_receiveingData_TextChanged(object sender, TextChangedEventArgs e)
        {
            textBox_receiveingData.ScrollToEnd();
        }

        private void Window_monitor_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(FilterWindow != null)
            {
                FilterWindow.Close();
                FilterWindow = null;
            }
            if(collision_test_window != null)
            {
                collision_test_window.Close();
                collision_test_window = null;
            }
        }

        /// <summary>
        /// reset 3D view
        /// </summary>
        private void reset_3D_view(int x, int y, int z, double t)
        {
            // viewPort3d.ResetCamera();
            Point3D newPosition = new Point3D(50,2,1);
            Vector3D vp = new Vector3D(0, 0, 1);
            viewPort3d.SetView(newPosition, new Vector3D(x, y, z), vp, t);
            //viewPort3d.FitView(new Vector3D(x, y, z), vp, t);
        }

        private void reset_3D_view(Point3D newPosition, Vector3D newDirect, Vector3D newUPDirect, double t)
        {
            viewPort3d.SetView(newPosition, newDirect, newUPDirect, 1500);
        }

        private void button_start_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (button_start.Content.Equals("START"))
            {
                button_start.Content = "STOP";
                if (glinesv3d != null)
                {
                    glinesv3d.Visible = true;
                }
                Point3D newPosition = new Point3D(325, -225.4, 221);
                Vector3D newdirect = new Vector3D(-325, 227, -219);
                Vector3D newUpdirect = new Vector3D(0,0,1);
                addTrajectory();
                reset_3D_view(newPosition, newdirect, newUpdirect, 1500);
                checkBox_Showground.IsChecked = true;
                SS = SystemState.SS_START;
            }
            else
            {
                button_start.Content = "START";
                reset_3D_view(-65, 0, 0, 1500);
                if (glinesv3d != null)
                {
                    glinesv3d.Visible = false;
                }
                removeTrajectory();
                checkBox_Showground.IsChecked = false;
                SS = SystemState.SS_IDLE;
            }
        }

        // rotate around Y axis
        private void checkBox_autorotate_Checked(object sender, RoutedEventArgs e)
        {
            // Vector3D vp = new Vector3D(-1, -1, 1);
            // viewPort3d.FitView(new Vector3D(-45, -100, -45), vp, 1500);
            dispatcherTimer.Start();
        }

        private void checkBox_autorotate_Unchecked(object sender, RoutedEventArgs e)
        {
            dispatcherTimer.Stop();
            //dispatcherTimer2.Stop();
        }

        private void initial_timer(int ms, int ms2)
        {
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, ms2);
            dispatcherTimer2 = new DispatcherTimer();
            dispatcherTimer2.Tick += dispatcherTimer2_Tick;
            dispatcherTimer2.Interval = new TimeSpan(0, 0, 0, 0, ms);
            dispatcherTimer_scanning = new DispatcherTimer();
            dispatcherTimer_scanning.Tick += dispatcherTimer_scanning_Tick;
            dispatcherTimer_scanning.Interval = new TimeSpan(0, 0, 0, 0, 300);
            dispatcherTimer_ble_streaming = new DispatcherTimer();
            dispatcherTimer_ble_streaming.Tick += dispatcherTimer_ble_streaming_Tick;
            dispatcherTimer_ble_streaming.Interval = new TimeSpan(0, 0, 0, 0, 10);
        }

        // auto rotate / rotate around Y axis
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            double camera_X = 1.5;
            viewPort3d.CameraController.IsRotationEnabled = true;
            viewPort3d.CameraController.AddRotateForce(camera_X, 0);
        }

        // setup output infomation 
        private void dispatcherTimer2_Tick(object sender, EventArgs e)
        {
            if (source_Ax.Collection.Count > 500)
            {
                Savedata(AUTO_SAVE_PATH, 1);

                source_Ax.Collection.Clear();
                source_Ay.Collection.Clear();
                source_Az.Collection.Clear();

                source_gx.Collection.Clear();
                source_gy.Collection.Clear();
                source_gz.Collection.Clear();

                source_ox.Collection.Clear();
                source_oy.Collection.Clear();
                source_oz.Collection.Clear();
                source_ow.Collection.Clear();

            }
            SetOutput_info();
        }

        private void dispatcherTimer_scanning_Tick(object sender, EventArgs e)
        {
            try
            {
                if (ble_task != null)
                {
                    if (ble_task.app_state == bgtask.App_State.STATE_SCANNING)
                    {
                        foreach (Bluegiga.BLE.Events.GAP.ScanResponseEventArgs scan_result in ble_task.lst_scan_result)
                        {
                            string strSender = ble_task.getHexstringfromArray(scan_result.sender, ":");
                            string str = "";
                            bool duplicate = false;
                            int i = 0;
                            for (i = 0; i < listBox_bluetoothdevices.Items.Count; i++)
                            {
                                if (listBox_bluetoothdevices.Items[i].ToString().StartsWith(strSender))
                                {
                                    duplicate = true;
                                    break;
                                }
                            }

                            str = strSender + "|" + scan_result.rssi + " dB";
                            if (strSender.StartsWith(str_selected_device))
                            {
                                setRSSIView(scan_result.rssi);
                            }
                            if (duplicate)
                            {
                                listBox_bluetoothdevices.Items[i] = str;
                            }
                            else
                            {
                                listBox_bluetoothdevices.Items.Add(str);
                            }

                        }
                    }
                }
            }
            catch
            {
                return;
            }
        }

        private void dispatcherTimer_ble_streaming_Tick(object sender, EventArgs e)
        {
            if (ble_task.streaming_data != null && ble_task.streaming_data.Length>0)
            {
                if (!stopFlag)
                {
                    try
                    {
                        if (DMH.boolAcceleration)
                        {
                            int acc_range = PacketParser.getAccelRange(ble_task.streaming_data);
                            double Ax = PacketParser.getAccel(ble_task.streaming_data, "X", acc_range);
                            double Ay = PacketParser.getAccel(ble_task.streaming_data, "Y", acc_range);
                            double Az = PacketParser.getAccel(ble_task.streaming_data, "Z", acc_range);
                            string str_click = "";

                            ACM.update_value(acc_range, Ax, Ay, Az, str_click);

                            if (DMH.boolIgnoreGravity)
                            {
                                Az = Az - 1;
                            }

                            if (filter_type_rd != 0)
                            {
                                if (is_new_filter)
                                {
                                    initial_filters(1, filter_type_rd, filter_length_rd, filter_threshold_rd, is_reverse_rd);
                                    initial_filters(2, filter_type_pry, filter_length_pry, filter_threshold_pry, is_reverse_pry);
                                    // initial_filters(3, filter_type_gd, filter_length_gd, filter_threshold_gd, is_reverse_gd);
                                    is_new_filter = false;
                                }

                                double Ax1 = applyFilter(filter_type_rd, ref Filter_X, filter_length_rd, Ax, first_detect_rd, ref is_locked_rd, is_collision_test);
                                double Ay1 = applyFilter(filter_type_rd, ref Filter_Y, filter_length_rd, Ay, first_detect_rd, ref is_locked_rd, is_collision_test);
                                double Az1 = applyFilter(filter_type_rd, ref Filter_Z, filter_length_rd, Az, first_detect_rd, ref is_locked_rd, is_collision_test);

                                AddAcceleration2Chart(Ax1, Ay1, Az1);
                                
                            }
                            else
                            {
                                AddAcceleration2Chart(Ax, Ay, Az);
                            }
                        }

                        if (DMH.boolGyroscope)
                        {
                            int gyro_range = PacketParser.getGyroRange(ble_task.streaming_data);
                            double Gx = PacketParser.getGyro(ble_task.streaming_data, "X", gyro_range);
                            double Gy = PacketParser.getGyro(ble_task.streaming_data, "Y", gyro_range);
                            double Gz = PacketParser.getGyro(ble_task.streaming_data, "Z", gyro_range);

                            GYR.update_value(gyro_range, Gx, Gy, Gz);

                            AddGyro2Chart(Gx, Gy, Gz);
                        }

                        if (DMH.boolTemperature)
                        {
                            DMH.device_temperature = 0;
                        }

                    }
                    catch (Exception ex)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Display the Acceleration infomation on the screem
        /// </summary>
        private void SetOutput_info()
        {
            try
            {
                String Time_now = System.DateTime.Now.ToLongDateString() + " " + System.DateTime.Now.ToLongTimeString();
                String camera_rotate_mode = ("  Rotation Mode: " + viewPort3d.CameraRotationMode.ToString() + "\r\n");
                String camera_position = ("  Position: " + viewPort3d.CameraController.CameraPosition.ToString() + "\r\n");
                String camera_look_direction = ("  Look Direction: " + viewPort3d.CameraController.CameraLookDirection.ToString() + "\r\n");
                String camera_target = ("  Target: " + viewPort3d.CameraController.CameraTarget.ToString() + "\r\n");
                String seperator_line = "\r\n";

                String acceleration = "  Acceleration: Not supported\r\n";
                String ned_acceleration = "  Fused Acceleration: Not Defined\r\n";
                String gyro = "  Gyro: Not supported\r\n";
                String range = "  Range: Not Defined \r\n";
                String device_temperature = "  Device Temperature: Not supported\r\n";
                String collision = "  Collision: Not detected\r\n";
                String calibration = "";

                if (!stopFlag)
                {
                    MathNet.Numerics.LinearAlgebra.Double.Matrix transform_Matrix = DenseMatrix.OfArray(new double[,]{ { 1,0,0},{ 0,1,0},{ 0,0,1} });
                    range = "  Range:";
                    if (DMH.boolAcceleration)
                    {
                        range += " <Acceleration> +/ -" + ACM.range.ToString() + "g\t";
                        textBox_X.Text = ACM.current_acceleration.Ax.ToString();
                        textBox_Y.Text = ACM.current_acceleration.Ay.ToString();
                        textBox_Z.Text = DMH.boolIgnoreGravity?(ACM.current_acceleration.Az - 1).ToString(): ACM.current_acceleration.Az.ToString();

                        if (DMH.enable_Click_Sensitivity == false)
                        {
                            collision = "";
                        }
                        else
                        {
                            string clic_detected = ACM.str_click;
                            if (clic_detected != "0")
                            {
                                collision = "  Collision: Detected at " + DateTime.Now.ToLongTimeString() + "\r\n";
                            }
                        }

                        acceleration = "  Acceleration (g): X = " + textBox_X.Text + ",   Y = " + textBox_Y.Text + ",   Z = " + textBox_Z.Text + "\r\n";
                        transform_Matrix = ACM.get_TransformMatrix();
                    }

                    if(DMH.boolGyroscope)
                    {
                        range += " <Gyro> +/-" + GYR.range.ToString() + " degree/s";

                        textBox_GX.Text = GYR.current_gyro.GyroX.ToString();
                        textBox_GY.Text = GYR.current_gyro.GyroY.ToString();
                        textBox_GZ.Text = GYR.current_gyro.GyroZ.ToString();

                        gyro = "  Gyro (rad/s): X = " + textBox_GX.Text + ",   Y = " + textBox_GY.Text + ",   Z = " + textBox_GZ.Text + "\r\n";
                        calibration += "  Gyroscope (X,Y,Z): " + Math.Round(GYR.calibration_vector[0],3).ToString() + ", " + Math.Round(GYR.calibration_vector[1],3).ToString() + ", " + Math.Round(GYR.calibration_vector[2],3).ToString() + "\r\n";

                        transform_Matrix = GYR.get_TransformMatrix();

                    }

                    // CONVERT TO NED SYSTEM, depends on weight between Accelerometer or Gyroscope
                    MathNet.Numerics.LinearAlgebra.Double.Vector NED_Acceleration = ACM.convert_to_NEDSystem(transform_Matrix);
                    ned_acceleration = "  NED Acceleration (g): X = " + Math.Round(NED_Acceleration[0], 2).ToString()
                        + ",   Y = " + Math.Round(NED_Acceleration[1], 2).ToString()
                        + ",   Z = " + Math.Round(NED_Acceleration[2], 2).ToString() + "\r\n";
                    calibration += "  Accelerometer (X,Y,Z): " + Math.Round(ACM.calibration_vector[0], 3).ToString() + ", " + Math.Round(ACM.calibration_vector[1], 3).ToString() + ", " + Math.Round(ACM.calibration_vector[2], 3).ToString() + "\r\n";


                    if (DMH.boolTemperature)
                    {
                        textBox_DeviceTemp.Text = DMH.device_temperature.ToString();
                        device_temperature = "  Device Temperature(centi-degree): " + textBox_DeviceTemp.Text + "\r\n";
                    }
                    range += "\r\n";
                }

                String tilt_angle = "";
                if (!is_collision_test)
                {
                    tilt_angle = SetModelPosition();
                }

                String camera_info = "[Camera Info] " + Time_now + "\r\n\r\n" + camera_rotate_mode + camera_position + camera_look_direction + camera_target;
                String measure_info = "[Measurement Info] ( "+ source_Ax.Collection.Count.ToString() + " )\r\n\r\n" + range + device_temperature + acceleration + gyro + tilt_angle + collision;
                String converted_info = "[Converted Info] \r\n\r\n" + ned_acceleration;
                String calibration_info = "[Calibration Info] "+ ACM.array_list_calibration.Count.ToString() + " / 500  \r\n\r\n" + calibration;
                textBlock_output.Text = measure_info + seperator_line + converted_info + seperator_line + calibration_info;
            }
            catch
            {
                return;
            }
        }

        // plot acceleration in the chart
        private void AddAcceleration2Chart(double Ax1, double Ay1, double Az1)
        {
            double x = DateTime.Now.Ticks / 10e5;
            Point p1 = new Point(x, Ax1);
            Point p2 = new Point(x, Ay1);
            Point p3 = new Point(x, Az1);
            
            source_Ax.AppendAsync(Dispatcher, p1);
            source_Ay.AppendAsync(Dispatcher, p2);
            source_Az.AppendAsync(Dispatcher, p3);
        }
        // plot Gyro in the chart
        private void AddGyro2Chart(double Gx, double Gy, double Gz)
        {
            double x = DateTime.Now.Ticks / 10e5;
            Point p1 = new Point(x, Gx);
            Point p2 = new Point(x, Gy);
            Point p3 = new Point(x, Gz);

            source_gx.AppendAsync(Dispatcher, p1);
            source_gy.AppendAsync(Dispatcher, p2);
            source_gz.AppendAsync(Dispatcher, p3);
        }

        private void AddQuat2Chart(double[] quat)
        {
            double x = DateTime.Now.Ticks / 10e5;
            Point p1 = new Point(x, quat[0]);
            Point p2 = new Point(x, quat[1]);
            Point p3 = new Point(x, quat[2]);
            Point p4 = new Point(x, quat[3]);
            source_ox.AppendAsync(Dispatcher, p1);
            source_oy.AppendAsync(Dispatcher, p2);
            source_oz.AppendAsync(Dispatcher, p3);
            source_ow.AppendAsync(Dispatcher, p4);
        }

        /*
        private void AddTiltAngle2Chart(Vector3D tilt_angle)
        {
            double x = DateTime.Now.Ticks / 10e5;
            Point p4 = new Point(x, tilt_angle.X * 180 / Math.PI);
            Point p5 = new Point(x, tilt_angle.Y * 180 / Math.PI);
            Point p6 = new Point(x, tilt_angle.Z * 180 / Math.PI);

            source_pitch.AppendAsync(Dispatcher, p4);
            source_roll.AppendAsync(Dispatcher, p5);
            source_yaw.AppendAsync(Dispatcher, p6);
        }
        */

        /// <summary>
        /// Set the model position according to the acceleration and return a string containing the tilt angle infomation
        /// </summary>
        /// <returns>string containing the tilt angle infomation</returns>
        private String SetModelPosition()
        {
            double g = Convert.ToDouble(textBox_gravity.Text.Trim());
            double Ax1 = Convert.ToDouble(textBox_X.Text.Trim());
            double Ay1 = Convert.ToDouble(textBox_Y.Text.Trim());
            double Az1 = Convert.ToDouble(textBox_Z.Text.Trim());
            
            Vector3D tilt_angle = ACM.get_TiltAngle();
            //if(!stopFlag)
            //{
                //AddTiltAngle2Chart(tilt_angle);
            //}
            Vector3D axis = new Vector3D(Ax1, -Ay1, -Az1);
            String model_center = SetModelPosition(tilt_angle, axis);

            String str_tilt_angles = "  Tilt Angles: Pitch = " + Math.Round(tilt_angle.X * 180 / Math.PI, 4).ToString() + ",  Roll = "
                + Math.Round(tilt_angle.Y * 180 / Math.PI, 4).ToString() + ",  Yaw = "
                + Math.Round(tilt_angle.Z * 180 / Math.PI, 4).ToString() + "\r\n" + model_center;

            double[] quat = getQuaternionfromPitchRollYaw(tilt_angle.X, tilt_angle.Y, tilt_angle.Z);
            AddQuat2Chart(quat);

            /*  TEST CODE
            tilt_angles = "  Tilt Angles: Pitch = " + global_pitch.ToString() + ",  Roll = "
                + global_roll.ToString() + ",  Yaw = "
                +global_yaw.ToString() + "\r\n" + model_center;
            */
            return str_tilt_angles;
        }

        /// <summary>
        /// the overload function of SetModelPosition, which actually set the position of mode in the 3d viewport
        /// </summary>
        /// <param name="tilt_angle">input tilt angle vector</param>
        /// <returns>string containing the model center infomation</returns>
        private String SetModelPosition(Vector3D tilt_angle, Vector3D axis)
        {
            ModelVisual3D device3D = (ModelVisual3D)viewPort3d.Children[0];
            Point3D device_center = GetCenter(device3D);
            String model_center = "  Model Center: " + device_center.ToString()+"\r\n";

            //AxisAngleRotation3D axis_angle_rotation1 = new AxisAngleRotation3D(new Vector3D(1, 0, 0), tilt_angle.X * 180 / Math.PI);
            //AxisAngleRotation3D axis_angle_rotation2 = new AxisAngleRotation3D(new Vector3D(1, Math.Tan(tilt_angle.X), 0), tilt_angle.Y * 180 / Math.PI);
            //AxisAngleRotation3D axis_angle_rotation3 = new AxisAngleRotation3D(new Vector3D(0, 0, 1), tilt_angle.Z * 180 / Math.PI);

            Vector3D axis_roll = new Vector3D(0, 1, Math.Tan(global_pitch*Math.PI/180));
            AxisAngleRotation3D axis_angle_rotation2 = new AxisAngleRotation3D(axis_roll, global_roll);
            AxisAngleRotation3D axis_angle_rotation1 = new AxisAngleRotation3D(new Vector3D(1, 0, 0), global_pitch);
            AxisAngleRotation3D axis_angle_rotation3 = new AxisAngleRotation3D(new Vector3D(0, 0, 1), global_yaw);


            global_roll = tilt_angle.Y * 180 / Math.PI;
            global_pitch = tilt_angle.X * 180 / Math.PI;
            global_yaw = tilt_angle.Z * 180 / Math.PI;

            /*  TEST CODE
            global_pitch = Convert.ToDouble(textBox_X.Text);
            global_roll = Convert.ToDouble(textBox_Y.Text);
            global_yaw = Convert.ToDouble(textBox_Z.Text);

            
            double roll = tilt_angle.Y;
            double pitch = tilt_angle.X;
            double yaw = tilt_angle.Z;

            double quat_x = Math.Cos(roll/2)*Math.Cos(pitch/2)*Math.Cos(yaw/2) + Math.Sin(roll / 2) * Math.Sin(pitch / 2) * Math.Sin(yaw / 2);
            double quat_y = Math.Sin(roll/2)*Math.Cos(pitch/2)*Math.Cos(yaw/2) - Math.Cos(roll / 2) * Math.Sin(pitch / 2) * Math.Sin(yaw / 2);
            double quat_z = Math.Cos(roll/2)*Math.Sin(pitch/2)*Math.Cos(yaw/2) + Math.Sin(roll / 2) * Math.Cos(pitch / 2) * Math.Sin(yaw / 2);
            double quat_w = Math.Cos(roll/2)*Math.Cos(pitch/2)*Math.Sin(yaw/2) - Math.Sin(roll / 2) * Math.Sin(pitch / 2) * Math.Cos(yaw / 2);
            Quaternion quaternion = new Quaternion(quat_x, quat_y, quat_z, quat_w);
            QuaternionRotation3D qr3d = new QuaternionRotation3D(quaternion);

            RotateTransform3D rotate_transform_quat = new RotateTransform3D(qr3d);
            */

            RotateTransform3D rotate_transform1 = new RotateTransform3D(axis_angle_rotation1, device_center);
            RotateTransform3D rotate_transform2 = new RotateTransform3D(axis_angle_rotation2, device_center);
            RotateTransform3D rotate_transform3 = new RotateTransform3D(axis_angle_rotation3, device_center);

            Transform3DGroup transform_group = new Transform3DGroup();

            transform_group.Children.Add(rotate_transform1);
            transform_group.Children.Add(rotate_transform2);
            if (checkBox_EstimateYaw.IsChecked == true)
            {
                transform_group.Children.Add(rotate_transform3);
            }

            
            if (SS == SystemState.SS_START)
            {
                double offset_x = 0;
                double offset_y = 0;
                double offset_z = 0;
                offset_x += 50;
                offset_y += 150;
                offset_z += 100;
                
                TranslateTransform3D translate_transform = setTranslateTransform3D(offset_x, offset_y, offset_z);
                transform_group.Children.Add(translate_transform);

                Thread update_trajectory_thread = new Thread(() => updateTrajectory(offset_x, offset_y, offset_z));
                update_trajectory_thread.Start();
                //updateTrajectory(offset_x, offset_y, offset_z);
            }
            
            device3D.Transform = transform_group;

            return model_center;
        }

        private void updateTrajectory(double offset_x, double offset_y, double offset_z)
        {
            if(!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action<double, double, double>(updateTrajectory), new object[] { offset_x, offset_y, offset_z });
                return;
            }

            List<double> lst = new List<double>();
            lst.Add(offset_x);
            lst.Add(offset_y);
            lst.Add(offset_z);

            Point3D points = new Point3D();
            Point3DCollection point_collection = new Point3DCollection();
            trajectory.Points.Clear();
            trajectory.Thickness = 1;
            trajectory.Color = Colors.White;
            
            double oz = 1;
            while (oz < lst.Max())
            {
                double x = oz > offset_x ? offset_x : oz;
                double y = oz > offset_y ? offset_y : oz;
                double z = oz > offset_z ? offset_z : oz;
                points = new Point3D(x, y, z);

                point_collection.Add(points);
                oz++;
            }
            trajectory.Points = point_collection;
        }

        private void removeTrajectory()
        {
            viewPort3d.Children.Remove(trajectory);
        }

        private void addTrajectory()
        {
            viewPort3d.Children.Add(trajectory);
        }

        private TranslateTransform3D setTranslateTransform3D(double offset_x, double offset_y, double offset_z)
        {
            TranslateTransform3D translate_transform = new TranslateTransform3D(new Vector3D(offset_x, offset_y, offset_z));
            // TEST CODE
            ModelVisual3D device3D = (ModelVisual3D)viewPort3d.Children[0];
            Point3D device_center = GetCenter(device3D);
            Transform3DGroup transform_group = new Transform3DGroup();
            if (SS == SystemState.SS_START)
            {
                transform_group.Children.Add(translate_transform);
            }
            device3D.Transform = transform_group;
            // END OF TEST CODE
            return translate_transform;
        }

        /// <summary>
        /// get center axis of the 3d model
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public Point3D GetCenter(ModelVisual3D model)
        {
            var rect3D = Rect3D.Empty;
            UnionRect(model, ref rect3D);
            Point3D _center = new Point3D((rect3D.X + rect3D.SizeX / 2), (rect3D.Y + rect3D.SizeY / 2), (rect3D.Z + rect3D.SizeZ / 2));
            return _center;
        }

        // get a rect of the model
        private void UnionRect(ModelVisual3D model, ref Rect3D rect3D)
        {
            for (int i = 0; i < model.Children.Count; i++)
            {
                var child = model.Children[i] as ModelVisual3D;
                UnionRect(child, ref rect3D);
            }
            if (model.Content != null)
                rect3D.Union(model.Content.Bounds);
        }

        /// <summary>
        /// move model according to the result from Calculate_velocity_distance
        /// </summary>
        /// <param name="alst"></param>
        private void move_model(ArrayList alst)
        {
            ModelVisual3D device3D = (ModelVisual3D)viewPort3d.Children[0];
            Point3D device_center = GetCenter(device3D);

            Vector3D offset_xyz = (Vector3D)alst[1];
            TranslateTransform3D translate_transform3d = new TranslateTransform3D(offset_xyz);
            device3D.Transform = translate_transform3d;
        }

        #endregion

        /* /////////////////////////////////////////////////////////////////////////////////////////////
                                    Following Code for Acceleration Calculations
        ///////////////////////////////////////////////////////////////////////////////////////////// */
        #region Acceleration Calculations

        /// <summary>
        /// change accelerometer fullscale
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox_FullScale_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                String str_fullscale = ((ComboBoxItem)(comboBox_FullScale.SelectedItem)).Content.ToString();
                if (DS == DataSource.DS_SERIAL)
                {
                    double db_range = Convert.ToDouble(str_fullscale);
                    if (ACM.range != db_range)
                    {
                        Send_Command("a:", str_fullscale);
                    }
                }
                else if (DS == DataSource.DS_BLE)
                {
                    byte[] cmmand_bytes = new byte[] { 0x00 };
                    // CONTROL_POINT
                    switch (str_fullscale)
                    {
                        case "2":
                            {
                                cmmand_bytes = new byte[] { 0x00 };
                                break;
                            }
                        case "4":
                            {
                                cmmand_bytes = new byte[] { 0x01 };
                                break;
                            }
                        case "8":
                            {
                                cmmand_bytes = new byte[] { 0x02 };
                                break;
                            }
                        case "16":
                            {
                                cmmand_bytes = new byte[] { 0x03 };
                                break;
                            }
                    }
                    ble_task.Write_Atrribute(34, ble_task.current_connection, cmmand_bytes);
                }
            }
            catch (Exception ex)
            {
                return;
            }
        }

        /*
        /// <summary>
        /// Calculate the tilt angle by Tri-axis tilt sensing method
        /// </summary>
        /// <param name="Ax1">Normalised Ax</param>
        /// <param name="Ay1">Normalised Ay</param>
        /// <param name="Az1">Normalised Az</param>
        /// <param name="g"></param>
        /// <returns>tilt angles</returns>
        /// <reference>http://www.st.com/web/en/resource/technical/document/application_note/CD00268887.pdf</reference>
        private Vector3D get_TiltAngle(double Ax, double Ay, double Az, double g, bool ignore_gravity)
        {

            double Az_temp = Az;
            if(ignore_gravity)
            {
                Az_temp += 1;
            }

            double sqrt_root = Math.Sqrt(Ax * Ax + Ay * Ay + Az_temp * Az_temp);
            double Ax1 = Ax / sqrt_root;
            double Ay1 = Ay / sqrt_root;
            double Az1 = Az_temp / sqrt_root;

            double delta_Ax = Ax - Ax_old;
            double delta_Ay = Ay - Ay_old;

            double alpha = Math.Asin(Ax1); // pitch
            double beta = Math.Asin(Ay1); // roll
            double gamma = Math.Acos(Az1); // yaw


            if (Az_temp < 0)
            {
                alpha = Math.PI - alpha;
            }

            Ax_old = Ax;
            Ay_old = Ay;

            if (filter_type_pry != 0)
            {
                if (is_new_filter)
                {
                    initial_filters(1, filter_type_rd, filter_length_rd, filter_threshold_rd, is_reverse_rd);
                    initial_filters(2, filter_type_pry, filter_length_pry, filter_threshold_pry, is_reverse_pry);
                    is_new_filter = false;
                }

                switch (filter_type_pry)
                {
                    case 1:
                        {
                            // simple moving average filter
                            Filter_Pitch.simple_moving_average_filter.add_Sample(alpha);
                            Filter_Roll.simple_moving_average_filter.add_Sample(beta);
                            Filter_Yaw.simple_moving_average_filter.add_Sample(gamma);
                            if (Filter_Pitch.simple_moving_average_filter.data_cnt >= filter_length_pry)
                            {
                                alpha = Filter_Pitch.simple_moving_average_filter.get_Value();
                                beta = Filter_Roll.simple_moving_average_filter.get_Value();
                                gamma = Filter_Yaw.simple_moving_average_filter.get_Value();
                            }
                            break;
                        }
                    case 2:
                        {
                            // zero-one filter
                            alpha = Filter_Pitch.zerone_filter.get_Value(alpha);
                            beta = Filter_Roll.zerone_filter.get_Value(beta);
                            gamma = Filter_Yaw.zerone_filter.get_Value(gamma);
                            break;
                        }
                }
            }

            return new Vector3D(alpha, beta, gamma);
        }
*/

        /// <summary>
        /// calculate velocity
        /// </summary>
        /// <param name="Ax"></param>
        /// <param name="Ay"></param>
        /// <param name="Az"></param>
        /// <param name="g"></param>
        /// <param name="delta_t"></param>
        /// <param name="v0"></param>
        /// <returns></returns>
        private ArrayList Calculate_velocity_distance(double Ax, double Ay, double Az, double g, double delta_t, double v0)
        {
            double vx = v0 + Ax * g * delta_t;
            double vy = v0 + Ay * g * delta_t;
            double vz = v0 + Az * g * delta_t;

            double dx = vx * delta_t;
            double dy = vy * delta_t;
            double dz = vz * delta_t;

            Vector3D velocity = new Vector3D(vx, vy, vz);
            Vector3D displacement = new Vector3D(dx,dy,dz);
            ArrayList alst = new ArrayList();

            alst.Add(velocity);
            alst.Add(displacement);

            return alst;
        }

        /// <summary>
        /// Perform calibration
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_calibaration_Click(object sender, RoutedEventArgs e)
        {
            bool serial_reading = true;
            if (port == null)
            {
                serial_reading = false;
            }
            else if (!port.IsOpen)
            {
                serial_reading = false;
            }
            if (serial_reading == false)
            {
                MessageBox.Show("To start calibration, the serial communication should be turned on first!");
            }
            else
            {
                ACM.start_calibrate();
                GYR.start_calibrate();
            }

        }

        /// <summary>
        ///  adjust the roll angle when pitch changes from less than 90 to larger than 90
        /// </summary>
        /// <param name="Ax"></param>
        /// <param name="Az"></param>
        /// <returns></returns>
        private double Adjust_roll(double Ax, double Az)
        {
            if (Ax > 0 && Az < 0)
            {
                return Math.PI;
            }
            else if (Ax < 0 && Az < 0)
            {
                return -Math.PI;
            }
            return 0;
        }

        /// <summary>
        ///  adjust the roll angle when pitch changes from less than 90 to larger than 90
        /// </summary>
        /// <param name="Ay"></param>
        /// <param name="Az"></param>
        /// <returns></returns>
        private double Adjust_pitch(double Ay, double Az)
        {
            if (Ay > 0 && Az < 0)
            {
                return Math.PI;
            }
            else if (Ay < 0 && Az < 0)
            {
                return -Math.PI;
            }
            return 0;
        }

        // ignore the gravity
        private void checkBox_ignore_gravity_Checked(object sender, RoutedEventArgs e)
        {
            DMH.boolIgnoreGravity = true;
        }
        // DO NOT ignore the gravity
        private void checkBox_ignore_gravity_Unchecked(object sender, RoutedEventArgs e)
        {
            DMH.boolIgnoreGravity = false;
        }

        #endregion

        /* /////////////////////////////////////////////////////////////////////////////////////////////
                            Following Code for Filters and Data processing
        ///////////////////////////////////////////////////////////////////////////////////////////// */
        #region Filters and Data processing

        /// <summary>
        /// initialize filters
        /// </summary>
        /// <param name="datasource"> received data or calculated pitch, roll yaw</param>
        /// <param name="filter_type">selected filter index</param>
        /// <param name="length">filter length / order</param>
        private void initial_filters(int datasource, int filter_type, int length, double[] thresholds, bool reverse = false)
        {
            switch (datasource)
            {
                case 1:
                    {
                        // filter on received data
                        Filter_X = new Filters(filter_type, length, thresholds[0], reverse);
                        Filter_Y = new Filters(filter_type, length, thresholds[1], reverse);
                        Filter_Z = new Filters(filter_type, length, thresholds[2], reverse);
                        
                        break;
                    }
                case 2:
                    {
                        // filter on pitch, roll, yaw
                        Filter_Pitch = new Filters(filter_type, length, thresholds[0], reverse);
                        Filter_Roll = new Filters(filter_type, length, thresholds[1], reverse);
                        Filter_Yaw = new Filters(filter_type, length, thresholds[2], reverse);
                        
                        break;
                    }
                case 3:
                    {
                        // filter on gyro data
                        Filter_GX = new Filters(filter_type, length, thresholds[0], reverse);
                        Filter_GY = new Filters(filter_type, length, thresholds[1], reverse);
                        Filter_GZ = new Filters(filter_type, length, thresholds[2], reverse);

                        break;
                    }
            }
        }

        /// <summary>
        /// Apply filters
        /// </summary>
        /// <param name="filter_type"></param>
        /// <param name="Filter"></param>
        /// <param name="filter_length"></param>
        /// <param name="inputvalue"></param>
        /// <param name="first_detect"></param>
        /// <param name="is_locked"></param>
        /// <param name="collision_test"></param>
        /// <returns></returns>
        private double applyFilter(int filter_type, ref Filters Filter, int filter_length, double inputvalue, bool first_detect, ref bool is_locked, bool collision_test)
        {
            double outputvalue = 0;
            switch (filter_type_rd)
            {
                case 1:
                    {
                        // simple moving average filter
                        Filter.simple_moving_average_filter.add_Sample(inputvalue);

                        if (Filter.simple_moving_average_filter.data_cnt >= filter_length)
                        {
                            outputvalue = Filter.simple_moving_average_filter.get_Value();
                        }
                        
                        break;
                    }
                case 2:
                    {
                        // zero-one filter
                        outputvalue = Filter.zerone_filter.get_Value(inputvalue);

                        if (first_detect)
                        {
                            DateTime tn = DateTime.Now;
                            double tspan = Filter.zerone_filter.get_timespan(tn);
                            if (!is_locked)
                            {
                                if (outputvalue == 1)
                                {
                                    if (tspan > 1000)
                                    {
                                        Filter.zerone_filter.lock_value(outputvalue, tn);
                                        is_locked = true;
                                    }
                                }
                            }
                            else
                            {
                                outputvalue = Filter.zerone_filter.get_lock_Value();
                                if (tspan > 1000)
                                {
                                    is_locked = false;
                                }
                            }
                        }
                        break;
                    }
            }
            return outputvalue;
        }

        private double[] getQuaternionfromPitchRollYaw(double pitch, double roll, double yaw)
        {
            double c1 = Math.Cos(-yaw / 2), s1 = Math.Sin(-yaw / 2);
            double c2 = Math.Cos(-pitch / 2), s2 = Math.Sin(-pitch / 2);
            double c3 = Math.Cos(roll / 2), s3 = Math.Sin(roll / 2);
            double c1_c2 = c1 * c2, s1_s2 = s1 * s2;
            double X = c1 * s2 * c3 - s1 * c2 * s3;
            double Y = c1_c2 * s3 + s1_s2 * c3;
            double Z = s1 * c2 * c3 + c1 * s2 * s3;
            double W = c1_c2 * c3 - s1_s2 * s3;

            return new double[] { X, Y, Z, W};
        }
        #endregion

    }
}