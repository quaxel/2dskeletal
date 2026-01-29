using UnityEngine;

[CreateAssetMenu(fileName = "PoseClip", menuName = "PoseAnim/PoseClip", order = 1)]
public sealed class PoseClipSO : ScriptableObject
{
    public const int PartCount = 5;

    [Min(1)] public int fps = 30;
    [Min(1)] public int frameCount = 1;

    [Tooltip("Length = PartCount * FrameCount")]
    public Vector3[] pos;

    [Tooltip("Length = PartCount * FrameCount")]
    public float[] rotZDeg;

    [Tooltip("Length = PartCount * FrameCount")]
    public Vector3[] scale;
}
