using System.Collections;
using System.Collections.Generic;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine;

public sealed class PrefabSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int count = 30;
    [SerializeField] private float minX = -3f;
    [SerializeField] private float maxX = 3f;
    [SerializeField] private float y = 0f;
    [SerializeField] private float minZ = 10f; // > 9
    [SerializeField] private float maxZ = 20f;
    [SerializeField] private float spawnDelaySeconds = 0.02f;
    [SerializeField] private AnimationClip walkClip;
    [SerializeField] private bool useSharedPlayableGraph = true;
    [SerializeField] private float cullDistance = 25f;
    [SerializeField] private float cullUpdateIntervalSeconds = 0.5f;

    private PlayableGraph sharedGraph;
    private readonly List<PlayableGraph> perInstanceGraphs = new List<PlayableGraph>();
    private readonly List<Animator> spawnedAnimators = new List<Animator>();

    private void Start()
    {
        if (prefab == null)
        {
            Debug.LogError("PrefabSpawner: prefab not assigned.", this);
            enabled = false;
            return;
        }

        if (walkClip == null)
        {
            Debug.LogError("PrefabSpawner: walkClip not assigned.", this);
            enabled = false;
            return;
        }

        if (useSharedPlayableGraph)
        {
            sharedGraph = PlayableGraph.Create("PrefabSpawnerGraph");
            sharedGraph.Play();
        }

        StartCoroutine(SpawnRoutine());
        StartCoroutine(CullingRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(minX, maxX);
            float z = Random.Range(minZ, maxZ);
            Vector3 position = new Vector3(x, y, z);
            GameObject instance = Instantiate(prefab, position, Quaternion.Euler(60f, 0f, 0f), transform);
            BindPlayableAnimation(instance);

            if (spawnDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(spawnDelaySeconds);
            }
        }
    }

    private void BindPlayableAnimation(GameObject instance)
    {
        Animator animator = instance.GetComponent<Animator>();
        if (animator == null)
        {
            animator = instance.AddComponent<Animator>();
        }
        ApplyCulling(animator, instance.transform.position);
        spawnedAnimators.Add(animator);

        if (useSharedPlayableGraph)
        {
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(sharedGraph, instance.name, animator);
            AnimationClipPlayable sharedInstanceClip = AnimationClipPlayable.Create(sharedGraph, walkClip);
            output.SetSourcePlayable(sharedInstanceClip);
            return;
        }

        PlayableGraph graph = PlayableGraph.Create(instance.name + "_Graph");
        AnimationClipPlayable instanceClip = AnimationClipPlayable.Create(graph, walkClip);
        AnimationPlayableOutput instanceOutput = AnimationPlayableOutput.Create(graph, instance.name, animator);
        instanceOutput.SetSourcePlayable(instanceClip);
        graph.Play();
        perInstanceGraphs.Add(graph);
    }

    private IEnumerator CullingRoutine()
    {
        while (true)
        {
            if (cullUpdateIntervalSeconds <= 0f)
            {
                yield return null;
                continue;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null && cullDistance > 0f)
            {
                for (int i = spawnedAnimators.Count - 1; i >= 0; i--)
                {
                    Animator animator = spawnedAnimators[i];
                    if (animator == null)
                    {
                        spawnedAnimators.RemoveAt(i);
                        continue;
                    }

                    float distance = Vector3.Distance(mainCamera.transform.position, animator.transform.position);
                    animator.cullingMode = distance > cullDistance
                        ? AnimatorCullingMode.CullCompletely
                        : AnimatorCullingMode.CullUpdateTransforms;
                }
            }

            yield return new WaitForSeconds(cullUpdateIntervalSeconds);
        }
    }

    private void ApplyCulling(Animator animator, Vector3 worldPosition)
    {
        if (cullDistance <= 0f)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return;
        }

        float distance = Vector3.Distance(mainCamera.transform.position, worldPosition);
        animator.cullingMode = distance > cullDistance
            ? AnimatorCullingMode.CullCompletely
            : AnimatorCullingMode.CullUpdateTransforms;
    }

    private void OnDestroy()
    {
        if (sharedGraph.IsValid())
        {
            sharedGraph.Destroy();
        }

        for (int i = 0; i < perInstanceGraphs.Count; i++)
        {
            if (perInstanceGraphs[i].IsValid())
            {
                perInstanceGraphs[i].Destroy();
            }
        }
    }
}
