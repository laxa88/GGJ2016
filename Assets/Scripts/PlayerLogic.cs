﻿using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class PlayerLogic : NetworkBehaviour {

	// This should only be handled by the host player.
	// NOTE:
	// 1 Round = when all players complete the number sequence
	// 1 Turn = one turn of player's correct button press

	[SyncVar]
	int playerScore = 100;

	[SyncVar]
	bool waitForNextRound = false;

	List<int> playerNumbers = new List<int>(); // player's numbers, e.g. [1,3,5,6,7]
	List<int> availableIdArray = new List<int>();
    PlayerScript _playerScript;

    void Awake()
    {
        _playerScript = GetComponent<PlayerScript>();
    }

	void Start ()
	{
		Game.playerCount++;
        HUD.instance.hideTitle();
		Random.seed = 12;

		if (isServer)
	    {
			Debug.Log("LOGGED IN AS SERVER");
			SetGameState(GameState.Init);
	    }
	    else
	    {
			Debug.Log("Logged in as client");
			CmdNewPlayerJoined();
	    }
	}

	void OnDestroy ()
	{
		Game.playerCount--;
	}

	void Update () 
	{
		if (!isLocalPlayer)
            return;

		if (Game.playerCount < 2 && Game.gameState != GameState.WaitingForPlayers)
			SetGameState(GameState.WaitingForPlayers);

		switch (Game.gameState)
		{
		case GameState.WaitingForPlayers:

			if (Game.playerCount > 1)
				SetGameState(GameState.WaitingForNextRound);

			break;

		case GameState.WaitingForNextRound:

			Game.nextRoundCountdown -= Time.deltaTime;

			if (Game.nextRoundCountdown <= 0f)
				SetGameState(GameState.RestartRound);

			break;

            case GameState.PlayingRound:

            Debug.Log("PlayingRound waiting for :" + Game.currentTurn);
            if (playerNumbers.Count > 0)
            {
                if (Input.GetButtonDown("Fire1"))
                {
                    string str = "";
                    for (int i = 0; i < playerNumbers.Count; i++)
                        str += playerNumbers[i] + ", ";
                    Debug.Log("UPDATE" + str);

                    Debug.Log("Send number : " + playerNumbers[0]);

                    if (isServer)
                        RpcPlayerPress(netId, playerNumbers[0]);
                    else
                        CmdPlayerPress(netId, playerNumbers[0]);
                }

                Game.currentTime -= Time.deltaTime;
                if (Game.currentTime <= 0)
                {
                    Game.currentTime = 5f;
                    if (playerScore <= 0)
                    {
                        playerScore = 0;
                    }
                    else
                    {
                        playerScore -= 5;
                    }
                }
                HUD.instance.UpdateCurrentNumber(playerNumbers[0]);
            }
            else
            {
                HUD.instance.UpdateCurrentNumber("none");
            }
            HUD.instance.UpdateRound(Game.currentRound);
			HUD.instance.UpdateTimer(Game.currentTime);
			HUD.instance.UpdateScore(playerScore);

			break;

        case GameState.GameOver:

			HUD.instance.UpdateRound(Game.currentRound);
			HUD.instance.UpdateTimer(Game.currentTime);
			HUD.instance.UpdateScore(playerScore);

        	// Only countdown and restart game if you're the server.
        	// Otherwise (for clients), wait for server.

        	Game.nextRoundCountdown -= Time.deltaTime;
        	HUD.instance.UpdateRestartText(Game.nextRoundCountdown);

			if (Game.nextRoundCountdown <= 0f)
            {
				Game.nextRoundCountdown = 0;
				SetGameState(GameState.None);

				if (isServer)
                	RpcSignalRestartRound();

				HUD.instance.UpdateRestartText("Waiting...");
            }

            break;
		}
	}

	void SetGameState(GameState newState)
	{
		Debug.Log("SetGameState : " + newState);

		Game.gameState = newState;

		switch (Game.gameState)
	    {
	    case GameState.Init:

	    	// This is when the the first player (host) is created,
	    	// so setup the server variables.
            HUD.instance.gameoverUI.SetActive(false);
			Game.currentTurn = 0;
			Game.currentRound = 0;
	        SetGameState(GameState.WaitingForPlayers);

	    	break;

	    case GameState.WaitingForPlayers:

	    	// This only happens when there's only one player,
	    	// either on init or all other players left.

	    	// In this state, the game doesn't start/countdown
	    	// until another player joins.

			break;

	    case GameState.WaitingForNextRound:

	    	// This is when there are enough players, and we
	    	// wait for a while before starting next round

	    	// TODO display the countdown to let everyone get ready
			Game.nextRoundCountdown = 2f;

			break;

		case GameState.RestartRound:

			HUD.instance.gameoverUI.SetActive(false);

			if (isServer)
				RpcDoRestartRound();
			/*else
				CmdDoRestartRound();*/

			break;

	    case GameState.PlayingRound:

	    	// Init variables before starting the round

	    	Game.currentTime = 5f;

			break;

        case GameState.GameOver:

            if (waitForNextRound)
            {
                waitForNextRound = false;
                tag = "Player";
            }

            // Makes sure the game over screen only shows once
            // for the localPlayer instead of for every connected player object.

            GameOver();
            
			break;

		default:
			Debug.Log("State not found : " + newState);
			break;
	    }
	}

	void CreateAvailableId()
	{
		int length = Game.totalTurns;

		availableIdArray = new List<int>();
	    //! Assign 0 - length
	    for (int i = 0; i < length; i++)
	    {
	        availableIdArray.Add(i);
	    }

	    //! Random swtich the index
	    for (int i = length-1; i > 0; i--)
	    {
	        // Randomize a number between 0 and i (so that the range decreases each time)
	        int rnd = Random.Range(0,i);

	        // Save the value of the current i, otherwise it'll overright when we swap the values
	        int temp = availableIdArray[i];

	        // Swap the new and old values
	        availableIdArray[i] = availableIdArray[rnd];
	        availableIdArray[rnd] = temp;
	    }
	}

	/*public bool CheckNumber (int numberToCheck)
	{
		if (Game.currentTurn == numberToCheck)
		{
			// If correct number, move to next turn (reset turn)
			Game.currentTurn++;
			Game.currentTime = 5f;

			if (Game.currentTurn >= Game.totalTurns)
				SetGameState(GameState.GameOver);

			return true;
		}
		else
			return false;
	}*/

	public void ClearNumbers ()
	{
		playerNumbers.Clear();
	}

	public void AddNumber (int num)
	{
		playerNumbers.Add(num);
	}

	public void SortNumbers ()
	{
		Debug.Log("Sort : " + playerNumbers);
		playerNumbers.Sort((int x, int y) => { return x.CompareTo(y); });
	}

	void AddScore(int score)
	{
		playerScore += score;
		if (playerScore <= 0)
			playerScore = 0;
	}

    void GameOver()
    {
    	// Show the game over screen for 5 seconds

		HUD.instance.gameoverUI.SetActive(true);
        Game.nextRoundCountdown = 5f;

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        List<int> playerScore = new List<int>();
        Debug.Log("Player length : " + players.Length);
        for (int i = 0; i < 5; i++)
        {
            if (i >= players.Length)
                break;
            playerScore.Add(players[i].GetComponent<PlayerLogic>().GetPlayerScore());
        }

        //Sort from the highest to lowest
        playerScore.Sort((int x, int y) => { return y.CompareTo(x); });

        //TODO
        //Check top 5 player
        //Update to HUD
        for (int i = 0; i < 5; i++)
        {
            if (i >= playerScore.Count)
                HUD.instance.UpdatePlayerResult(i,false, 0);//Empty
            else
                HUD.instance.UpdatePlayerResult(i,false, playerScore[i]);
        }
    }


	[Command]
	void CmdDoRestartRound ()
	{
		Debug.Log("CmdDoRestartRound");
		// DoRestartRound();
		RpcDoRestartRound();
	}

	[ClientRpc]
	void RpcDoRestartRound ()
	{
		Debug.Log("RpcDoRestartRound");
        DoRestartRound();
	}

	void DoRestartRound ()
	{
		Debug.Log("DoRestartRound");

        Game.currentTurn = 0;
        Game.currentRound++;
        Game.totalTurnPerPlayer = 0;

		// Set up total turn for each player count
		//Game.totalTurnPerPlayer = 2; // debug only
        if (Game.playerCount < 5)
            Game.totalTurnPerPlayer = Random.Range(5, 10);
		else if (Game.playerCount < 10)
            Game.totalTurnPerPlayer = Random.Range(3, 5);
		else if (Game.playerCount >= 10)
            Game.totalTurnPerPlayer = Random.Range(2, 3);

		GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        Game.totalTurns = players.Length * Game.totalTurnPerPlayer;

		// Create numbers for every player
		//! Assign available id to each player
        CreateAvailableId();

        /*string str = "";
		for (int i = 0; i < availableIdArray.Count; i++)
			str += availableIdArray[i] + ", ";
		HUD.instance.SetScore(str);*/

		for (int i = 0; i < players.Length; i++)
        {
        	Debug.Log("===== Assigning for player : " + i);

			players[i].GetComponent<PlayerLogic>().ClearNumbers();

			for (int j = 0; j < Game.totalTurnPerPlayer; j++)
	        {
				int number = j+(i*Game.totalTurnPerPlayer);
				players[i].GetComponent<PlayerLogic>().AddNumber(availableIdArray[number]);

				Debug.Log("assigned number : " + i + " , " + availableIdArray[number]);
	        }

			Debug.Log(playerNumbers.ToString());
	        Debug.Log("===== Done assigning : " + i);
			

			players[i].GetComponent<PlayerLogic>().SortNumbers();
        }

		SetGameState(GameState.PlayingRound);
	}

	[Command]
	void CmdPlayerPress (NetworkInstanceId nid, int number)
	{
		//Debug.Log("=====1 " + isServer + " , " + isClient + " , " + isLocalPlayer);
		Debug.Log("CmdPlayerPress");

		//PlayerPress(nid, number);
		RpcPlayerPress(nid, number);
	}

	[ClientRpc]
	void RpcPlayerPress (NetworkInstanceId nid, int number)
	{
		Debug.Log("RpcPlayerPress");
		PlayerPress(nid, number);
	}

	void PlayerPress (NetworkInstanceId nid, int number)
	{
		//Debug.Log("=====2 " + isServer + " , " + isClient + " , " + isLocalPlayer);
		Debug.Log("PlayerPress : " + nid + " = turn " + Game.currentTurn + " , number " + number);

		string str = "";
		for (int i = 0; i < playerNumbers.Count; i++)
			str += playerNumbers[i] + ", ";
		Debug.Log("PlayerPress curr playerNumbers : " + str);

		if (Game.currentTurn == number)
		{
			int score = Mathf.FloorToInt(Game.currentTime);
			AddScore(score);

			Debug.Log("Game Over at : " + Game.currentTurn + " , " + Game.totalTurns);

			Game.currentTurn++;
			Game.currentTime = 5f;
			playerNumbers.RemoveAt(0);

			if (isServer)
                RpcSignalCorrectAnswer();
            else
				CmdSignalCorrectAnswer();
        }
		else
		{
			AddScore(-5);

			if (isServer)
				RpcSignalWrongAnswer();
			else
				CmdSignalWrongAnswer();
		}

		if (Game.currentTurn >= Game.totalTurns)
        {
			SetGameState(GameState.GameOver);
        }
	}

	[Command]
	void CmdSignalCorrectAnswer ()
	{
		Debug.Log("CmdSignalCorrectAnswer");
		RpcSignalCorrectAnswer();
	}

	[ClientRpc]
	void RpcSignalCorrectAnswer ()
	{
		Debug.Log("RpcSignalCorrectAnswer");
		SignalCorrectAnswer();
	}

	void SignalCorrectAnswer ()
	{
		Debug.Log("SignalCorrectAnswer");
		_playerScript.PlayRightSound();
	}

	[Command]
	void CmdSignalWrongAnswer ()
	{
		Debug.Log("CmdSignalWrongAnswer");
		SignalWrongAnswer();
		RpcSignalWrongAnswer();
	}

	[ClientRpc]
	void RpcSignalWrongAnswer ()
	{
		Debug.Log("RpcSignalWrongAnswer");
		SignalWrongAnswer();
	}

	void SignalWrongAnswer ()
	{
		Debug.Log("SignalWrongAnswer");
		_playerScript.PlayWrongSound();
	}

	[ClientRpc]
	void RpcSignalRestartRound ()
	{
		Debug.Log("RpcSignalRestartRound");

		//HUD.instance.gameoverUI.SetActive(false);
		SetGameState(GameState.WaitingForNextRound);
	}

	[Command]
	void CmdNewPlayerJoined ()
	{
		RpcNewPlayerJoined();
	}

	[ClientRpc]
	void RpcNewPlayerJoined ()
	{
		if (Game.gameState == GameState.WaitingForNextRound)
		{
			Game.nextRoundCountdown = 2f;
		}
		else if (Game.gameState == GameState.PlayingRound)
		{
			// Change tag to make sure that this player does
			// not get listed during CmdDoRestartRound()
			waitForNextRound = true;
			tag = "PlayerInQueue";
		}
	}

    public int GetPlayerScore()
    {
        return playerScore;
    }

    public bool GetWaitNextRound()
    {
        return waitForNextRound;
    }
}