using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PD3_Monitor
{
    class Zerone_filter
    {
        protected double threshold;
        protected bool isreverse;
        protected double val_lock;
        protected DateTime lock_time;

        /// <summary>
        /// initialize an instance for the filter
        /// </summary>
        /// <param name="threshold"></param>
        /// <param name="reverse"></param>
        public Zerone_filter(double threshold, bool reverse = false)
        {
            this.threshold = threshold;
            this.isreverse = reverse;
            this.lock_time = DateTime.Now;
        }

        public void lock_value(double value, DateTime time)
        {
            this.val_lock = value;
            this.lock_time = time;
        }

        /// <summary>
        /// get the filtered value
        /// </summary>
        /// <returns></returns>
        public double get_Value(double value)
        {
            double val = Math.Abs(value) > threshold ? Math.Sign(value)*1 : 0;
            val = isreverse ? (1 - val) : val;
            return val;
        }

        /// <summary>
        /// get the filtered value
        /// </summary>
        /// <returns></returns>
        public double get_lock_Value()
        {
            return this.val_lock;
        }

        /// <summary>
        /// return the timespan since last lock
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public double get_timespan(DateTime dt)
        {
            return (dt - this.lock_time).TotalMilliseconds;
        }
    }
}
