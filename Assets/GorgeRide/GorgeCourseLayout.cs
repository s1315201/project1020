using System;

namespace TaiwanGo
{
    public static class GorgeCourseLayout
    {
        public const float RiverHalfWidth=9f, RockBankLimit=8.8f, PathClearance=1.55f;
        public struct Rock { public float x,z,radius; }
        public struct Segment
        {
            public float start,middle,end;
            public Rock[] rocks;
            public float Center(float z){return z<19?start+(middle-start)*Clamp(z/19,0,1):middle+(end-middle)*Clamp((z-19)/19,0,1);}
        }
        public static Segment Scatter(Random random,float start,float difficulty)
        {
            float swing=3.3f+difficulty*1.2f;
            float middle=Clamp(start+(float)(random.NextDouble()*2-1)*swing,-3.8f,3.8f);
            if(Math.Abs(middle)<1.8f)middle=middle>=0?2.4f:-2.4f;
            middle=Clamp(middle,start-swing,start+swing);
            var result=new Segment{start=start,middle=middle,end=Clamp(middle+(float)(random.NextDouble()*2-1)*swing,-3.8f,3.8f)};
            int count=random.Next(5,7);result.rocks=new Rock[count];
            for(int i=0;i<count;i++)
            {
                float radius=1.6f+(float)random.NextDouble()*2.5f;
                if(i==0)radius=1.6f+(float)random.NextDouble()*.45f;
                float margin=radius+1.4f;
                float z=margin+(float)random.NextDouble()*(38-margin*2);
                if(i==0)z=16+(float)random.NextDouble()*6;
                // Protect the whole swept boat footprint around each rock, not just its center row.
                float lo=z-radius-.85f,hi=z+radius+.85f;
                float min=Math.Min(result.Center(lo),result.Center(hi)),max=Math.Max(result.Center(lo),result.Center(hi));
                if(lo<19&&hi>19){min=Math.Min(min,middle);max=Math.Max(max,middle);}
                const float clearance=PathClearance,bank=RockBankLimit;
                bool left=random.NextDouble()<.5;
                if(i==0)left=result.Center(z)>=0;
                float leftRoom=min-clearance+bank,rightRoom=bank-max-clearance;
                if((left?leftRoom:rightRoom)<3.2f)left=!left;
                float room=left?leftRoom:rightRoom;radius=Math.Min(radius,room*.47f);
                float a=left?-bank+radius:max+clearance+radius;
                float b=left?min-clearance-radius:bank-radius;
                float x=a+(b-a)*(float)random.NextDouble();
                // Deliberately mix a central blocker with shoreline and scattered boulders.
                if(i==0)x=Clamp(0,a,b);
                else if(i%3==1)x=left?a+(b-a)*.13f:b-(b-a)*.13f;
                result.rocks[i]=new Rock{x=x,z=z,radius=radius};
            }
            return result;
        }
        public struct Row
        {
            public float center, leftX, rightX, leftRadius, rightRadius;
        }
        public static Row Next(Random random, float previousCenter)
        {
            float center=Clamp(previousCenter+(float)(random.NextDouble()*5.4-2.7),-5.4f,5.4f);
            const float gap=3.15f, edge=11.7f;
            float leftSpace=center-gap+edge, rightSpace=edge-center-gap;
            float leftRadius=Math.Min(4.1f,leftSpace*.46f)*(float)(.87+random.NextDouble()*.13);
            float rightRadius=Math.Min(4.1f,rightSpace*.46f)*(float)(.87+random.NextDouble()*.13);
            return new Row {center=center,leftRadius=leftRadius,rightRadius=rightRadius,
                leftX=center-gap-leftRadius-(float)random.NextDouble()*.22f,
                rightX=center+gap+rightRadius+(float)random.NextDouble()*.22f};
        }
        static float Clamp(float x,float min,float max){return Math.Max(min,Math.Min(max,x));}
        public static float Elevation(float distance)
        {
            float step=(float)Math.Floor(distance/92f),phase=distance-step*92f;
            float location=59+(float)Math.Sin(step*1.73f)*7;
            float width=9+(float)Math.Sin(step*2.17f)*2;
            float t=Clamp((phase-location)/width,0,1);t=t*t*(3-2*t);
            return -distance*.022f+(float)Math.Sin(distance*.08f)*.14f-step*.95f-t*.95f;
        }
        public static bool SweptHit(float previousX,float currentX,float rockX,float rockZ,float advance,float radius)
        {
            float ax=rockX-previousX,az=rockZ+advance;
            float dx=previousX-currentX,dz=-advance;
            float length=dx*dx+dz*dz;
            float t=Clamp(-(ax*dx+az*dz)/Math.Max(length,.00001f),0,1);
            float x=ax+t*dx,z=az+t*dz;
            return x*x+z*z<radius*radius;
        }
        public static bool SweptBoatHit(float previousX,float currentX,float rockX,float rockZ,float advance,float rockRadius)
        {
            // Hull capsule: 0.84 m radius, 2.5 m center line. Its rounded bow reaches 2.09 m.
            return SweptHit(previousX,currentX,rockX,rockZ-1.25f,advance+2.5f,rockRadius+.84f);
        }
    }
}
