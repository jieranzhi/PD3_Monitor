using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Windows.Media.Media3D;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace PD3_Monitor
{
    class Gyroscope
    {
        public Gyro current_gyro;
        public double range;
        public double[] calibration_vector;
        public ArrayList array_list_calibration;

        public Gyroscope()
        {
            this.current_gyro = new Gyro();
            array_list_calibration = new ArrayList();
            calibration_vector = new double[3];
            update_value(250, 0, 0, 0);
        }

        public void update_value(double range, double gx, double gy, double gz)
        {
            this.range = range;
            this.current_gyro.GyroX = gx;
            this.current_gyro.GyroY = gy;
            this.current_gyro.GyroZ = gz;
            calibrate();
        }

        public void start_calibrate()
        {
            array_list_calibration = new ArrayList();
        }

        public bool calibrate()
        {
            int cnt = this.array_list_calibration.Count;
            if (cnt < 500)
            {
                this.array_list_calibration.Add(new Vector3D(this.current_gyro.GyroX, this.current_gyro.GyroY, this.current_gyro.GyroZ));
            }
            else
            {
                double sum_x = 0, sum_y = 0, sum_z = 0;

                foreach (Vector3D v in array_list_calibration)
                {
                    sum_x += v.X;
                    sum_y += v.Y;
                    sum_z += v.Z;
                }
                calibration_vector = new double[] { sum_x / cnt, sum_y / cnt, sum_z / cnt };
                return true;
            }
            return false;
        }

        private MathNet.Numerics.LinearAlgebra.Vector<double> quaternion_differential(double t, MathNet.Numerics.LinearAlgebra.Vector<double> q)
        {
            MathNet.Numerics.LinearAlgebra.Matrix<double> omega = DenseMatrix.OfArray(new double[,] { { 0, -current_gyro.GyroX, -current_gyro.GyroY , -current_gyro.GyroZ },
                {current_gyro.GyroX,0,current_gyro.GyroZ,-current_gyro.GyroY },
                { current_gyro.GyroY, -current_gyro.GyroZ, 0, current_gyro.GyroX},
                {current_gyro.GyroZ, current_gyro.GyroY,-current_gyro.GyroX,0 } });
            return omega * q / 2;
        }

        private MathNet.Numerics.LinearAlgebra.Vector<double> get_QuaternionMatrix()
        {
            Runge runge = new Runge();
            MathNet.Numerics.LinearAlgebra.Vector<double> q0 = DenseVector.OfArray(new double[] { 0,0,0,1});
            MathNet.Numerics.LinearAlgebra.Vector<double>  q = runge.runge(0, 1, q0, 0.01, new Runge.Function(quaternion_differential));

            return q;
        }

        public Matrix get_TransformMatrix()
        {
            MathNet.Numerics.LinearAlgebra.Vector<double> quaternion = get_QuaternionMatrix();

            double q0 = quaternion[0];
            double q1 = quaternion[1];
            double q2 = quaternion[2];
            double q3 = quaternion[3];

            double c11 = q0 * q0 + q1 * q1 - q2 * q2 - q3 * q3; double c12 = 2 * (q1 * q2 - q0 * q3); double c13 = 2 * (q1 * q3 + q0 * q2);
            double c21 = 2 * (q1 * q2 + q0 * q3); double c22 = q0 * q0 - q1 * q1 + q2 * q2 - q3 * q3; double c23 = 2 * (q2 * q3 - q0 * q3);
            double c31 = 2 * (q1 * q3 - q0 * q2); double c32 = 2 * (q2 * q3 + q0 * q1); double c33 = q0 * q0 - q1 * q1 - q2 * q2 + q3 * q3;

            Matrix transfrom_matrix = DenseMatrix.OfArray(new double[,] { { c11, c12, c13 }, { c21, c22, c23 }, { c32, c32, c33 } });

            return transfrom_matrix;
        }

        public struct Gyro
        {
            public double GyroX;
            public double GyroY;
            public double GyroZ;
        }
    }
}
