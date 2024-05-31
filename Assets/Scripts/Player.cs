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
[RequireComponent(typeof(CharacterController))]
public class Player : NetworkBehaviour
{
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
    private bool AllowMovementInit;

    public GameObject pauseMenuUI;
    public GameObject LoadingScreen;
    public GameObject HandUI;
    public Slider RenderDistanceSlider;
    public Slider SensitivitySlider;
    public TextMeshProUGUI RenderDistanceText;
    public TextMeshProUGUI SensitivityText;
    public Toggle graphicsToggle;

    public bool isPaused = false;
    private Player localPlayer;

    public NetworkVariable<float> GameTime = 
        new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkVariable<FixedString32Bytes> Username =
        new NetworkVariable<FixedString32Bytes>(new FixedString32Bytes(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);


    public NetworkVariable<bool> Synced = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> Hosting = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkList<byte> NetworkSaveDataBytes = new NetworkList<byte>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);


    private void Start()
    {
        controller = gameObject.GetComponent<CharacterController>();
        controller.enabled = false;

        if (IsOwner)
        {
            Hosting.Value = IsHost;
            Username.Value = (FixedString32Bytes)SteamClient.Name;
            LoadingScreen.SetActive(true);
            if (IsHost)
            {
                StartCoroutine(InitSync(new List<(Vector3, byte)>()));
            }
        }
        else
        {
            chunkManager.gameObject.SetActive(false);
            playerCamera.gameObject.SetActive(false);
            PlayerModel.layer = PlayerModelLayer;
        }
        gameObject.name = Username.Value.ToString();


        if (NetworkManager.IsHost)
        {
            GetHost().SyncRequestServerRPC();
        }
    }
    private void OnDestroy()
    {
        if (NetworkManager.IsHost)
        {
            GetHost().BlockPlace.BufferedBlockEvents.Clear();
            GetHost().chunkManager.SaveGame(Application.dataPath + "/../" + "Saves/" + PlayerPrefs.GetString("WorldName") + ".dat");
        }
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

        Debug.Log("Server: " + BlockEvents.Count);

        SyncRequestClientRPC(VecList, ByteList);
    }
    [ClientRpc]
    public void SyncRequestClientRPC(Vector3[] VecList, byte[] ByteList)
    {
        Debug.Log("recieved");
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
            Debug.Log("Client: " + BlockEvents.Count);
            BlockPlace.PlaceBufferedBlocks();
            Cursor.lockState = CursorLockMode.Locked;
            chunkManager.StartGenerating();
            StartCoroutine(WaitForInit());
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

        }
        if(Synced.Value == false)
        {
            Synced.Value = true;
        }
    }

    IEnumerator WaitForInit()
    {
        AllowMovementInit = false;
        yield return new WaitUntil(() => chunkManager.SpawnPosition != Vector3.zero);
        yield return new WaitForSeconds(3);
        AllowMovementInit = true;
        LoadingScreen.SetActive(false);
    }

    void Update()
    {
        if(localPlayer == null)
        {
            localPlayer = GetLocal();
        }
        gameObject.name = Username.Value.ToString();
        if (IsOwner == false) return;
        if(chunkManager == null) return;
        PlayerPrefs.SetInt("RenderDistance", (int)RenderDistanceSlider.value);
        PlayerPrefs.SetFloat("Sensitivity", SensitivitySlider.value);
        RenderDistanceText.text = "Render Distance: " + RenderDistanceSlider.value;
        SensitivityText.text = "Sensitivity: " + SensitivitySlider.value / 100;

        mouseSensitivity = SensitivitySlider.value * 10;
        chunkManager.renderDistance = (int)RenderDistanceSlider.value;

        Chunk currentChunk = chunkManager.GetChunk(transform.position);
        PlayerInChunk = currentChunk != null;
        AllowMovement = PlayerInChunk && AllowMovementInit;

        controller.enabled = false;
        if (AllowMovement == false || AllowMovementInit == false) return;
        controller.enabled = true;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        if (Input.GetKeyDown(KeyCode.F5) && !isPaused)
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
        if (IsOwner == false) return;
        float playerVelocityAmount = new Vector3(playerVelocity.x, 0, playerVelocity.z).magnitude;
        float MoveAmount = Mathf.Clamp01(Mathf.Abs(Mathf.RoundToInt(playerVelocityAmount)));

        if (MoveAmount == 1)
        {
            Anim.SetBool("Moving", true);
            MovementDotY = (Vector3.Dot((transform.right), (playerVelocity))) * 5;
        }
        else
        {
            Anim.SetBool("Moving", false);
        }

        Hand.transform.localPosition = new Vector3(Mathf.Sin(Time.time * playerSpeed * HandAmountX * MoveAmount) * HandAmount, Mathf.Sin(Time.time * playerSpeed * HandAmountY * MoveAmount) * HandAmount, 0);
        Anim.transform.localRotation = Quaternion.Slerp(Anim.transform.localRotation, Quaternion.Euler(0, MovementDotY + 90, 0), 15 * Time.deltaTime);
        Head.transform.rotation = Quaternion.Euler(playerCamera.transform.eulerAngles.z, playerCamera.transform.eulerAngles.y + 90, playerCamera.transform.eulerAngles.x);
    }

    void MouseLook()
    {
        float mouseX = isPaused ? 0 : Input.GetAxisRaw("Mouse X") * mouseSensitivity * averageDeltaTime;
        float mouseY = isPaused ? 0 : Input.GetAxisRaw("Mouse Y") * mouseSensitivity * averageDeltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void Move()
    {
        RaycastHit hit;
        float radius = .1f;
        groundedPlayer = Physics.SphereCast(transform.position, radius, -Vector3.up, out hit, (1.1f - radius), ~ignore);
        if (groundedPlayer && playerVelocity.y < 0)
        {
            playerVelocity.y = 0f;
        }

        Vector3 move = isPaused ? Vector3.zero : transform.right * Input.GetAxisRaw("Horizontal") + transform.forward * Input.GetAxisRaw("Vertical");
        playerVelocity.x = move.x * playerSpeed;
        playerVelocity.z = move.z * playerSpeed;

        if (Input.GetButtonDown("Jump") && groundedPlayer)
        {
            playerVelocity.y += Mathf.Sqrt(jumpHeight * -3.0f * gravityValue);
        }

        playerVelocity.y += gravityValue * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        isPaused = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Pause()
    {
        pauseMenuUI.SetActive(true);
        isPaused = true;
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
        Light mainLight = GameObject.FindObjectOfType<Light>();
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
