using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tag が Car / Walker のオブジェクトに加え、任意でプレイヤー自転車・警察オブジェクトも含め、
/// 警告表示中は速度ゼロ（Rigidbody kinematic 等）で一時停止します。
/// </summary>
public static class MobTrafficPause
{
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticsForEnterPlayMode()
    {
        _frozen = false;
        Rigidbodies.Clear();
        Agents.Clear();
        Animators.Clear();
        DupRigidbodies.Clear();
        DupAgents.Clear();
        DupAnimators.Clear();
    }
#endif
    struct RigidbodySnap
    {
        public Rigidbody Body;
        public bool WasKinematic;
        public bool UseGravity;
        public RigidbodyConstraints Constraints;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
    }

    struct AgentSnap
    {
        public NavMeshAgent Agent;
        public bool WasEnabled;
        public bool WasOnNavMesh;
        public float AngularSpeed;
        public bool WasStopped;
    }

    struct AnimatorSnap
    {
        public Animator Anim;
        public float Speed;
    }

    static bool _frozen;
    static readonly List<RigidbodySnap> Rigidbodies = new List<RigidbodySnap>();
    static readonly List<AgentSnap> Agents = new List<AgentSnap>();
    static readonly List<AnimatorSnap> Animators = new List<AnimatorSnap>();

    static readonly HashSet<Rigidbody> DupRigidbodies = new HashSet<Rigidbody>();
    static readonly HashSet<NavMeshAgent> DupAgents = new HashSet<NavMeshAgent>();
    static readonly HashSet<Animator> DupAnimators = new HashSet<Animator>();

    static readonly string[] MobTags = { "Car", "Walker" };

    public static bool IsFrozen => _frozen;

    /// <summary>Car / Walker のみ（後方互換）。</summary>
    public static void FreezeCarAndWalkerMobs()
    {
        FreezeForPoliceCatch(null, null);
    }

    /// <summary>
    /// Car / Walker 全員 + 自転車（player）+ 警察（police）を停止。null はスキップ。
    /// </summary>
    public static void FreezeForPoliceCatch(Transform playerBicycle, Transform policeRoot)
    {
        if (_frozen)
            return;

        Rigidbodies.Clear();
        Agents.Clear();
        Animators.Clear();
        DupRigidbodies.Clear();
        DupAgents.Clear();
        DupAnimators.Clear();

        foreach (var tag in MobTags)
        {
            try
            {
                var gos = GameObject.FindGameObjectsWithTag(tag);
                foreach (var go in gos)
                {
                    if (go == null)
                        continue;
                    FreezeHierarchy(go);
                }
            }
            catch (UnityException)
            {
                // Tag 未定義
            }
        }

        if (playerBicycle != null)
            FreezeHierarchy(playerBicycle.gameObject);
        if (policeRoot != null)
            FreezeHierarchy(policeRoot.gameObject);

        _frozen = true;
    }

    static void FreezeHierarchy(GameObject root)
    {
        if (root == null)
            return;

        var rbs = root.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rbs)
        {
            if (rb == null || !DupRigidbodies.Add(rb))
                continue;
            Rigidbodies.Add(new RigidbodySnap
            {
                Body = rb,
                WasKinematic = rb.isKinematic,
                UseGravity = rb.useGravity,
                Constraints = rb.constraints,
                Velocity = rb.velocity,
                AngularVelocity = rb.angularVelocity,
            });
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        var agents = root.GetComponentsInChildren<NavMeshAgent>(true);
        foreach (var agent in agents)
        {
            if (agent == null || !DupAgents.Add(agent))
                continue;
            Agents.Add(new AgentSnap
            {
                Agent = agent,
                WasEnabled = agent.enabled,
                WasOnNavMesh = agent.isOnNavMesh,
                AngularSpeed = agent.angularSpeed,
                WasStopped = agent.isStopped,
            });
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.angularSpeed = 0f;
            agent.isStopped = true;
            agent.enabled = false;
        }

        var anims = root.GetComponentsInChildren<Animator>(true);
        foreach (var anim in anims)
        {
            if (anim == null || !DupAnimators.Add(anim))
                continue;
            Animators.Add(new AnimatorSnap { Anim = anim, Speed = anim.speed });
            anim.speed = 0f;
        }
    }

    public static void UnfreezeCarAndWalkerMobs()
    {
        if (!_frozen)
            return;

        foreach (var s in Animators)
        {
            if (s.Anim != null)
                s.Anim.speed = s.Speed;
        }
        Animators.Clear();

        foreach (var s in Agents)
        {
            if (s.Agent == null)
                continue;
            s.Agent.enabled = s.WasEnabled;
            s.Agent.angularSpeed = s.AngularSpeed;
            s.Agent.isStopped = s.WasStopped;
            if (s.WasEnabled && s.WasOnNavMesh)
                s.Agent.ResetPath();
        }
        Agents.Clear();

        foreach (var s in Rigidbodies)
        {
            if (s.Body == null)
                continue;
            s.Body.isKinematic = s.WasKinematic;
            s.Body.useGravity = s.UseGravity;
            s.Body.constraints = s.Constraints;
            if (!s.WasKinematic)
            {
                s.Body.velocity = s.Velocity;
                s.Body.angularVelocity = s.AngularVelocity;
            }
        }
        Rigidbodies.Clear();

        DupRigidbodies.Clear();
        DupAgents.Clear();
        DupAnimators.Clear();

        _frozen = false;
        PatrolTrafficResume.AfterTrafficPauseUnfreeze();
    }
}
