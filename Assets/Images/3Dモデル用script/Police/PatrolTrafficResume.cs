using UnityEngine;

/// <summary>
/// 交通一時停止解除後に、巡回スクリプトへ NavMesh の目的地を付け直します。
/// </summary>
public static class PatrolTrafficResume
{
    public static void AfterTrafficPauseUnfreeze()
    {
        var a = Object.FindObjectsOfType<PatrolWaypoints>(true);
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != null)
                a[i].NotifyResumeFromTrafficPause();
        }

        var b = Object.FindObjectsOfType<PatrolWaypointsRandom>(true);
        for (int i = 0; i < b.Length; i++)
        {
            if (b[i] != null)
                b[i].NotifyResumeFromTrafficPause();
        }

        var c = Object.FindObjectsOfType<PatrolWaypointsBranchRandom>(true);
        for (int i = 0; i < c.Length; i++)
        {
            if (c[i] != null)
                c[i].NotifyResumeFromTrafficPause();
        }
    }
}
