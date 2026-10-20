#ifndef TAIWAN_GO_GORGE_PROFILE
#define TAIWAN_GO_GORGE_PROFILE
float _GorgeDistance;
float _GorgeOriginHeight;
float GorgeElevation(float distance)
{
    float stepIndex=floor(distance/92.0),phase=distance-stepIndex*92.0;
    float location=59+sin(stepIndex*1.73)*7;
    float width=9+sin(stepIndex*2.17)*2;
    float t=saturate((phase-location)/width);t=t*t*(3-2*t);
    return -distance*.022+sin(distance*.08)*.14-stepIndex*.95-t*.95;
}
float GorgeRelativeHeight(float worldZ)
{
    return GorgeElevation(_GorgeDistance+worldZ)-_GorgeOriginHeight;
}
#endif
