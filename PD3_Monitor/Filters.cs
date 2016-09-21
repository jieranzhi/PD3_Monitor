using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PD3_Monitor
{
    class Filters
    {
        const int NONE = 0;
        const int SIMPLE_MOVING_AVERAGE = 1;
        const int ZERO_ONE = 2;

        public Simple_moving_average_filter simple_moving_average_filter;
        public Zerone_filter zerone_filter;

        /// <summary>
        /// initialize Filter instance
        /// </summary>
        public Filters(int type, int length = 10, double threshold = 5, bool reverse = false)
        {
            switch (type)
            {
                case NONE:
                    {
                        break;
                    }
                case SIMPLE_MOVING_AVERAGE:
                    {
                        this.simple_moving_average_filter = new Simple_moving_average_filter(length);
                        break;
                    }
                case ZERO_ONE:
                    {
                        this.zerone_filter = new Zerone_filter(threshold, reverse);
                        break;
                    }
            }
        }

        /// <summary>
        /// return a simple moving average filter
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        private Simple_moving_average_filter create_simple_moving_average_filter(int length)
        {
            return new Simple_moving_average_filter(length);
        }

        /// <summary>
        /// return a zero-one filter
        /// </summary>
        /// <param name="threshold"></param>
        /// <param name="reverse"></param>
        /// <returns></returns>
        private Zerone_filter create_zero_one_filter(double threshold, bool reverse = false)
        {
            return new Zerone_filter(threshold, reverse);
        }

    }
}
