using UnityEngine;

public sealed class PoseAnimActor : MonoBehaviour
{
    [SerializeField] private Transform[] parts = new Transform[PoseAnimManager.PartCount];
    [SerializeField] private int clipId = 0;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool autoFindChildren = true;

    private int actorIndex = -1;

    private void Awake()
    {
        if (parts == null || parts.Length != PoseAnimManager.PartCount)
        {
            parts = new Transform[PoseAnimManager.PartCount];
        }

        if (autoFindChildren)
        {
            for (int i = 0; i < PoseAnimManager.PartCount; i++)
            {
                if (parts[i] == null && i < transform.childCount)
                {
                    parts[i] = transform.GetChild(i);
                }
            }
        }
    }

    private void OnEnable()
    {
        if (PoseAnimManager.Instance == null)
        {
            return;
        }

        actorIndex = PoseAnimManager.Instance.RegisterActor(parts);
        if (actorIndex >= 0)
        {
            PoseAnimManager.Instance.SetClip(actorIndex, clipId, true);
            if (playOnEnable)
            {
                PoseAnimManager.Instance.Play(actorIndex);
            }
            else
            {
                PoseAnimManager.Instance.Stop(actorIndex);
            }
        }
    }

    private void OnDisable()
    {
        if (PoseAnimManager.Instance == null)
        {
            return;
        }

        if (actorIndex >= 0)
        {
            PoseAnimManager.Instance.UnregisterActor(actorIndex);
            actorIndex = -1;
        }
    }
}
