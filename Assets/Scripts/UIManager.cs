using System;
using System.Collections.Generic;
using _Project.CodeBase;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Fusion;
using MultiClimb.Match;

public class UIManager : MonoBehaviour
{
    public static UIManager Singleton { get; private set; }

    [Header("Refs")]
    [SerializeField] private PlayerRegistry playerRegistry;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI gameStateText;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private Slider breakCD;
    [SerializeField] private Image breakSelected;
    [SerializeField] private Slider cageCD;
    [SerializeField] private Image cageSelected;
    [SerializeField] private Slider shoveCD;
    [SerializeField] private Image shoveSelected;
    [SerializeField] private Slider grappleCD;
    [SerializeField] private Slider glideCD;
    [SerializeField] private Image glideActive;
    [SerializeField] private Slider doubleJumpCD;

    [SerializeField] private LeaderboardItem[] leaderboardItems;

    public Player LocalPlayer;

    private void Awake()
    {
        if (Singleton != null && Singleton != this) { Destroy(gameObject); return; }
        Singleton = this;

        breakCD.value = 0f;
        cageCD.value = 0f;
        shoveCD.value = 0f;
        grappleCD.value = 0f;
        glideCD.value = 0f;
        doubleJumpCD.value = 0f;

        SelectAbility(AbilityMode.BreakBlock);
    }

    private void OnEnable()
    {
        if (MatchEventBus.Instance == null)
        {
            Debug.LogError("[UIManager] MatchEventBus.Instance is null. " +
                           "Проверь что MatchEventBus.EnsureExists() работает.");
            return;
        }
 
        MatchEventBus.Instance.MatchStateChanged  += OnMatchStateChanged;
        MatchEventBus.Instance.LeaderboardChanged += OnLeaderboardChanged;
        MatchEventBus.Instance.RegistryReady      += OnRegistryReady;
    }

    private void OnDisable()
    {
        if (MatchEventBus.Instance == null) return;
 
        MatchEventBus.Instance.MatchStateChanged  -= OnMatchStateChanged;
        MatchEventBus.Instance.LeaderboardChanged -= OnLeaderboardChanged;
        MatchEventBus.Instance.RegistryReady      -= OnRegistryReady;
    }

    private void Update()
    {
        if (LocalPlayer == null) return;

        breakCD.value = LocalPlayer.BreakCDFactor;
        cageCD.value = LocalPlayer.CageCDFactor;
        shoveCD.value = LocalPlayer.ShoveCDFactor;
        grappleCD.value = LocalPlayer.GrappleCDFactor;
        doubleJumpCD.value = LocalPlayer.DoubleJumpCDFactor;

        glideActive.enabled = LocalPlayer.IsGliding;
        glideCD.value = LocalPlayer.IsGliding ? LocalPlayer.GlideCharge : LocalPlayer.GlideCDFactor;
    }

    private void OnMatchStateChanged(MatchStateChangedEvent e)
    {
        Player winner = null;

        if (e.Winner != PlayerRef.None && playerRegistry != null)
            playerRegistry.TryGet(e.Winner, out winner);

        SetWaitUI(e.State, winner);
    }

    private void OnLeaderboardChanged(LeaderboardChangedEvent e)
    {
        UpdateLeaderboard(e.Entries);
    }

    public void DidSetReady()
    {
        instructionText.text = "Waiting for other players to be ready...";
    }

    public void SetWaitUI(MatchState newState, Player winner)
    {
        if (newState == MatchState.Waiting)
        {
            if (winner == null)
            {
                gameStateText.text = "Waiting to Start";
                instructionText.text = "Press R when you're ready to begin!";
            }
            else
            {
                gameStateText.text = $"{winner.Name} Wins";
                instructionText.text = "Next round starting...";
            }
        }

        gameStateText.enabled = newState == MatchState.Waiting;
        instructionText.enabled = newState == MatchState.Waiting;
    }

    public void SelectAbility(AbilityMode mode)
    {
        breakSelected.enabled = mode == AbilityMode.BreakBlock;
        cageSelected.enabled = mode == AbilityMode.Cage;
        shoveSelected.enabled = mode == AbilityMode.Shove;
    }

    public void UpdateLeaderboard(KeyValuePair<PlayerRef, Player>[] players)
    {
        for (int i = 0; i < leaderboardItems.Length; i++)
        {
            var item = leaderboardItems[i];

            if (i < players.Length)
            {
                var p = players[i].Value;
                item.nameText.text = p.Name;
                item.heightText.text = $"Kills: {p.Kills} | Score: {p.Score}";
            }
            else
            {
                item.nameText.text = string.Empty;
                item.heightText.text = string.Empty;
            }
        }
    }

    private void OnRegistryReady(PlayerRegistry registry)
    {
        playerRegistry = registry;
    }

    [Serializable]
    private struct LeaderboardItem
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI heightText;
    }
}
