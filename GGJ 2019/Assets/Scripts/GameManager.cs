﻿using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using Cinemachine;

public class GameManager : MonoBehaviour
{
    [System.Serializable]
    public class Order
    {
    }

    [System.Serializable]
    public class Day
    {
        public PlayableDirector dayIntro;
        public PlayableDirector dayOutro;
        public PlayableDirector birdSequence;
        public List<House> possibleHouses;
        public HouseDisplay houseDisplay;
        public Bird bird;
        public float time = 90.0f;
        public bool extraCabinets;
    }

    [SerializeField]
    private PlayableDirector goPlayable;

    [SerializeField]
    private PlayerInput playerInput = null;

    [SerializeField]
    private HouseBuildManager houseBuildManager = null;

    [SerializeField]
    private ItemFlyIn itemFlyIn = null;

    [SerializeField]
    private CinemachineVirtualCamera vCamIntro;

    [SerializeField]
    private CinemachineVirtualCamera vCamResult;

    [SerializeField]
    private int goodReactionMistakeThreshold = 3;

    [SerializeField]
    private int mediumReactionMistakeThreshold = 10;

    [SerializeField]
    private GameObject goodReactionSpawnPoint = null;

    [SerializeField]
    private GameObject mediumReactionSpawnPoint = null;

    [SerializeField]
    private GameObject badReactionSpawnPoint = null;

    [SerializeField]
    private GameObject goodReactionPrefab = null;

    [SerializeField]
    private GameObject mediumReactionPrefab = null;

    [SerializeField]
    private GameObject badReactionPrefab = null;

    [SerializeField]
    private HouseBook houseBook = null;

    [SerializeField]
    private List<Day> days;

    [SerializeField]
    private GameTimer timer;

    [SerializeField]
    private PlayableDirector endCinematic;

    [SerializeField]
    private GameObject extraCabinets;

    private static List<Item> jokeItems = new List<Item> { Item.Brick, Item.Egg, Item.Fish, Item.FidgetSpinner };
    private static List<Item> necessaryItems = new List<Item> { Item.Hammer, Item.Log, Item.Nail, Item.Plank, Item.Saw };
    private PlayableDirector currentCinematic;
    private int dayIndex = 0;

    private void Start()
    {
        houseBuildManager.OnHouseCompleted = (x) => OnHouseCompleted(x, false);
        playerInput.enabled = false;

        Debug.Assert(days.Count > 0);
        StartDay();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentCinematic != null && currentCinematic.state == PlayState.Playing)
            {
                currentCinematic.time = currentCinematic.duration;
                currentCinematic.Evaluate();
                currentCinematic.Stop();
            }
            else if (itemFlyIn != null && itemFlyIn.IsInProgress())
            {
                itemFlyIn.CancelFlyIn();
            }
        }
    }

    void StartDay()
    {
        vCamResult.enabled = false;
        vCamIntro.enabled = true;
        playerInput.DisableMainVcams();

        extraCabinets.gameObject.SetActive(days[dayIndex].extraCabinets);
        houseBook.Clear();

        days[dayIndex].dayIntro.stopped += OnDayIntroComplete;
        days[dayIndex].dayIntro.Play();
        currentCinematic = days[dayIndex].dayIntro;
        RandomizeCabinets();

        AudioManager.Instance?.BirdChirpPlay();
    }
    
    private static System.Random rng = new System.Random();
    public static void Shuffle<T>(IList<T> list)  
    {  
        int n = list.Count;  
        while (n > 1) {  
            n--;  
            int k = rng.Next(n + 1);  
            T value = list[k];  
            list[k] = list[n];  
            list[n] = value;  
        }  
    }

    void RandomizeCabinets()
    {
        Cabinet[] cabinets = FindObjectsOfType<Cabinet>();
        Shuffle(cabinets);
        for (int i = 0; i < cabinets.Length; i++)
        {
            if (i < necessaryItems.Count)
            {
                cabinets[i].SetItem(necessaryItems[i]);
            }
            else
            {
                Item randomJunkItem = jokeItems[(int)Random.Range(0, jokeItems.Count)];
                cabinets[i].SetItem(randomJunkItem);
            }
        }
    }

    void OnDayIntroComplete(PlayableDirector playableDirector)
    {
        days[dayIndex].dayIntro.stopped -= OnDayIntroComplete;
        currentCinematic = null;
        itemFlyIn.DoFlyIn(StartOrder);
    }

    void StartOrder()
    {
        Day day = days[dayIndex];
        day.birdSequence.stopped += OnBirdSequenceComplete;
        day.birdSequence.Play();
        currentCinematic = day.birdSequence;

        vCamIntro.enabled = false;
    }

    void OnBirdSequenceComplete(PlayableDirector director)
    {
        Day day = days[dayIndex];
        day.birdSequence.stopped -= OnBirdSequenceComplete;

        House house = day.possibleHouses[(int)Random.Range(0, day.possibleHouses.Count)];

        currentCinematic = null;
        houseBuildManager.SpawnNewHouse(house);
        houseBook.Fill(houseBuildManager.currentHouse);
        playerInput.enabled = true;
        playerInput.EnableMainVcams();

        goPlayable.stopped += OnGoComplete;
        goPlayable.Play();
        currentCinematic = goPlayable;

        AudioManager.Instance?.BuildingMusicPlay();
    }

    void OnGoComplete(PlayableDirector director)
    {
        Day day = days[dayIndex];
        currentCinematic = null;

        timer.SetShown(true);
        timer.OnTimerCompleted = () => OnHouseCompleted(houseBuildManager.currentHouse, true);
        timer.StartTimer(day.time);
    }

    void OnHouseCompleted(House house, bool timeRanOut)
    {
        StartCoroutine(HouseCompletionCoroutine(house, timeRanOut));
    }

    IEnumerator HouseCompletionCoroutine(House house, bool timeRanOut)
    {
        timer.SetShown(false);
        timer.StopTimer();
        timer.OnTimerCompleted = null;

        playerInput.enabled = false;
        playerInput.DisableMainVcams();
        vCamResult.enabled = true;

        houseBuildManager.Cancel();

        foreach (Rigidbody rigidbody in house.gameObject.GetComponentsInChildren<Rigidbody>())
        {
            rigidbody.isKinematic = true;
        }

        if (timeRanOut)
        {
            AudioManager.Instance?.TimeUpPlay();
            yield return new WaitForSeconds(1.0f);
        }

        GameObject location = null;
        GameObject prefab = null;
        float result = 0;
        if (!house.IsComplete() || timeRanOut)
        {
            // Timer ran out
            location = badReactionSpawnPoint;
            prefab = badReactionPrefab;
            result = -1f;
        }
        else if (house.mistakes < goodReactionMistakeThreshold)
        {
            location = goodReactionSpawnPoint;
            prefab = goodReactionPrefab;
            result = 1f;
        }
        else if (house.mistakes < mediumReactionMistakeThreshold)
        {
            location = mediumReactionSpawnPoint;
            prefab = mediumReactionPrefab;
            result = 0f;
        }
        else
        {
            location = badReactionSpawnPoint;
            prefab = badReactionPrefab;
            result = -1f;
        }

        Instantiate(prefab, location.transform.position, location.transform.rotation, null);

        yield return new WaitForSeconds(3f);

        float startTime = Time.time;
        float animTime = 1f;
        Vector3 initial = house.transform.position;
        Vector3 target = initial + (-Vector3.right * 10.0f);
        while (Time.time - startTime < animTime)
        {
            house.transform.position = Util.EaseInOut(initial, target, Time.time - startTime, animTime);
            yield return null;
        }

        days[dayIndex].bird.SetReaction(result);
        days[dayIndex].houseDisplay.Fill(house, days[dayIndex].bird);

        NextDay();
    }

    void NextDay()
    {
        days[dayIndex].dayOutro.stopped += OutroFinished;
        days[dayIndex].dayOutro.Play();
        currentCinematic = days[dayIndex].dayIntro;
        
        AudioManager.Instance?.ShopMusicPlay();
    }

    void OutroFinished(PlayableDirector playableDirector)
    {
        days[dayIndex].dayOutro.stopped -= OutroFinished;
        currentCinematic = null;

        dayIndex++;
        if (dayIndex >= days.Count)
        {
            endCinematic.stopped += OnTotallyEnd;
            endCinematic.Play();
            currentCinematic = endCinematic;
        }
        else
        {
            StartDay();
        }
    }

    void OnTotallyEnd(PlayableDirector playableDirector)
    {
        endCinematic.stopped -= OnTotallyEnd;
        currentCinematic = null;
        SceneManager.LoadScene("Credits");
    }
}
