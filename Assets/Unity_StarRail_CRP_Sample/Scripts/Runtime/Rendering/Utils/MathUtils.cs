using System;

namespace Unity_StarRail_CRP_Sample
{
    namespace MathUtils
    {
        public class DVector3
        {
            public double x;
            public double y;
            public double z;
    
            public DVector3(double x, double y, double z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
            
            public static DVector3 operator +(DVector3 v1, DVector3 v2)
            {
                return new DVector3(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z);
            }
            
            public static DVector3 operator -(DVector3 v1, DVector3 v2)
            {
                return new DVector3(v1.x - v2.x, v1.y - v2.y, v1.z - v2.z);
            }
            
            public static DVector3 operator *(DVector3 v1, double v2)
            {
                return new DVector3(v1.x * v2, v1.y * v2, v1.z * v2);
            }
            
            public static DVector3 operator /(DVector3 v1, double v2)
            {
                return new DVector3(v1.x / v2, v1.y / v2, v1.z / v2);
            }
            
            public static DVector3 operator -(DVector3 v1)
            {
                return new DVector3(-v1.x, -v1.y, -v1.z);
            }
            
            public static DVector3 Cross(DVector3 v1, DVector3 v2)
            {
                double x = v1.y * v2.z - v1.z * v2.y;
                double y = v1.z * v2.x - v1.x * v2.z;
                double z = v1.x * v2.y - v1.y * v2.x;
                
                return new DVector3(x, y, z);
            }
            
            public static double Dot(DVector3 v1, DVector3 v2)
            {
                return v1.x * v2.x + v1.y * v2.y + v1.z * v2.z;
            }

            public static DVector3 Normalize(DVector3 v)
            {
                double length = Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);

                return new DVector3(v.x / length, v.y / length, v.z / length);
            }
        }
    }
}