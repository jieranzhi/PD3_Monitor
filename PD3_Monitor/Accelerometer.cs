using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using System.Windows.Media.Media3D;
using System.Windows;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Collections;

namespace PD3_Monitor
{
    class Accelerometer
    {
        /**One MOST IMPORTANT note: the input should ALWAYS be the raw data**/
        public Acceleration current_acceleration;
        public double range;
        public string str_click; // LIS3DH only
        public double[] calibration_vector;
        public ArrayList array_list_calibration;

        public Accelerometer()
        {
            this.current_acceleration = new Acceleration();
            array_list_calibration = new ArrayList();
            calibration_vector = new double[3];
            update_value(2,0,0,1,"");
        }

        public void update_value(double range, double Ax, double Ay, double Az, string click)
        {
            this.range = range;
            this.current_acceleration.Ax = Ax;
            this.current_acceleration.Ay = Ay;
            this.current_acceleration.Az = Az;
            this.str_click = click;
            calibrate();
        }

        public void start_calibrate()
        {
            array_list_calibration = new ArrayList();
        }

        public bool calibrate()
        {
            int cnt = this.array_list_calibration.Count;
            if (cnt<500)
            {
                this.array_list_calibration.Add(new Vector3D(this.current_acceleration.Ax, this.current_acceleration.Ay, this.current_acceleration.Az));
            }
            else
            {
                double sum_x = 0, sum_y = 0, sum_z = 0;
                
                foreach(Vector3D v in array_list_calibration)
                {
                    sum_x += v.X;
                    sum_y += v.Y;
                    sum_z += v.Z;
                }
                calibration_vector = new double[] { sum_x / cnt, sum_y / cnt, sum_z / cnt - 1};
                return true;
            }
            return false;
        }

        /// <summary>
        /// Calculate the tilt angle by Tri-axis tilt sensing method
        /// </summary>
        /// <param name="Ax">Ax</param>
        /// <param name="Ay">Ay</param>
        /// <param name="Az">Az</param>
        /// <param name="g"></param>
        /// <returns>tilt angles</returns>
        /// <reference>http://www.st.com/web/en/resource/technical/document/application_note/CD00268887.pdf</reference>
        public Vector3D get_TiltAngle()
        {
            double Ax = this.current_acceleration.Ax;
            double Ay = this.current_acceleration.Ay;
            double Az = this.current_acceleration.Az;

            double sqrt_root = Math.Sqrt(Ax * Ax + Ay * Ay + Az * Az);
            double Ax1 = Ax / sqrt_root;
            double Ay1 = Ay / sqrt_root;
            double Az1 = Az / sqrt_root;

            double alpha = Math.Asin(Ax1); // pitch
            double beta = Math.Asin(Ay1); // roll
            double gamma = Math.Acos(Az1); // yaw

            if (Az < 0)
            {
                alpha = Math.PI - alpha;
            }

            return new Vector3D(alpha, beta, gamma);
        }

        public Matrix get_TransformMatrix()
        {
            Vector3D pry = get_TiltAngle();

            double alpha = pry.X;
            double beta = pry.Y;
            double gamma = pry.Z;

            double cos_alpha = Math.Cos(alpha);
            double sin_alpha = Math.Sin(alpha);
            double cos_beta = Math.Cos(beta);
            double sin_beta = Math.Sin(beta);
            double cos_gamma = Math.Cos(gamma);
            double sin_gamma = Math.Sin(gamma);

            Matrix Ce2b = DenseMatrix.OfArray(new double[,] {
                {cos_gamma*cos_beta, sin_gamma*cos_alpha+cos_gamma*sin_beta*sin_alpha, sin_gamma*sin_alpha-cos_gamma*sin_beta*cos_alpha},
                {-sin_gamma*cos_beta, cos_gamma*cos_alpha+cos_gamma*sin_beta*sin_alpha, cos_gamma*sin_alpha-sin_gamma*sin_beta*cos_alpha},
                {sin_beta, -cos_beta*sin_alpha, cos_beta*cos_alpha}
            });
            return Ce2b;
        }

        public MathNet.Numerics.LinearAlgebra.Double.Vector convert_to_NEDSystem(Matrix Ce2b)
        {
            MathNet.Numerics.LinearAlgebra.Double.Vector Ab = DenseVector.OfArray(new double[] { this.current_acceleration.Ax, this.current_acceleration.Ay, this.current_acceleration.Az });
            MathNet.Numerics.LinearAlgebra.Double.Vector G = DenseVector.OfArray(new double[] { 0, 0, 1});
            MathNet.Numerics.LinearAlgebra.Double.Vector Ae = (MathNet.Numerics.LinearAlgebra.Double.Vector)((Ce2b.TransposeThisAndMultiply(Ab)).Subtract(G));

            return Ae;
        }

        public struct Acceleration
        {
            public double Ax;
            public double Ay;
            public double Az;
        }
    }
}
