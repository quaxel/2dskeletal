using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

public sealed class PoseAnimManager : MonoBehaviour
{
    public static PoseAnimManager Instance { get; private set; }

    public const int PartCount = 5;
    public const int ClipCount = 3;
    public const int MaxFrames = 5;
    public const int MaxActors = 600;

    [Header("Clip Data")]
    public PoseClipSO[] clips = new PoseClipSO[ClipCount];

    [Header("Defaults")]
    public int defaultClipId = 0;
    public float defaultSpeed = 1f;

    private Transform[] allPartsFlat;

    private NativeArray<float3> kPos;
    private NativeArray<float> kRotZDeg;
    private NativeArray<float3> kScale;
    private NativeArray<int> clipFrameCount;
    private NativeArray<float> clipFps;

    private NativeArray<float> time;
    private NativeArray<int> clipId;
    private NativeArray<float> speed;
    private NativeArray<byte> playing;
    private NativeArray<byte> active;
    private NativeArray<byte> visible;

    private NativeArray<float3> outPos;
    private NativeArray<float> outRotZDeg;
    private NativeArray<float3> outScale;

    private NativeArray<int> bucket0;
    private NativeArray<int> bucket1;
    private NativeArray<int> bucket2;
    private int bucket0Count;
    private int bucket1Count;
    private int bucket2Count;
    private int activeCount;

    private int[] freeStack;
    private int freeTop;

    private static readonly ProfilerMarker MarkerSchedule = new ProfilerMarker("PoseJob.Schedule");
    private static readonly ProfilerMarker MarkerComplete = new ProfilerMarker("PoseJob.Complete");
    private static readonly ProfilerMarker MarkerApply = new ProfilerMarker("Pose.ApplyTransforms");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        allPartsFlat = new Transform[MaxActors * PartCount];

        kPos = new NativeArray<float3>(ClipCount * PartCount * MaxFrames, Allocator.Persistent);
        kRotZDeg = new NativeArray<float>(ClipCount * PartCount * MaxFrames, Allocator.Persistent);
        kScale = new NativeArray<float3>(ClipCount * PartCount * MaxFrames, Allocator.Persistent);
        clipFrameCount = new NativeArray<int>(ClipCount, Allocator.Persistent);
        clipFps = new NativeArray<float>(ClipCount, Allocator.Persistent);

        time = new NativeArray<float>(MaxActors, Allocator.Persistent);
        clipId = new NativeArray<int>(MaxActors, Allocator.Persistent);
        speed = new NativeArray<float>(MaxActors, Allocator.Persistent);
        playing = new NativeArray<byte>(MaxActors, Allocator.Persistent);
        active = new NativeArray<byte>(MaxActors, Allocator.Persistent);
        visible = new NativeArray<byte>(MaxActors, Allocator.Persistent);

        outPos = new NativeArray<float3>(MaxActors * PartCount, Allocator.Persistent);
        outRotZDeg = new NativeArray<float>(MaxActors * PartCount, Allocator.Persistent);
        outScale = new NativeArray<float3>(MaxActors * PartCount, Allocator.Persistent);

        bucket0 = new NativeArray<int>(MaxActors, Allocator.Persistent);
        bucket1 = new NativeArray<int>(MaxActors, Allocator.Persistent);
        bucket2 = new NativeArray<int>(MaxActors, Allocator.Persistent);

        freeStack = new int[MaxActors];
        freeTop = MaxActors;
        for (int i = 0; i < MaxActors; i++)
        {
            freeStack[i] = MaxActors - 1 - i;
            speed[i] = defaultSpeed;
            clipId[i] = defaultClipId;
        }

        BuildPackedClips();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        DisposeArray(kPos);
        DisposeArray(kRotZDeg);
        DisposeArray(kScale);
        DisposeArray(clipFrameCount);
        DisposeArray(clipFps);
        DisposeArray(time);
        DisposeArray(clipId);
        DisposeArray(speed);
        DisposeArray(playing);
        DisposeArray(active);
        DisposeArray(visible);
        DisposeArray(outPos);
        DisposeArray(outRotZDeg);
        DisposeArray(outScale);
        DisposeArray(bucket0);
        DisposeArray(bucket1);
        DisposeArray(bucket2);
    }

    private static void DisposeArray<T>(NativeArray<T> arr) where T : struct
    {
        if (arr.IsCreated)
        {
            arr.Dispose();
        }
    }

    private void BuildPackedClips()
    {
        for (int clip = 0; clip < ClipCount; clip++)
        {
            PoseClipSO so = null;
            if (clips != null && clip < clips.Length)
            {
                so = clips[clip];
            }

            if (so == null)
            {
                clipFrameCount[clip] = 0;
                clipFps[clip] = 0f;
                continue;
            }

            int frameCount = math.clamp(so.frameCount, 1, MaxFrames);
            clipFrameCount[clip] = frameCount;
            clipFps[clip] = Mathf.Max(1, so.fps);

            int requiredLen = PartCount * frameCount;
            if (so.pos == null || so.rotZDeg == null || so.scale == null)
            {
                clipFrameCount[clip] = 0;
                clipFps[clip] = 0f;
                continue;
            }
            if (so.pos.Length < requiredLen || so.rotZDeg.Length < requiredLen || so.scale.Length < requiredLen)
            {
                clipFrameCount[clip] = 0;
                clipFps[clip] = 0f;
                continue;
            }

            for (int part = 0; part < PartCount; part++)
            {
                int partOffset = part * frameCount;
                int packedBase = (clip * PartCount + part) * MaxFrames;
                for (int frame = 0; frame < frameCount; frame++)
                {
                    int src = partOffset + frame;
                    int dst = packedBase + frame;
                    Vector3 p = so.pos[src];
                    Vector3 s = so.scale[src];
                    kPos[dst] = new float3(p.x, p.y, p.z);
                    kScale[dst] = new float3(s.x, s.y, s.z);
                    kRotZDeg[dst] = so.rotZDeg[src];
                }
            }
        }
    }

    private void Update()
    {
        BuildBuckets();

        JobHandle h0 = default;
        JobHandle h1 = default;
        JobHandle h2 = default;

        MarkerSchedule.Begin();

        if (bucket0Count > 0)
        {
            var job0 = CreateJob(0, bucket0, bucket0Count);
            h0 = job0.Schedule(bucket0Count, 64);
        }
        if (bucket1Count > 0)
        {
            var job1 = CreateJob(1, bucket1, bucket1Count);
            h1 = job1.Schedule(bucket1Count, 64);
        }
        if (bucket2Count > 0)
        {
            var job2 = CreateJob(2, bucket2, bucket2Count);
            h2 = job2.Schedule(bucket2Count, 64);
        }

        MarkerSchedule.End();

        MarkerComplete.Begin();
        JobHandle combined = JobHandle.CombineDependencies(h0, h1, h2);
        combined.Complete();
        MarkerComplete.End();

        MarkerApply.Begin();
        ApplyTransforms();
        MarkerApply.End();
    }

    private PoseEvalJobClip CreateJob(int clip, NativeArray<int> indices, int count)
    {
        return new PoseEvalJobClip
        {
            clipId = clip,
            partCount = PartCount,
            maxFrames = MaxFrames,
            deltaTime = Time.deltaTime,
            indices = indices,
            kPos = kPos,
            kRotZDeg = kRotZDeg,
            kScale = kScale,
            clipFrameCount = clipFrameCount,
            clipFps = clipFps,
            speed = speed,
            playing = playing,
            time = time,
            outPos = outPos,
            outRotZDeg = outRotZDeg,
            outScale = outScale
        };
    }

    private void BuildBuckets()
    {
        bucket0Count = 0;
        bucket1Count = 0;
        bucket2Count = 0;
        activeCount = 0;

        for (int actor = 0; actor < MaxActors; actor++)
        {
            if (active[actor] == 0 || visible[actor] == 0)
            {
                continue;
            }

            activeCount++;
            int c = clipId[actor];
            if (c == 0)
            {
                bucket0[bucket0Count++] = actor;
            }
            else if (c == 1)
            {
                bucket1[bucket1Count++] = actor;
            }
            else
            {
                bucket2[bucket2Count++] = actor;
            }
        }
    }

    public int ActiveCount => activeCount;
    public int Clip0Count => bucket0Count;
    public int Clip1Count => bucket1Count;
    public int Clip2Count => bucket2Count;

    private void ApplyTransforms()
    {
        int totalParts = MaxActors * PartCount;
        for (int i = 0; i < totalParts; i++)
        {
            Transform tr = allPartsFlat[i];
            if (tr == null)
            {
                continue;
            }

            int actor = i / PartCount;
            if (active[actor] == 0 || visible[actor] == 0)
            {
                continue;
            }

            float3 p = outPos[i];
            float3 s = outScale[i];
            float zDeg = outRotZDeg[i];

            Quaternion rot = (Quaternion)quaternion.RotateZ(math.radians(zDeg));
            tr.SetLocalPositionAndRotation(new Vector3(p.x, p.y, p.z), rot);
            tr.localScale = new Vector3(s.x, s.y, s.z);
        }
    }

    public int RegisterActor(Transform[] parts)
    {
        if (parts == null || parts.Length < PartCount)
        {
            return -1;
        }
        if (freeTop <= 0)
        {
            return -1;
        }

        int index = freeStack[--freeTop];
        int baseIndex = index * PartCount;
        for (int i = 0; i < PartCount; i++)
        {
            allPartsFlat[baseIndex + i] = parts[i];
        }

        active[index] = 1;
        visible[index] = 1;
        playing[index] = 1;
        time[index] = 0f;
        return index;
    }

    public void UnregisterActor(int actorIndex)
    {
        if (actorIndex < 0 || actorIndex >= MaxActors)
        {
            return;
        }
        if (active[actorIndex] == 0)
        {
            return;
        }

        int baseIndex = actorIndex * PartCount;
        for (int i = 0; i < PartCount; i++)
        {
            allPartsFlat[baseIndex + i] = null;
        }

        active[actorIndex] = 0;
        visible[actorIndex] = 0;
        playing[actorIndex] = 0;
        time[actorIndex] = 0f;

        freeStack[freeTop++] = actorIndex;
    }

    public void SetClip(int actorIndex, int newClipId, bool resetTime)
    {
        if (actorIndex < 0 || actorIndex >= MaxActors)
        {
            return;
        }
        if (newClipId < 0) newClipId = 0;
        if (newClipId >= ClipCount) newClipId = ClipCount - 1;

        clipId[actorIndex] = newClipId;
        if (resetTime)
        {
            time[actorIndex] = 0f;
        }
    }

    public void SetSpeed(int actorIndex, float newSpeed)
    {
        if (actorIndex < 0 || actorIndex >= MaxActors)
        {
            return;
        }
        speed[actorIndex] = newSpeed;
    }

    public void Play(int actorIndex)
    {
        if (actorIndex < 0 || actorIndex >= MaxActors)
        {
            return;
        }
        playing[actorIndex] = 1;
    }

    public void Stop(int actorIndex)
    {
        if (actorIndex < 0 || actorIndex >= MaxActors)
        {
            return;
        }
        playing[actorIndex] = 0;
    }
}
