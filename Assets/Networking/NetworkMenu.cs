using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using Steamworks;
using Steamworks.Data;
using Netcode.Transports.Facepunch;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

[RequireComponent(typeof(FacepunchTransport))]
[RequireComponent(typeof(NetworkManager))]
public class NetworkMenu : MonoBehaviour
{
    public uint steamAppID;
    private NetworkManager _NetworkManager;
    private FacepunchTransport Transport;
    public Transform MenuObject;
    public int MenuScene;
    public int GameScene;
    public ulong CodeToJoin = 0;
    public static NetworkMenu instance;
    private bool joining_lobby;
    public bool StartAsHost;
    public Lobby? currentLobby { get; private set; } = null;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }


    // Start is called before the first frame update
    void Start()
    {
        if (instance == this)
        {
            if (!SteamClient.IsValid)
            {
                Application.Quit();
            }
            Transport = GetComponent<FacepunchTransport>();
            _NetworkManager = GetComponent<NetworkManager>();
            _NetworkManager.OnClientConnectedCallback += ClientConnected;
            SteamMatchmaking.OnLobbyCreated += SteamMatchmaking_OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += SteamMatchmaking_OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberLeave += SteamMatchmaking_OnLobbyMemberLeave;
            SteamMatchmaking.OnLobbyInvite += SteamMatchmaking_OnLobbyInvite;
            SteamMatchmaking.OnLobbyGameCreated += SteamMatchmaking_OnLobbyGameCreated;
            SteamFriends.OnGameLobbyJoinRequested += SteamFriends_OnGameLobbyJoinRequested;
            if (StartAsHost == true)
            {
                Host();
            }
        }

    }


    private async void SteamFriends_OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        if (currentLobby != null)
        {
            CodeToJoin = lobby.Id;
            joining_lobby = true;
            _NetworkManager.Shutdown();
            return;
        }
        RoomEnter joinedLobby = await lobby.Join();
        if (joinedLobby != RoomEnter.Success)
        {
            Debug.Log("Failed to create lobby");
        }
        else
        {
            Debug.Log("Joined lobby");
        }
    }

    private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
        if (joining_lobby)
        {
            StartCoroutine(JoinInvite());
            joining_lobby = false;
        }
        else
        {
            if (StartAsHost)
            {
                StartCoroutine(StartAsHostReconnect());
            }
        }
        SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
    }
    IEnumerator JoinInvite()
    {
        yield return new WaitForSeconds(1);
        Client();
    }
    IEnumerator StartAsHostReconnect()
    {
        yield return new WaitForSeconds(1);
        Host();
    }

    private void SteamMatchmaking_OnLobbyInvite(Friend friend, Lobby lobby)
    {
        Debug.Log($"Invite from {friend.Name}");
    }

    private void SteamMatchmaking_OnLobbyGameCreated(Lobby lobby, uint _ip, ushort _port, SteamId steamId)
    {
        Debug.Log("Lobby created");
    }

    private void SteamMatchmaking_OnLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        Debug.Log("Member left");
    }

    private void SteamMatchmaking_OnLobbyEntered(Lobby lobby)
    {
        Debug.Log("Entered lobby");
        if (_NetworkManager.IsHost)
        {
            return;
        }


        currentLobby = lobby;
        CodeToJoin = lobby.Id;
        Transport.targetSteamId = lobby.Owner.Id;
        if (Transport.targetSteamId != 0)
        {
            _NetworkManager.StartClient();
        }


    }

    private void SteamMatchmaking_OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            Debug.Log("Lobby was not created");
        }
        else
        {
            Debug.Log("Lobby was created");
            lobby.SetPublic();
            lobby.SetData("name", new System.Guid().ToString());
            lobby.SetJoinable(true);
            lobby.SetGameServer(lobby.Owner.Id);
            CodeToJoin = lobby.Id;
            _NetworkManager.StartHost();
        }
    }



    void Disconnected(ulong id)
    {
        if (id == _NetworkManager.LocalClientId)
        {
            LoadMenu();
        }
        _NetworkManager.OnClientDisconnectCallback -= Disconnected;
    }
    void LoadMenu()
    {
        currentLobby?.Leave();
        currentLobby = null;
        UnityEngine.SceneManagement.SceneManager.LoadScene(MenuScene);
        if (MenuObject)
        {
            MenuObject.gameObject.SetActive(true);
        }
        SceneManager.sceneLoaded += SceneManager_sceneLoaded;
    }

    void ClientConnected(ulong id)
    {
        if (MenuObject)
        {
            MenuObject.gameObject.SetActive(false);
        }
        if (_NetworkManager.IsHost)
        {
            string ScenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(GameScene);
            _NetworkManager.SceneManager.LoadScene(ScenePath, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }


        //callbakcs
        _NetworkManager.OnClientStopped += ClientStopped;
        _NetworkManager.OnClientDisconnectCallback += Disconnected;
    }

    public async void Host()
    {   
        currentLobby = await SteamMatchmaking.CreateLobbyAsync(100);
    }

    public async void Client()
    {
        await SteamMatchmaking.JoinLobbyAsync(CodeToJoin);
    }

    void ClientStopped(bool washosting)
    {
        LoadMenu();
        _NetworkManager.OnClientStopped -= ClientStopped;
    }

    // Update is called once per frame
    void Update()
    {
        if (!SteamClient.IsValid)
        {
            SteamClient.Init(steamAppID, false);
        }
    }
}
