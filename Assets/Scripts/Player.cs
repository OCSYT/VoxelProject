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
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;
using System.Linq;


[RequireComponent(typeof(CharacterController))]
public class Player : NetworkBehaviour
{
    public VoiceChat VC;
    public GameObject MicOn;
    public GameObject MicOff;
    public GameObject ChatPrefab;
    public Transform ChatContent;
    public TMP_InputField ChatInput;
    public GameObject ChatCanvas;
    public Transform UsernameRoot;
    public int PlayerModelLayer;
    public GameObject PlayerModel;
    public GameObject PlayerModelCape;
    public ChunkManager chunkManager;
    public BlockPlace BlockPlace;
    public Animator Anim;
    public Transform Head;
    private CharacterController controller;
    private Vector3 playerVelocity;
    private bool groundedPlayer;
    public float playerSpeed = 2.0f;
    private float OriginalSpeed;
    public float jumpHeight = 1.0f;
    public float gravityValue = -9.81f;
    private float CurrentGravity = 0;
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
    public GameObject BlockPickingMenu;
    public TextMeshProUGUI CodeText;
    public Slider RenderDistanceSlider;
    public Slider SensitivitySlider;
    public Slider AudioSlider;
    public TextMeshProUGUI RenderDistanceText;
    public TextMeshProUGUI SensitivityText;
    public TextMeshProUGUI AudioText;
    public Toggle graphicsToggle;
    public GameObject PostFX;

    public bool PickingBlock = false;
    public bool IsPaused = false;
    public bool Chatting = false;
    private Player localPlayer;
    private bool Sprinting;
    private bool Crouching;
    private bool Flying;
    private float DoublePressTime = 0.25f;
    private float LastSpacePressTime = 0f;
    private bool WaitingForDoublePress = false;
    private bool InWater = false;
    private bool CamInWater = false;
    public GameObject CamWaterFX;
    private bool chunkborders;
    private Vector3 CamStart;
    public Camera[] DebugCameras;
    [HideInInspector]
    public NetworkVariable<float> GameTime = 
        new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [HideInInspector]
    public NetworkVariable<FixedString64Bytes> Username =
        new NetworkVariable<FixedString64Bytes>(new FixedString64Bytes(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [HideInInspector]
    public NetworkVariable<FixedString64Bytes> SkinURL =
        new NetworkVariable<FixedString64Bytes>(new FixedString64Bytes(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [HideInInspector]
    public NetworkVariable<bool> Synced = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [HideInInspector]
    public NetworkVariable<bool> Hosting = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [HideInInspector]
    public NetworkVariable<bool> Moving = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [HideInInspector]
    public NetworkVariable<bool> Crouch = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private string TargetWorldName;
    private bool Teleporting = false;

    public string[] Devs;


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
        StartCoroutine(WaitForChunkTeleport(Position, false));
    }

    IEnumerator WaitForChunkTeleport(Vector3 Position, bool MakeNotInGround)
    {
        Chunk currentChunk = chunkManager.GetChunk(transform.position);
        bool inChunk = currentChunk != null;
        AllowMovementUpdate = false;
        if (!inChunk)
        {
            yield return new WaitForSeconds(3);
        }
        if (MakeNotInGround)
        {
            float yPos = 0;
            RaycastHit hit;
            if (Physics.Raycast(Position + Vector3.up * 100, Vector3.down, out hit, Mathf.Infinity, ~ignore))
            {
                yPos = hit.point.y + 2;
                transform.position = new Vector3(Position.x, yPos, Position.z);
            }
        }
        else
        {
            transform.position = Position;
        }
        AllowMovementUpdate = true;

        Teleporting = false;
    }


    private void Awake()
    {
        CamStart = playerCamera.transform.localPosition;
        CurrentGravity = gravityValue;
        OriginalSpeed = playerSpeed;
        controller = gameObject.GetComponent<CharacterController>();
        controller.enabled = false;
    }

    private void FixedUpdate()
    {
        if (IsOwner)
        {
            UpdatePlayerPositions();

            Chunk currentChunk = chunkManager.GetChunk(transform.position);
            if (currentChunk != null)
            {

                Vector3 playerPos = transform.position;
                Vector3Int playerAlignedGridPos = Vector3Int.FloorToInt(playerPos);
                Vector3Int LocalPlayerPosition = Vector3Int.FloorToInt(currentChunk.transform.InverseTransformPoint(playerAlignedGridPos));
                bool InRangeX = LocalPlayerPosition.x >= 0 && LocalPlayerPosition.x < chunkManager.ChunkSize;
                bool InRangeY = LocalPlayerPosition.y >= 0 && LocalPlayerPosition.y < chunkManager.ChunkSize;
                bool InRangeZ = LocalPlayerPosition.z >= 0 && LocalPlayerPosition.z < chunkManager.ChunkSize;
                if (InRangeX && InRangeY && InRangeZ)
                {
                    byte CurrentVoxel = currentChunk.GetData()[LocalPlayerPosition.x, LocalPlayerPosition.y, LocalPlayerPosition.z];
                    InWater = CurrentVoxel == chunkManager.BlockList["Water"];
                }
            }


            if (Camera.main)
            {
                Chunk CamChunk = chunkManager.GetChunk(Camera.main.transform.position);
                if (CamChunk != null)
                {
                    Vector3 CamPos = Camera.main.transform.position;
                    Vector3Int CamAlignedGridPos = Vector3Int.FloorToInt(CamPos);
                    Vector3Int CamPlayerPosition = Vector3Int.FloorToInt(CamChunk.transform.InverseTransformPoint(CamAlignedGridPos));
                    bool CamInRangeX = CamPlayerPosition.x >= 0 && CamPlayerPosition.x < chunkManager.ChunkSize;
                    bool CamInRangeY = CamPlayerPosition.y >= 0 && CamPlayerPosition.y < chunkManager.ChunkSize;
                    bool CamInRangeZ = CamPlayerPosition.z >= 0 && CamPlayerPosition.z < chunkManager.ChunkSize;
                    if (CamInRangeX && CamInRangeY && CamInRangeZ)
                    {
                        byte CurrentVoxel = CamChunk.GetData()[CamPlayerPosition.x, CamPlayerPosition.y, CamPlayerPosition.z];
                        CamInWater = CurrentVoxel == chunkManager.BlockList["Water"];
                    }
                }
            }

            CamWaterFX.SetActive(CamInWater);

            if (InWater)
            {
                CurrentGravity = gravityValue / 10;
            }
            else
            {
                CurrentGravity = gravityValue;
            }
        }
    }

    private void UpdatePlayerPositions()
    {
        Chunk currentChunk = chunkManager.GetChunk(transform.position);
        if (currentChunk == null) return;   

        Vector3Int chunkPosition = Vector3Int.FloorToInt(currentChunk.transform.position);
        ConcurrentDictionary<string, Vector3> playerPositions = new ConcurrentDictionary<string, Vector3>();


        playerPositions[gameObject.name] = transform.position;

        Vector3 playerPos = transform.position;
        Vector3Int playerAlignedGridPos = Vector3Int.FloorToInt(playerPos);
        Vector3Int LocalPlayerPosition = Vector3Int.FloorToInt(currentChunk.transform.InverseTransformPoint(playerAlignedGridPos));

        bool InRangeX = LocalPlayerPosition.x >= 0 && LocalPlayerPosition.x < chunkManager.ChunkSize;
        bool InRangeY = LocalPlayerPosition.y >= 0 && LocalPlayerPosition.y < chunkManager.ChunkSize;
        bool InRangeZ = LocalPlayerPosition.z >= 0 && LocalPlayerPosition.z < chunkManager.ChunkSize;


        if (InRangeX && InRangeY && InRangeZ)
        {
            //VoxelData axis only goes up to size of ChunkSize

            byte CurrentVoxel = currentChunk.GetData()[LocalPlayerPosition.x, LocalPlayerPosition.y, LocalPlayerPosition.z];
            if (CurrentVoxel != 0 && !chunkManager.NoCollisonBlocks.Contains(CurrentVoxel))
            {
                playerPositions[gameObject.name] = playerPos + Vector3.up * 2;
                TeleportPlayerCorrect(playerPositions[gameObject.name]);
                if (AllowMovement)
                {
                    if (WaitForChunkCoroutine != null)
                    {
                        StopCoroutine(WaitForChunkCoroutine);
                    }
                    WaitForChunkCoroutine = StartCoroutine(WaitForChunk(.5f));
                }
                Debug.Log("Moving player");
            }
        }
    }


    private void Start()
    {

        if (IsOwner)
        {
            Hosting.Value = IsHost;
            SkinURL.Value = PlayerPrefs.GetString("SkinURL", "");
            Username.Value = (FixedString64Bytes)SteamClient.Name;
            LoadingScreen.SetActive(true);
            UsernameRoot.gameObject.SetActive(false);

            RenderDistanceSlider.value = PlayerPrefs.GetInt("RenderDistance", 8);
            SensitivitySlider.value = PlayerPrefs.GetFloat("Sensitivity", 100);
            AudioSlider.value = PlayerPrefs.GetFloat("Audio", 100);

            graphicsToggle.isOn = PlayerPrefs.GetInt("Graphics", 1) == 1;
            SetGraphicsSettings(graphicsToggle.isOn);

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
            PlayerModelCape.layer = PlayerModelLayer;
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


    [Serializable]
    public class SerializableVector3
    {
        public float X;
        public float Y;
        public float Z;

        public SerializableVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public SerializableVector3(Vector3 vector)
        {
            X = vector.x;
            Y = vector.y;
            Z = vector.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(X, Y, Z);
        }
    }

    [Serializable]
    public class BlockEvent
    {
        public SerializableVector3 Vec1;
        public SerializableVector3 Vec2;
        public byte ByteVal;

        public BlockEvent(SerializableVector3 vec1, SerializableVector3 vec2, byte byteVal)
        {
            Vec1 = vec1;
            Vec2 = vec2;
            ByteVal = byteVal;
        }
    }


    public static byte[] CompressData(List<(Vector3, Vector3, byte)> blockEvents)
    {
        List<BlockEvent> events = new List<BlockEvent>();
        foreach (var blockEvent in blockEvents)
        {
            events.Add(new BlockEvent(
                new SerializableVector3(blockEvent.Item1),
                new SerializableVector3(blockEvent.Item2),
                blockEvent.Item3
            ));
        }

        BinaryFormatter formatter = new BinaryFormatter();
        using (MemoryStream memoryStream = new MemoryStream())
        {
            formatter.Serialize(memoryStream, events);
            byte[] serializedData = memoryStream.ToArray();

            using (MemoryStream compressedStream = new MemoryStream())
            {
                using (GZipStream gzipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    gzipStream.Write(serializedData, 0, serializedData.Length);
                }
                return compressedStream.ToArray();
            }
        }
    }

    public static List<(Vector3, Vector3, byte)> DecompressData(byte[] compressedData)
    {
        using (MemoryStream compressedStream = new MemoryStream(compressedData))
        {
            using (GZipStream gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            {
                using (MemoryStream decompressedStream = new MemoryStream())
                {
                    gzipStream.CopyTo(decompressedStream);
                    decompressedStream.Position = 0;

                    BinaryFormatter formatter = new BinaryFormatter();
                    List<BlockEvent> events = (List<BlockEvent>)formatter.Deserialize(decompressedStream);

                    List<(Vector3, Vector3, byte)> blockEvents = new List<(Vector3, Vector3, byte)>();
                    foreach (var blockEvent in events)
                    {
                        blockEvents.Add((blockEvent.Vec1.ToVector3(), blockEvent.Vec2.ToVector3(), blockEvent.ByteVal));
                    }
                    return blockEvents;
                }
            }
        }
    }


    [ServerRpc(RequireOwnership =false)]
    public void SyncRequestServerRPC()
    {
        byte[] compressedData = CompressData(BlockPlace.BufferedBlockEvents);
        SyncRequestClientRPC(compressedData);

    }
    [ClientRpc]
    public void SyncRequestClientRPC(byte[] compressedData)
    {
        StartCoroutine(WaitForPlayer(DecompressData(compressedData)));
    }
    IEnumerator WaitForPlayer(List<(Vector3, Vector3, byte)> BlockEvents)
    {
        yield return new WaitUntil(() => localPlayer != null);


        if (localPlayer.Synced.Value == false)
        {
            StartCoroutine(localPlayer.InitSync(BlockEvents));
        }
    }


    IEnumerator InitSync(List<(Vector3, Vector3, byte)> BlockEvents)
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
        GetHost().GameTime.Value = Time;
    }

    Coroutine WaitForChunkCoroutine;

    [ServerRpc]
    void SetSpawnServerRPC(Vector3 pos)
    {
        SetSpawnClientRPC(pos);
    }
    [ClientRpc]
    void SetSpawnClientRPC(Vector3 pos)
    {
        GetLocal().chunkManager.SpawnPosition = pos;
    }


    bool SetCape = false;

    void Update()
    {

        if(gameObject.name != "Player" && !SetCape)
        {
            if(Devs.Contains(gameObject.name))
            {
                PlayerModelCape.SetActive(true);
            }

            SetCape = true;
        }

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

        if (chunkManager == null) return;
        Cursor.visible = Cursor.lockState != CursorLockMode.Locked;



        PlayerPrefs.SetInt("RenderDistance", (int)RenderDistanceSlider.value);


        PlayerPrefs.SetFloat("Sensitivity", SensitivitySlider.value);


        PlayerPrefs.SetFloat("Audio", AudioSlider.value);

        RenderDistanceText.text = "Render Distance: " + RenderDistanceSlider.value;
        SensitivityText.text = "Sensitivity: " + SensitivitySlider.value / 100;
        AudioText.text = "Volume: " + AudioSlider.value / 100;

        AudioListener.volume = AudioSlider.value / 100;
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

        if(Input.GetKeyDown(KeyCode.V) && !IsPaused && !Chatting && !PickingBlock)
        {
            VC.Muted =! VC.Muted;
            MicOn.SetActive(!VC.Muted);
            MicOff.SetActive(VC.Muted);
        }

        if (Input.GetKeyDown(KeyCode.E) && !IsPaused && !Chatting){
            PickingBlock =! PickingBlock;
            BlockPickingMenu.SetActive(PickingBlock);
            if (PickingBlock)
            {
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }


        if (Input.GetKeyDown(KeyCode.T) && !IsPaused && !PickingBlock)
        {
            Cursor.lockState = CursorLockMode.None;
            ChatCanvas.gameObject.SetActive(true);
            ChatInput.Select();
            Chatting = true;
        }
        if(Chatting && !IsPaused && !PickingBlock)
        {
            ChatInput.Select();
            if (Input.GetKeyDown(KeyCode.Return))
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                Cursor.lockState = CursorLockMode.Locked;
                ChatCanvas.gameObject.SetActive(false);
                Chatting = false;

                if (ChatInput.text == "/help")
                {
                    SendChatMessageLocal("<color=#00FFFF>/spawn, /tp {Username} | {x} {y} {z}, " +
                        "/setSpawnPosition, /time set {Value}, /chunkborders </color>");
                }
                else if (ChatInput.text == "/spawn")
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
                        SetSpawnServerRPC(transform.position);
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
                else if (ChatInput.text == "/chunkborders")
                {
                    chunkborders = !chunkborders;
                    SendChatMessageLocal("<color=#00FFFF>Set chunk borders to " + chunkborders + "</color>");

                    foreach (Camera cam in DebugCameras)
                    {
                        if (chunkborders)
                        {
                            cam.cullingMask |= (1 << 11);
                        }
                        else
                        {
                            cam.cullingMask &= ~(1 << 11);
                        }
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
                        SendChatMessageServerRPC(Username.Value.ToString() + ": " + ChatInput.text);
                    }
                }
                ChatInput.text = "";
            }
        }

        if(transform.position.y < -100)
        {
            Teleport(chunkManager.SpawnPosition, 0, true);
        }


        if (Input.GetKeyDown(KeyCode.Escape) && !Chatting && !PickingBlock)
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

        if (Input.GetKeyDown(KeyCode.F5) && !IsPaused && !Chatting && !PickingBlock)
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
        Anim.SetBool("Crouching", Crouch.Value);
        if (IsOwner == false) return;
        float playerVelocityAmount = new Vector3(playerVelocity.x, 0, playerVelocity.z).magnitude;
        float MoveAmount = Mathf.Clamp01(Mathf.Abs(Mathf.RoundToInt(playerVelocityAmount)));

        if (MoveAmount == 1)
        {
            Moving.Value = true; 
            MovementDotY = Mathf.Clamp((Vector3.Dot((transform.right), (playerVelocity))) * 5, -45, 45);
        }
        else
        {
            Moving.Value = false;
        }

        Hand.transform.localPosition = Vector3.Lerp(Hand.transform.localPosition, 
            new Vector3(Mathf.Sin(Time.time * playerSpeed * HandAmountX * MoveAmount) * HandAmount, Mathf.Sin(Time.time * playerSpeed * HandAmountY * MoveAmount) * HandAmount, 0)
            , 15 * Time.deltaTime);
        Anim.transform.localRotation = Quaternion.Slerp(Anim.transform.localRotation, Quaternion.Euler(0, MovementDotY + 90, 0), 15 * Time.deltaTime);
        Head.transform.rotation = Quaternion.Euler(playerCamera.transform.eulerAngles.z, playerCamera.transform.eulerAngles.y + 90, playerCamera.transform.eulerAngles.x);
    }

    void MouseLook()
    {
        float mouseX = !(!IsPaused && !Chatting && !PickingBlock) ? 0 : Input.GetAxisRaw("Mouse X") * mouseSensitivity * averageDeltaTime;
        float mouseY = !(!IsPaused && !Chatting && !PickingBlock) ? 0 : Input.GetAxisRaw("Mouse Y") * mouseSensitivity * averageDeltaTime;

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

        Vector3 move = !(!IsPaused && !Chatting && !PickingBlock) ? Vector3.zero : transform.right * Input.GetAxis("Horizontal") + transform.forward * Input.GetAxis("Vertical");
        playerVelocity.x = move.x * playerSpeed;
        playerVelocity.z = move.z * playerSpeed;

        if (!IsPaused && !Chatting && !PickingBlock)
        {
            Sprinting = Input.GetKey(KeyCode.LeftControl);
            if (Sprinting)
            {
                Crouching = false;
                playerSpeed = OriginalSpeed * 1.5f;
                if (Flying)
                {
                    playerSpeed = playerSpeed * 1.5f;
                }
            }
            else
            {
                Crouching = Input.GetKey(KeyCode.LeftShift);

                playerSpeed = OriginalSpeed;
                if (Flying)
                {
                    playerSpeed = playerSpeed * 1.5f;
                }
                else
                {
                    if (Crouching)
                    {
                        playerSpeed = OriginalSpeed / 2;
                    }
                }
            }
        }
        else
        {
            Sprinting = false;
            Crouching = false;
        }
        if (Crouching)
        {
            playerCamera.transform.localPosition = CamStart + Vector3.down * 0.25f;
        }
        else {
            playerCamera.transform.localPosition = CamStart;
        }

        Crouch.Value = Crouching;

        if (!Flying)
        {
            if (!IsPaused && !Chatting && !PickingBlock)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    if (WaitingForDoublePress && (Time.time - LastSpacePressTime) < DoublePressTime)
                    {
                        Flying = true;
                        WaitingForDoublePress = false;
                    }
                    else
                    {
                        LastSpacePressTime = Time.time;
                        WaitingForDoublePress = true;
                    }

                    if (groundedPlayer)
                    {
                        playerVelocity.y += Mathf.Sqrt(jumpHeight * -3.0f * gravityValue);
                    }
                }
            }

            playerVelocity.y += CurrentGravity * Time.deltaTime;
        }
        else
        {
            if (!IsPaused && !Chatting && !PickingBlock)
            {
                if (Input.GetKey(KeyCode.Space))
                {
                    playerVelocity.y = playerSpeed;
                }
                else if (Input.GetKey(KeyCode.LeftShift))
                {
                    playerVelocity.y = -playerSpeed;
                }
                else
                {
                    playerVelocity.y = 0;
                }

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    if (WaitingForDoublePress && (Time.time - LastSpacePressTime) < DoublePressTime)
                    {
                        Flying = false;
                        WaitingForDoublePress = false;
                    }
                    else
                    {
                        LastSpacePressTime = Time.time;
                        WaitingForDoublePress = true;
                    }
                }
            }
            else
            {
                playerVelocity.y = 0;
            }
        }

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
        pauseMenuUI.transform.GetChild(0).GetChild(1).gameObject.SetActive(true);
        pauseMenuUI.transform.GetChild(0).GetChild(2).gameObject.SetActive(false);
        IsPaused = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void QuitGame()
    {
        if (IsHost)
        {
            chunkManager.SaveGame(Application.dataPath + "/../" + "Saves/" + PlayerPrefs.GetString("WorldName") + ".dat");
        }
        StartCoroutine(WaitForQuit());
    }
    IEnumerator WaitForQuit()
    {
        while (chunkManager.saving)
        {
            yield return null;
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
                PostFX.SetActive(true);
            }
            else
            {
                mainLight.shadows = LightShadows.None;
                PostFX.SetActive(false);
            }
        }
    }
}
