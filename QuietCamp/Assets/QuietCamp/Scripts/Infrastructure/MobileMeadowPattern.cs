using System;

namespace QuietCamp.Infrastructure
{
    /// <summary>Deterministic packed meadow detail, baked once per process, never per frame.
    /// Ground RG = height/pigment, BA = signed normal slopes. The numerical bake has no Unity dependency or authored scene writes.</summary>
    public static class MobileMeadowPattern
    {
        public const int GroundSize=256;
        public static byte[] Ground()
        {
            int n=GroundSize;var data=new byte[n*n*4];var random=new Random(4157);
            for(int i=0;i<n*n;i++)
            {data[i*4]=42;data[i*4+1]=96;data[i*4+2]=128;data[i*4+3]=128;}
            for(int blade=0;blade<22000;blade++)
            {
                double x=random.NextDouble()*n,y=random.NextDouble()*n,angle=random.NextDouble()*Math.PI*2;
                double length=5+random.NextDouble()*15,width=.65+random.NextDouble()*.65;
                double dx=Math.Cos(angle),dy=Math.Sin(angle),tone=.32+random.NextDouble()*.60;
                int x0=(int)Math.Floor(Math.Min(x,x+dx*length)-width-1),x1=(int)Math.Ceiling(Math.Max(x,x+dx*length)+width+1);
                int y0=(int)Math.Floor(Math.Min(y,y+dy*length)-width-1),y1=(int)Math.Ceiling(Math.Max(y,y+dy*length)+width+1);
                for(int py=y0;py<=y1;py++)for(int px=x0;px<=x1;px++)
                {
                    double qx=px+.5-x,qy=py+.5-y,along=(qx*dx+qy*dy)/length;
                    if(along<0||along>1)continue;
                    double across=-qx*dy+qy*dx,span=width*(1-along*.88),coverage=Clamp(span+.55-Math.Abs(across));
                    if(coverage<=0)continue;
                    double height=(.35+.65*Math.Sin(along*Math.PI*.75))*coverage;
                    int i=(((py%n+n)%n)*n+(px%n+n)%n)*4;
                    if(height*255<data[i])continue;
                    data[i]=Byte(height);data[i+1]=Byte(tone*(.7+.3*along));
                    double ridge=Clamp(.5+across/(span+.001))-.5;
                    data[i+2]=Byte(.5-dy*ridge*.7);data[i+3]=Byte(.5+dx*ridge*.7);
                }
            }
            return data;
        }
        static double Clamp(double value)=>Math.Max(0,Math.Min(1,value));
        static byte Byte(double value)=>(byte)Math.Round(Clamp(value)*255);
    }
}
