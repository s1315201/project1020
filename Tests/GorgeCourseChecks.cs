using System;
using TaiwanGo;
public static class GorgeCourseChecks
{
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Main()
    {
        for(int seed=0;seed<100;seed++)
        {
            var random=new Random(seed);float previous=0;
            for(int i=0;i<1000;i++)
            {
                var row=GorgeCourseLayout.Next(random,previous);
                Require(Math.Abs(row.center-previous)<=2.701f,"Corridor turns too sharply");
                Require(row.leftRadius>1.2f&&row.rightRadius>1.2f,"Obstacle too small");
                Require(row.rightX-row.rightRadius-(row.leftX+row.leftRadius)>=6.29f,"Blocked passage");
                Require(row.leftX-row.leftRadius>=-12&&row.rightX+row.rightRadius<=12,"Obstacle outside river");
                Require(Math.Abs(row.center)<=5.401f,"Corridor outside reachable area");
                previous=row.center;
            }
        }
        int scatteredRocks=0,centralRocks=0,bankRocks=0;
        for(int seed=0;seed<100;seed++)
        {
            var random=new Random(seed);float previous=0;
            for(int i=0;i<100;i++)
            {
                var segment=GorgeCourseLayout.Scatter(random,previous,i/100f);
                Require(segment.start==previous,"Discontinuous path at chunk seam");
                Require(segment.rocks.Length>=5&&segment.rocks.Length<=6,"Incorrect scatter count");
                Require(Math.Abs(segment.middle-segment.start)<=4.501f&&Math.Abs(segment.end-segment.middle)<=4.501f,"Unreachable path turn");
                foreach(var rock in segment.rocks)
                {
                    Require(rock.radius>=1.59f&&rock.radius<=4.101f,"Scatter radius invalid");
                    Require(rock.x-rock.radius>=-GorgeCourseLayout.RockBankLimit-.001f&&rock.x+rock.radius<=GorgeCourseLayout.RockBankLimit+.001f,"Scatter outside narrowed river");
                    Require(rock.z-rock.radius>.79f&&rock.z+rock.radius<37.21f,"Rock blocks an unprotected seam");
                    if(Math.Abs(rock.x)<2.5f)centralRocks++;
                    if(Math.Abs(rock.x)>5)bankRocks++;
                    for(float z=-1;z<=39;z+=.25f)
                    {
                        float dx=segment.Center(z)-rock.x,dz=z-rock.z;
                        Require(dx*dx+dz*dz>Math.Pow(rock.radius+.78f,2),"Reserved boat path collides with a scattered rock");
                        Require(!GorgeCourseLayout.SweptBoatHit(segment.Center(z),segment.Center(z),rock.x,rock.z-z,0,rock.radius),"Full hull cannot traverse reserved path");
                    }
                    scatteredRocks++;
                }
                previous=segment.end;
            }
        }
        Require(centralRocks>500&&bankRocks>500,"Course does not mix central and riverbank obstacles");
        for(float s=-100;s<5000;s+=.25f)
        {
            float height=GorgeCourseLayout.Elevation(s),next=GorgeCourseLayout.Elevation(s+.25f);
            Require(next<height,"River profile unexpectedly climbs uphill");
            Require(height-next<.065f,"Small descent contains an abrupt height discontinuity");
        }
        Require(GorgeCourseLayout.Elevation(1000)<-25,"River has no substantial downstream descent");
        Require(GorgeCourseLayout.SweptHit(0,0,0,-8,16,2),"Fast forward collision skipped");
        Require(!GorgeCourseLayout.SweptHit(0,0,7,-8,16,2),"Clear path falsely collides");
        Require(GorgeCourseLayout.SweptHit(-5,5,0,0,0,1),"Lateral crossing missed");
        Require(!GorgeCourseLayout.SweptHit(0,0,0,5,1,2),"Distant obstacle falsely collides");
        Require(GorgeCourseLayout.SweptHit(0,0,0,0,0,2),"Stationary overlap missed");
        Require(GorgeCourseLayout.SweptHit(10,0,5,0,0,1),"A falling rock crossing a stationary boat was missed");
        Require(!GorgeCourseLayout.SweptHit(2,0,8,0,0,1),"A falling rock passing far from the boat falsely collides");
        Require(GorgeCourseLayout.SweptBoatHit(0,0,0,2.9f,0,1),"Rounded bow collision missed");
        Require(GorgeCourseLayout.SweptBoatHit(0,0,0,-2.9f,0,1),"Rounded stern collision missed");
        Require(!GorgeCourseLayout.SweptBoatHit(0,0,3,0,0,1),"Hull side falsely collides");
        Require(!GorgeRunRules.CanSpawnRockfall(4.99f)&&!GorgeRunRules.CanSpawnRockfall(19.99f),"Rockfalls enter practice or early challenge");
        Require(GorgeRunRules.CanSpawnRockfall(20)&&GorgeRunRules.CanSpawnRockfall(24),"Final stage lacks rockfalls");
        Require(!GorgeRunRules.CanSpawnRockfall(29.9f),"A new rockfall starts after reaction time runs out");
        Require(GorgeRunRules.Duration==30&&GorgeRunRules.PracticeTime==5,"Requested timings changed");
        for(float t=0;t<30;t+=.05f)
        {
            Require(GorgeRunRules.TargetSpeed(t+.05f)>=GorgeRunRules.TargetSpeed(t),"Speed progression regresses");
            Require(GorgeRunRules.TargetSpeed(t+.05f)-GorgeRunRules.TargetSpeed(t)<.09f,"Stage speed changes abruptly");
        }
        Console.WriteLine("PASS: 10,000 courses with full hull clearance ("+scatteredRocks+" rocks), stage timing and speed transitions, 10 collision cases, elevation and legacy checks.");
    }
}
