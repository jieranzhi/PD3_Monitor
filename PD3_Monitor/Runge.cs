using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace PD3_Monitor
{
    // Reference:　http://csharpcomputing.com/Tutorials/Lesson16.htm
    class Runge
    {
        //fourth order Runge Kutte method for y'=f(t,y);
        //solve first order ode in the interval (a,b) with a given initial condition at x=a and fixed step h.
        public delegate MathNet.Numerics.LinearAlgebra.Vector<double> Function(double t, MathNet.Numerics.LinearAlgebra.Vector<double> q); //declare a delegate that takes a double and returns
        public MathNet.Numerics.LinearAlgebra.Vector<double> runge(double a, double b, MathNet.Numerics.LinearAlgebra.Vector<double> initial_value, double step, Function f)
        {
            MathNet.Numerics.LinearAlgebra.Vector<double> q, k1, k2, k3, k4;
            double t = a;
            q = initial_value;
            for (int i = 0; i < (b - a) / step; i++)
            {
                k1 = (MathNet.Numerics.LinearAlgebra.Double.Vector)f(t, q).Multiply(step);
                k2 = f(t + step / 2, (q + k1 / 2)).Multiply(step);
                k3 = f(t + step / 2, (q + k2 / 2)).Multiply(step);
                k4 = f(t + step, (q + k3)).Multiply(step);
                q = q + (k1 + 2 * k2 + 2 * k3 + k4) / 6;
                t = a + i * step;
            }
            return q;
        }
    }
}
