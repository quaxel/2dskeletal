using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct PoseEvalJobClip : IJobParallelFor
{
    public int clipId;
    public int partCount;
    public int maxFrames;
    public float deltaTime;

    [ReadOnly] public NativeArray<int> indices;
    [ReadOnly] public NativeArray<float3> kPos;
    [ReadOnly] public NativeArray<float> kRotZDeg;
    [ReadOnly] public NativeArray<float3> kScale;
    [ReadOnly] public NativeArray<int> clipFrameCount;
    [ReadOnly] public NativeArray<float> clipFps;
    [ReadOnly] public NativeArray<float> speed;
    [ReadOnly] public NativeArray<byte> playing;

    [NativeDisableParallelForRestriction] public NativeArray<float> time;
    [NativeDisableParallelForRestriction] public NativeArray<float3> outPos;
    [NativeDisableParallelForRestriction] public NativeArray<float> outRotZDeg;
    [NativeDisableParallelForRestriction] public NativeArray<float3> outScale;

    public void Execute(int index)
    {
        int actorIndex = indices[index];

        int frameCount = clipFrameCount[clipId];
        if (frameCount <= 0)
        {
            return;
        }

        float fps = clipFps[clipId];
        if (fps <= 0f)
        {
            return;
        }

        float t = time[actorIndex];
        if (playing[actorIndex] != 0)
        {
            float dt = deltaTime * speed[actorIndex];
            t += dt;

            float duration = frameCount / fps;
            if (duration > 0f)
            {
                if (t >= duration || t < 0f)
                {
                    t = t - math.floor(t / duration) * duration;
                }
            }
            else
            {
                t = 0f;
            }

            time[actorIndex] = t;
        }

        float frameF = t * fps;
        float frameFloor = math.floor(frameF);
        int frame0 = (int)frameFloor % frameCount;
        if (frame0 < 0)
        {
            frame0 += frameCount;
        }
        int frame1 = frame0 + 1;
        if (frame1 >= frameCount)
        {
            frame1 = 0;
        }

        float lerpT = frameF - frameFloor;

        int baseOut = actorIndex * partCount;
        int baseClip = clipId * partCount;

        for (int part = 0; part < partCount; part++)
        {
            int idx0 = ((baseClip + part) * maxFrames) + frame0;
            int idx1 = ((baseClip + part) * maxFrames) + frame1;

            float3 p0 = kPos[idx0];
            float3 p1 = kPos[idx1];
            float3 s0 = kScale[idx0];
            float3 s1 = kScale[idx1];
            float r0 = kRotZDeg[idx0];
            float r1 = kRotZDeg[idx1];

            float3 p = math.lerp(p0, p1, lerpT);
            float3 s = math.lerp(s0, s1, lerpT);
            float r = LerpAngleDeg(r0, r1, lerpT);

            int outIndex = baseOut + part;
            outPos[outIndex] = p;
            outScale[outIndex] = s;
            outRotZDeg[outIndex] = r;
        }
    }

    private static float LerpAngleDeg(float a, float b, float t)
    {
        float delta = math.fmod(b - a, 360f);
        if (delta > 180f)
        {
            delta -= 360f;
        }
        else if (delta < -180f)
        {
            delta += 360f;
        }
        return a + delta * t;
    }
}
