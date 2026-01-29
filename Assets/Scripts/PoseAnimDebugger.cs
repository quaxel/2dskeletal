using UnityEngine;

public sealed class PoseAnimDebugger : MonoBehaviour
{
    private void OnGUI()
    {
        var mgr = PoseAnimManager.Instance;
        if (mgr == null)
        {
            return;
        }

        GUI.Label(new Rect(10, 10, 300, 20), "PoseAnim running");
        GUI.Label(new Rect(10, 30, 300, 20), "Actors: " + mgr.ActiveCount);
        GUI.Label(new Rect(10, 50, 300, 20), "Clip0: " + mgr.Clip0Count);
        GUI.Label(new Rect(10, 70, 300, 20), "Clip1: " + mgr.Clip1Count);
        GUI.Label(new Rect(10, 90, 300, 20), "Clip2: " + mgr.Clip2Count);
    }
}
