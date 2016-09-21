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

namespace PD3_Monitor
{
    /// <summary>
    /// Interaction logic for FilterWindow.xaml
    /// </summary>
    public partial class FilterWindow : Window
    {
        public FilterWindow()
        {
            InitializeComponent();
            comboBox_filter_type_rd.SelectedIndex = MainWindow.filter_type_rd;
            comboBox_filter_type_pry.SelectedIndex = MainWindow.filter_type_pry;

            textBox_sMAF_rd.Text = MainWindow.filter_length_rd.ToString();
            textBox_sMAF_pry.Text = MainWindow.filter_length_pry.ToString();

            checkBox_1st_rd.IsChecked = MainWindow.first_detect_rd;
            checkBox_1st_pry.IsChecked = MainWindow.first_detect_pry;

            checkBox_isreverse_rd.IsChecked = MainWindow.is_reverse_rd;
            checkBox_isreverse_pry.IsChecked = MainWindow.is_reverse_pry;

            if (MainWindow.filter_threshold_rd == null)
            {
                MainWindow.filter_threshold_rd = new double[] { 0.4, 0.4, 1.5 };
            }
            else
            {
                textBox_threshold_rd.Text = MainWindow.filter_threshold_rd[0].ToString() + "," + MainWindow.filter_threshold_rd[1].ToString() + "," + MainWindow.filter_threshold_rd[2].ToString();
            }

            if (MainWindow.filter_threshold_pry == null)
            {
                MainWindow.filter_threshold_pry = new double[] { 10, 10, 90 };
            }
            else
            {
                textBox_threshold_pry.Text = MainWindow.filter_threshold_pry[0].ToString() + "," + MainWindow.filter_threshold_pry[1].ToString() + "," + MainWindow.filter_threshold_pry[2].ToString();
            }

        }

        private void button_approve_filter_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.filter_type_rd = comboBox_filter_type_rd.SelectedIndex;
            MainWindow.filter_type_pry = comboBox_filter_type_pry.SelectedIndex;

            MainWindow.filter_length_rd = Convert.ToInt32(textBox_sMAF_rd.Text);
            MainWindow.filter_length_pry = Convert.ToInt32(textBox_sMAF_pry.Text);

            MainWindow.filter_threshold_rd = Convert2Double(textBox_threshold_rd.Text.Trim());
            MainWindow.filter_threshold_pry = Convert2Double(textBox_threshold_pry.Text.Trim());

            MainWindow.is_reverse_rd = (bool)checkBox_isreverse_rd.IsChecked;
            MainWindow.is_reverse_pry = (bool)checkBox_isreverse_pry.IsChecked;

            MainWindow.first_detect_rd = (bool)checkBox_1st_rd.IsChecked;
            MainWindow.first_detect_pry = (bool)checkBox_1st_pry.IsChecked;


            MainWindow.is_new_filter = true;
            MessageBox.Show("Setting saved");
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

        private void Window_Closed(object sender, EventArgs e)
        {
            MainWindow.FilterWindow = null;
        }
    }
}
