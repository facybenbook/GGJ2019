﻿using UnityEngine;
using System.Collections;

public class Nail : MonoBehaviour
{
    private Animator animator;
    public GameObject hammer;
    public GameObject goodNail;
    public GameObject badNail;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void SetShown(bool shown)
    {
        hammer.gameObject.SetActive(shown);
    }

    public void Finish(bool good, System.Action OnComplete)
    {
        animator.SetBool("DoHammer", true);
        StartCoroutine(DoThing(good, OnComplete));
    }

    private IEnumerator DoThing(bool good, System.Action OnComplete)
    {
        float greh = GetAnimationLength("HammerInNail") + GetAnimationLength("SawLog");
        yield return new WaitForSeconds(greh);

        hammer.gameObject.SetActive(false);
        goodNail.gameObject.SetActive(false);
        if (!good)
        {
            badNail.SetActive(true);
            badNail.transform.rotation = badNail.transform.rotation * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
        }

        OnComplete?.Invoke();
    }

    float GetAnimationLength(string name)
    {
        float time = 0;
        RuntimeAnimatorController ac = animator.runtimeAnimatorController;

        for (int i = 0; i < ac.animationClips.Length; i++)
        {
            if (ac.animationClips[i].name == name)
            {
                time = ac.animationClips[i].length;
                break;
            }
        }

        return time;
    }
}
