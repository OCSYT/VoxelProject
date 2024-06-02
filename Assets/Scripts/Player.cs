using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Netcode.Transports.Facepunch;
using Unity.Netcode;
using Steamworks;
using Unity.Collections;
using System.IO;
using System;
[RequireComponent(typeof(CharacterController))]
public class Player : NetworkBehaviour
{
    public GameObject ChatPrefab;
    public Transform ChatContent;
    public TMP_InputField ChatInput;
    public GameObject ChatCanvas;
    public Transform UsernameRoot;
    public int PlayerModelLayer;
    public GameObject PlayerModel;
    public ChunkManager chunkManager;
    public BlockPlace BlockPlace;
    public Animator Anim;
    public Transform Head;
    private CharacterController controller;
    private Vector3 playerVelocity;
    private bool groundedPlayer;
    public float playerSpeed = 2.0f;
    public float jumpHeight = 1.0f;
    public float gravityValue = -9.81f;
    private int camVal;
    public Camera playerCamera;
    public Camera playerCameraBack;
    public Camera playerCameraFront;
    public float mouseSensitivity = 100.0f;
    private float xRotation = 0f;
    public LayerMask ignore;
    private int deltaTimeCount;
    private float maxDeltaTime;
    private float averageDeltaTime;
    public Transform Hand;
    public float HandAmount;
    public float HandAmountX;
    public float HandAmountY;
    private float MovementDotY;
    private Vector3 ChunkPosition;
    private bool PlayerInChunk;
    public bool AllowMovement;
    private bool AllowMovementUpdate;

    public GameObject pauseMenuUI;
    public GameObject LoadingScreen;
    public GameObject HandUI;
    public TextMeshProUGUI CodeText;
    public Slider RenderDistanceSlider;
    public Slider SensitivitySlider;
    public TextMeshProUGUI RenderDistanceText;
    public TextMeshProUGUI SensitivityText;
    public Toggle graphicsToggle;

    public bool IsPaused = false;
    public bool Chatting = false;
    private Player localPlayer;

    [HideInInspector]
    public NetworkVariable<float> GameTime = 
        new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [HideInInspector]
    public NetworkVariable<FixedString32Bytes> Username =
        new NetworkVariable<FixedString32Bytes>(new FixedString32Bytes(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [HideInInspector]
    public NetworkVariable<bool> Synced = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [HideInInspector]
    public NetworkVariable<bool> Hosting = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [HideInInspector]
    public NetworkVariable<bool> Moving = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private string TargetWorldName;
    private bool Teleporting = false;
    public void Teleport(Vector3 Position, float EulerAngle, bool MakeNotInGround)
    {
        Teleporting = true;
        Position = Vector3Int.FloorToInt(Position) + new Vector3(0.5f, 0.5f, 0.5f);
        controller.enabled = false;
        transform.position = Position;
        transform.eulerAngles = new Vector3(0, EulerAngle, 0);
        xRotation = 0;
        StartCoroutine(WaitForChunkTeleport(Position, MakeNotInGround));
    }

    public void TeleportPlayerCorrect(Vector3 Position)
    {
        Teleporting = true;
        controller.enabled = false;
        transform.position = Position;
        Teleporting = false;
    }

    IEnumerator WaitForChunkTeleport(Vector3 Position, bool MakeNotInGround)
    {
        if (MakeNotInGround)
        {
            Chunk currentChunk = chunkManager.GetChunk(transform.position);
            bool inChunk = currentChunk != null;
            AllowMovementUpdate = false;
            if (!inChunk)
            {
                yield return new WaitForSeconds(3);
            }
            float yPos = 0;
            RaycastHit hit;
            if (Physics.Raycast(Position + Vector3.up * 100, Vector3.down, out hit, Mathf.Infinity, ~ignore))
            {
                yPos = hit.point.y + 1;
                transform.position = new Vector3(Position.x, yPos, Position.z);
            }
            AllowMovementUpdate = true;
        }

        Teleporting = false;
    }


    private void Awake()
    {
        controller = gameObject.GetComponent<CharacterController>();
        controller.enabled = false;
    }

    private void Start()
    {

        if (IsOwner)
        {
            Hosting.Value = IsHost;
            Username.Value = (FixedString32Bytes)SteamClient.Name;
            LoadingScreen.SetActive(true);
            UsernameRoot.gameObject.SetActive(false);

            RenderDistanceSlider.value = PlayerPrefs.GetInt("RenderDistance", 8);
            SensitivitySlider.value = PlayerPrefs.GetFloat("Sensitivity", 100);

            // Set the default graphics settings to high
            graphicsToggle.isOn = PlayerPrefs.GetInt("Graphics", 1) == 1;
            SetGraphicsSettings(graphicsToggle.isOn);

            // Add listener to the toggle
            graphicsToggle.onValueChanged.AddListener(delegate
            {
                SetGraphicsSettings(graphicsToggle.isOn);
            });
            CodeText.text = NetworkMenu.instance.CodeToJoin.ToString();
        }
        else
        {
            chunkManager.gameObject.SetActive(false);
            playerCamera.gameObject.SetActive(false);
            PlayerModel.layer = PlayerModelLayer;
        }
        gameObject.name = Username.Value.ToString();

        TargetWorldName = NetworkMenu.instance.currentLobby.Value.GetData("WorldName");
        if (NetworkManager.IsHost)
        {
            Player host = GetHost();
            if (Synced.Value == false && (this != host))
            {
                host.chunkManager.SyncSave(this.OwnerClientId);
            }
            host.SyncRequestServerRPC();
        }
    }

    public void CopyCode()
    {
        GUIUtility.systemCopyBuffer = NetworkMenu.instance.CodeToJoin.ToString();
    }


    private void OnDestroy()
    {
        if (NetworkManager.IsHost)
        {
            GetHost().BlockPlace.BufferedBlockEvents.Clear();
            GetHost().chunkManager.SaveGame(Application.dataPath + "/../" + "Saves/" + PlayerPrefs.GetString("WorldName") + ".dat");
        }
        SendChatMessageLocal("<color=yellow>" + Username.Value.ToString() + " has left");
    }
    public Player GetHost()
    {
        foreach (Player p in GameObject.FindObjectsOfType<Player>())
        {
            if (p.Hosting.Value)
            {
                return p;
            }
        }
        return null;
    }
    public Player GetLocal()
    {
        foreach (Player p in GameObject.FindObjectsOfType<Player>())
        {
            if (p.OwnerClientId == NetworkManager.LocalClientId)
            {
                return p;
            }
        }
        return null;
    }
    public Player GetPlayerByName(string username)
    {
        foreach (Player p in GameObject.FindObjectsOfType<Player>())
        {
            if (p.Username.Value.ToString() == username)
            {
                return p;
            }
        }
        return null;
    }


    [ServerRpc(RequireOwnership =false)]
    public void SyncRequestServerRPC()
    {
        List<(Vector3, byte)> BlockEvents = BlockPlace.BufferedBlockEvents;
        Vector3[] VecList = new Vector3[BlockEvents.Count];
        byte[] ByteList = new byte[BlockEvents.Count];
        for (int i = 0; i < VecList.Length; i++)
        {
            VecList[i] = BlockEvents[i].Item1;
            ByteList[i] = BlockEvents[i].Item2;
        }

        SyncRequestClientRPC(VecList, ByteList);
    }
    [ClientRpc]
    public void SyncRequestClientRPC(Vector3[] VecList, byte[] ByteList)
    {
        List<(Vector3, byte)> BlockEvents = new List<(Vector3, byte)>();
        for (int i = 0; i < VecList.Length; i++)
        {
            BlockEvents.Add((VecList[i], ByteList[i]));
        }
        StartCoroutine(WaitForPlayer(BlockEvents));
    }
    IEnumerator WaitForPlayer(List<(Vector3, byte)> BlockEvents)
    {
        yield return new WaitUntil(() => localPlayer != null);


        if (localPlayer.Synced.Value == false)
        {
            StartCoroutine(localPlayer.InitSync(BlockEvents));
        }
    }


    IEnumerator InitSync(List<(Vector3, byte)> BlockEvents)
    {
        if (!Synced.Value)
        {

            yield return new WaitUntil(() => ChunkManager.Instance != null);
            BlockPlace.BufferedBlockEvents = BlockEvents;
            BlockPlace.PlaceBufferedBlocks();
            Cursor.lockState = CursorLockMode.Locked;
            if (!IsHost)
            {
                while(File.Exists(Application.dataPath + "/../" + "SaveCache/" + TargetWorldName + ".dat") == false)
                {
                    yield return new WaitForSeconds(1);
                }
            }
            chunkManager.StartGenerating();
            StartCoroutine(WaitForInit());
        }
        if(Synced.Value == false)
        {
            Synced.Value = true;
        }
    }

    public bool Spawned = false;
    IEnumerator WaitForInit()
    {
        yield return new WaitUntil(() => chunkManager.SpawnPosition != Vector3.zero);
        yield return new WaitForSeconds(3);
        Spawned = true;
        LoadingScreen.SetActive(false);
        SendChatMessageServerRPC("<color=yellow>" + Username.Value.ToString() + " has joined");
    }

    [ServerRpc]
    void SendChatMessageServerRPC(string message)
    {
        SendChatMessageClientRPC(message);
    }
    [ClientRpc]
    void SendChatMessageClientRPC(string message)
    {
        foreach (Player p in GameObject.FindObjectsOfType<Player>())
        {
            Transform chatContentTransform = p.ChatContent;
            // Limit to 5 children
            if (chatContentTransform.childCount >= 5)
            {
                Destroy(chatContentTransform.GetChild(0).gameObject);
            }

            // Instantiate the new chat message
            TextMeshProUGUI Text = Instantiate(ChatPrefab, chatContentTransform).GetComponent<TextMeshProUGUI>();
            Text.text = message;

            // Start coroutine to delete the message after 60 seconds
            Destroy(Text.gameObject, 60);
        }
    }

    void SendChatMessageLocal(string message)
    {
        foreach (Player p in GameObject.FindObjectsOfType<Player>())
        {
            Transform chatContentTransform = p.ChatContent;
            // Limit to 5 children
            if (chatContentTransform.childCount >= 5)
            {
                Destroy(chatContentTransform.GetChild(0).gameObject);
            }

            // Instantiate the new chat message
            TextMeshProUGUI Text = Instantiate(ChatPrefab, chatContentTransform).GetComponent<TextMeshProUGUI>();
            Text.text = message;

            // Start coroutine to delete the message after 60 seconds
            Destroy(Text.gameObject, 60);
        }
    }

    public IEnumerator WaitForChunk(float time)
    {
        AllowMovementUpdate = false;
        yield return new WaitForSeconds(time);
        AllowMovementUpdate = true;
    }

    [ServerRpc]
    public void SetTimeServerRPC(float Time)
    {
        Debug.Log(Time);
        GetHost().GameTime.Value = Time;
    }

    Coroutine WaitForChunkCoroutine;
    void Update()
    {
        if(localPlayer == null)
        {
            localPlayer = GetLocal();
        }
        gameObject.name = Username.Value.ToString();
        UsernameRoot.GetChild(0).GetComponent<TextMeshPro>().text = Username.Value.ToString();
        if(Camera.main != null)
        {
            UsernameRoot.LookAt(Camera.main.transform.position);
        }


        if (IsOwner == false) return;
        if(chunkManager == null) return;
        Cursor.visible = Cursor.lockState != CursorLockMode.Locked;



        PlayerPrefs.SetInt("RenderDistance", (int)RenderDistanceSlider.value);


        PlayerPrefs.SetFloat("Sensitivity", SensitivitySlider.value);
    
        RenderDistanceText.text = "Render Distance: " + RenderDistanceSlider.value;
        SensitivityText.text = "Sensitivity: " + SensitivitySlider.value / 100;

        mouseSensitivity = SensitivitySlider.value * 10;
        chunkManager.renderDistance = (int)RenderDistanceSlider.value;

        Chunk currentChunk = chunkManager.GetChunk(transform.position);
        PlayerInChunk = currentChunk != null;
        AllowMovement = PlayerInChunk && Spawned && AllowMovementUpdate;

        controller.enabled = false;
        if (PlayerInChunk == false && !Teleporting && Spawned)
        {
            if(WaitForChunkCoroutine != null)
            {
                StopCoroutine(WaitForChunkCoroutine);
            }
            WaitForChunkCoroutine = StartCoroutine(WaitForChunk(3));
        }

        if (Spawned == false) return;
        if (AllowMovement)
        {
            if (controller.enabled == false)
            {
                controller.enabled = true;
            }
        }



        if (Input.GetKeyDown(KeyCode.T) && !IsPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            ChatCanvas.gameObject.SetActive(true);
            ChatInput.Select();
            Chatting = true;
        }
        if(Chatting && !IsPaused)
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                Cursor.lockState = CursorLockMode.Locked;
                ChatCanvas.gameObject.SetActive(false);
                Chatting = false;

                if (ChatInput.text == "/help")
                {
                    SendChatMessageLocal("<color=#00FFFF>/spawn, /tp {Username} | {x} {y} {z}, /setSpawnPosition, /time set {Value}</color>");
                }
                else if(ChatInput.text == "/spawn")
                {
                    Teleport(chunkManager.SpawnPosition, 0, true);
                    SendChatMessageLocal("<color=#00FFFF>Teleported</color>");
                }
                else if (ChatInput.text.StartsWith("/tp "))
                {
                    string[] parts = ChatInput.text.Split(' ');
                    if (parts.Length >= 2)
                    {
                        string usernameOrX = parts[1];
                        Player p = GetPlayerByName(usernameOrX);

                        // Check if input is a username
                        if (p != null && usernameOrX != this.Username.Value)
                        {
                            Teleport(p.transform.position, 0, false);
                            SendChatMessageLocal("<color=#00FFFF>Teleported to player " + usernameOrX + "</color>");
                        }
                        else if (parts.Length == 4)
                        {
                            Vector3 currentPosition = this.transform.position; // Assuming this is the player's current position
                            float x = 0;
                            float y = 0;
                            float z = 0;
                            bool validX = parts[1] == "~" || float.TryParse(parts[1], out x);
                            bool validY = parts[2] == "~" || float.TryParse(parts[2], out y);
                            bool validZ = parts[3] == "~" || float.TryParse(parts[3], out z);

                            if (validX && validY && validZ)
                            {
                                x = parts[1] == "~" ? currentPosition.x : x;
                                y = parts[2] == "~" ? currentPosition.y : y;
                                z = parts[3] == "~" ? currentPosition.z : z;

                                Vector3 targetPosition = new Vector3(x, y, z);
                                Teleport(targetPosition, 0, false);
                                SendChatMessageLocal("<color=#00FFFF>Teleported to coordinates (" + x + ", " + y + ", " + z + ")</color>");
                            }
                            else
                            {
                                SendChatMessageLocal("<color=red>Invalid coordinates</color>");
                            }
                        }
                        else
                        {
                            SendChatMessageLocal("<color=red>Invalid user or coordinates</color>");
                        }
                    }
                    else
                    {
                        SendChatMessageLocal("<color=red>Invalid Syntax</color>");
                    }
                }
                else if (ChatInput.text == "/setSpawnPosition")
                {
                    if (IsHost)
                    {
                        chunkManager.SpawnPosition = transform.position;
                        chunkManager.SaveGame(Application.dataPath + "/../" + "Saves/" + PlayerPrefs.GetString("WorldName") + ".dat");
                        SendChatMessageLocal("<color=#00FFFF>Set Spawn position to (" + transform.position.x + ", " + transform.position.y + ", " + transform.position.z + ")</color>");
                    }
                    else
                    {
                        SendChatMessageLocal("<color=red>You need to be the host</color>");
                    }
                }
                else if (ChatInput.text.StartsWith("/time set "))
                {
                    string timeString = ChatInput.text.Substring(10);

                    if (float.TryParse(timeString, out float timeValue))
                    {
                        SetTimeServerRPC(timeValue);
                        SendChatMessageLocal("<color=#00FFFF>Time set to " + timeValue + "</color>");
                    }
                    else
                    {
                        SendChatMessageLocal("<color=red>Invalid time value entered</color>");
                    }
                }



                else if (ChatInput.text.StartsWith("/"))
                {
                    SendChatMessageLocal("<color=red>Invalid Syntax</color>");
                }


                else if (ChatInput.text != "")
                {
                    if (IsHost)
                    {
                        SendChatMessageServerRPC("<color=yellow>" + Username.Value.ToString() + "</color>: " + ChatInput.text);
                    }
                    else
                    {
                        SendChatMessageServerRPC(Username.Value.ToString() +": "+ ChatInput.text);
                    }
                }
                ChatInput.text = "";
            }
        }

        if(transform.position.y < -100)
        {
            Teleport(chunkManager.SpawnPosition, 0, true);
        }


        if (Input.GetKeyDown(KeyCode.Escape) && !Chatting)
        {
            if (IsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        if (Input.GetKeyDown(KeyCode.F5) && !IsPaused && !Chatting)
        {
            if (camVal != 2)
            {
                camVal++;
            }
            else
            {
                camVal = 0;
            }
            if (camVal == 0)
            {
                playerCamera.enabled = true;
                playerCameraBack.enabled = false;
                playerCameraFront.enabled = false;
                HandUI.SetActive(true);
            }
            if (camVal == 1)
            {
                playerCamera.enabled = false;
                playerCameraBack.enabled = true;
                playerCameraFront.enabled = false;
                HandUI.SetActive(false);
            }
            if (camVal == 2)
            {
                playerCamera.enabled = false;
                playerCameraBack.enabled = false;
                playerCameraFront.enabled = true;
                HandUI.SetActive(false);
            }
        }

        if (AllowMovement == false)
        {
            playerVelocity = Vector3.zero;
        }

        maxDeltaTime += Time.deltaTime;
        deltaTimeCount++;
        averageDeltaTime = maxDeltaTime / deltaTimeCount;

        Move();
        MouseLook();
    }

    private void LateUpdate()
    {
        Anim.SetBool("Moving", Moving.Value);
        if (IsOwner == false) return;
        float playerVelocityAmount = new Vector3(playerVelocity.x, 0, playerVelocity.z).magnitude;
        float MoveAmount = Mathf.Clamp01(Mathf.Abs(Mathf.RoundToInt(playerVelocityAmount)));

        if (MoveAmount == 1)
        {
            Moving.Value = true; 
            MovementDotY = (Vector3.Dot((transform.right), (playerVelocity))) * 5;
        }
        else
        {
            Moving.Value = false;
        }

        Hand.transform.localPosition = new Vector3(Mathf.Sin(Time.time * playerSpeed * HandAmountX * MoveAmount) * HandAmount, Mathf.Sin(Time.time * playerSpeed * HandAmountY * MoveAmount) * HandAmount, 0);
        Anim.transform.localRotation = Quaternion.Slerp(Anim.transform.localRotation, Quaternion.Euler(0, MovementDotY + 90, 0), 15 * Time.deltaTime);
        Head.transform.rotation = Quaternion.Euler(playerCamera.transform.eulerAngles.z, playerCamera.transform.eulerAngles.y + 90, playerCamera.transform.eulerAngles.x);
    }

    void MouseLook()
    {
        float mouseX = !(!IsPaused && !Chatting) ? 0 : Input.GetAxisRaw("Mouse X") * mouseSensitivity * averageDeltaTime;
        float mouseY = !(!IsPaused && !Chatting) ? 0 : Input.GetAxisRaw("Mouse Y") * mouseSensitivity * averageDeltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void Move()
    {
        if (controller.enabled == false) return;
        RaycastHit hit;
        float radius = .1f;
        groundedPlayer = Physics.SphereCast(transform.position, radius, -Vector3.up, out hit, (1.1f - radius), ~ignore);
        if (groundedPlayer && playerVelocity.y < 0)
        {
            playerVelocity.y = 0f;
        }

        Vector3 move = !(!IsPaused && !Chatting) ? Vector3.zero : transform.right * Input.GetAxisRaw("Horizontal") + transform.forward * Input.GetAxisRaw("Vertical");
        playerVelocity.x = move.x * playerSpeed;
        playerVelocity.z = move.z * playerSpeed;

        if (Input.GetButtonDown("Jump") && groundedPlayer && !IsPaused && !Chatting)
        {
            playerVelocity.y += Mathf.Sqrt(jumpHeight * -3.0f * gravityValue);
        }

        playerVelocity.y += gravityValue * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        IsPaused = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Pause()
    {
        pauseMenuUI.SetActive(true);
        IsPaused = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void QuitGame()
    {
        if (IsHost)
        {
            chunkManager.SaveGame(Application.dataPath + "/../" + "Saves/" + PlayerPrefs.GetString("WorldName") + ".dat");
        }
        NetworkManager.Singleton.Shutdown();
    }

    void SetGraphicsSettings(bool highGraphics)
    {
        PlayerPrefs.SetInt("Graphics", highGraphics ? 1 : 0);
        Light mainLight = chunkManager.DirectionalLight;
        if (mainLight != null)
        {
            if (highGraphics)
            {
                mainLight.shadows = LightShadows.Soft;
            }
            else
            {
                mainLight.shadows = LightShadows.None;
            }
        }
    }
}
