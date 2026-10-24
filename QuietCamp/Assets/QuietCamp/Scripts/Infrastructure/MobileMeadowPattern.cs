using System;

namespace QuietCamp.Infrastructure
{
    /// <summary>Deterministic packed meadow detail, baked once per process, never per frame.
    /// Ground RG = height/pigment, BA = signed normal slopes. Cards RGB = shade/pigment/height,
    /// A = silhouette. The numerical bake has no Unity dependency or authored scene writes.</summary>
    public static class MobileMeadowPattern
    {
        public const int GroundSize=256,CardWidth=256,CardHeight=64;
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
        public static byte[] Cards()
        {
            var data=new byte[CardWidth*CardHeight*4];var random=new Random(9821);
            const int blades=64;
            for(int blade=0;blade<blades;blade++)
            {
                double u=(blade+.15+random.NextDouble()*.7)/blades;
                double root=u*(CardWidth-16)+8;
                double envelope=.38+.62*Math.Sin(u*Math.PI);
                double height=(.45+random.NextDouble()*.51)*envelope*(CardHeight-2),lean=(random.NextDouble()-.5)*24;
                double width=1.8+random.NextDouble()*1.6,tone=.35+random.NextDouble()*.5;
                for(int y=0;y<=(int)height;y++)
                {
                    double t=y/height,centre=root+lean*t*t,span=width*(1-t*.90);
                    for(int x=Math.Max(1,(int)Math.Floor(centre-span-1));x<=Math.Min(CardWidth-2,(int)Math.Ceiling(centre+span+1));x++)
                    {
                        double coverage=Clamp(span+.5-Math.Abs(x+.5-centre));int i=(y*CardWidth+x)*4;
                        if(coverage*255<data[i+3])continue;
                        data[i]=Byte(.65+.35*t);data[i+1]=Byte(tone);data[i+2]=Byte(t);data[i+3]=Byte(coverage);
                    }
                }
            }
            return data;
        }
        // Ordinary averaging erases clipped blades at gameplay distance. Keep
        // the silhouette's occupied area in each mip instead of lowering the
        // clip threshold until the cards become solid rectangular strips.
        public static byte[][] CardMips()
        {
            var levels=new System.Collections.Generic.List<byte[]>();var data=Cards();
            int width=CardWidth,height=CardHeight;double coverage=Coverage(data);
            levels.Add(data);
            while(width>1||height>1)
            {
                int w=Math.Max(1,width/2),h=Math.Max(1,height/2);var next=new byte[w*h*4];
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)for(int c=0;c<4;c++)
                {
                    int sum=0,count=0;
                    for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)
                    {int px=x*2+dx,py=y*2+dy;if(px<width&&py<height){sum+=data[(py*width+px)*4+c];count++;}}
                    next[(y*w+x)*4+c]=(byte)(sum/count);
                }
                double lo=.25,hi=4;
                for(int iteration=0;iteration<12;iteration++)
                {
                    double scale=(lo+hi)*.5;int occupied=0;
                    for(int i=3;i<next.Length;i+=4)if(next[i]*scale>102)occupied++;
                    if(occupied/(double)(w*h)<coverage)lo=scale;else hi=scale;
                }
                for(int i=3;i<next.Length;i+=4)next[i]=(byte)Math.Min(255,Math.Round(next[i]*hi));
                levels.Add(next);data=next;width=w;height=h;
            }
            return levels.ToArray();
        }
        static double Coverage(byte[] data)
        {int count=0;for(int i=3;i<data.Length;i+=4)if(data[i]>102)count++;return count/(double)(data.Length/4);}
        static double Clamp(double value)=>Math.Max(0,Math.Min(1,value));
        static byte Byte(double value)=>(byte)Math.Round(Clamp(value)*255);
    }
}
