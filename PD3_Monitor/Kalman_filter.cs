using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace PD3_Monitor
{
    class Kalman_filter
    {
        // define Kalman filter type
        const int BASIC = 0; // basic kalman filter
        const int EKF = 1; // EKF kalman filter
        const int UKF = 2; // UKF kalman filter
        const int CMKF = 3; // CMKF kalman filter

        // filter parameters
        public int filter_length = 0; // Filter length and sample counts
        public Matrix<double> A, B, H, P, Q, R, Z;
        public int kalman_type = 0;

        public Kalman_filter(int kalman_type, Matrix<double> A, Matrix<double> B)
        { }


    }
}
