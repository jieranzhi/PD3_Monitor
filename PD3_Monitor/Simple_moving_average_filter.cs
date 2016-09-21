using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PD3_Monitor
{
    class Simple_moving_average_filter
    {
        // filter parameters
        protected double[] mv_samples; // moving average samples
        protected int mv_idx; // moving average index
        public int data_cnt;

        /// <summary>
        /// initialize the instance of the simple moving average filter
        /// </summary>
        /// <param name="length"></param>
        public Simple_moving_average_filter(int length)
        {
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(
                   "Filter length can't be negative or 0.");
            }

            mv_samples = new double[length];
            mv_idx = 0;
        }

        /// <summary>
        ///  add samples to the data cache
        /// </summary>
        /// <param name="val"></param>
        public void add_Sample(double val)
        {
            mv_samples[mv_idx] = val;
            data_cnt++;

            if (++mv_idx == mv_samples.Length)
            {
                mv_idx = 0;
            }
        }

        /// <summary>
        ///  return the filtered result
        /// </summary>
        /// <returns></returns>
        public double get_Value()
        {
            double total = 0;

            for (int i = 0; i < mv_samples.Length; i++)
            {
                total += mv_samples[i];
            }

            return total / mv_samples.Length;
        }

    }
}
