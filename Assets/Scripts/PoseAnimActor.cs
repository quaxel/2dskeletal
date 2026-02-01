using UnityEngine;

public sealed class PoseAnimActor : MonoBehaviour
{
    [SerializeField] private Transform[] parts = new Transform[PoseAnimManager.PartCount];
    [SerializeField] private int clipId = 0;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool autoFindChildren = true;

    private int actorIndex = -1;
    private bool registered;

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
        registered = false;
        TryRegister();
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
            registered = false;
        }
    }

    private void Start()
    {
        if (!registered)
        {
            TryRegister();
        }
    }

    private void TryRegister()
    {
        var mgr = PoseAnimManager.Instance;
        if (mgr == null)
        {
            return;
        }

        actorIndex = mgr.RegisterActor(parts);
        if (actorIndex >= 0)
        {
            registered = true;
            mgr.SetClip(actorIndex, clipId, true);
            if (playOnEnable)
            {
                mgr.Play(actorIndex);
            }
            else
            {
                mgr.Stop(actorIndex);
            }
        }
    }
}
