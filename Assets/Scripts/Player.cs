using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; 
using TMPro;
using UnityEngine.SceneManagement;
using System;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    public Animator Anim;
    public Transform Head;
    private CharacterController controller;
    private Vector3 playerVelocity;
    private bool groundedPlayer;
    public float playerSpeed = 2.0f;
    public float jumpHeight = 1.0f;
    public float gravityValue = -9.81f;

    public Camera playerCamera;
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
    private ChunkManager chunkManager;
    private bool PlayerInChunk;
    public bool AllowMovement;
    private bool AllowMovementInit;

    public GameObject pauseMenuUI;
    public GameObject LoadingScreen;
    public Slider RenderDistanceSlider;
    public Slider SensitivitySlider;
    public TextMeshProUGUI RenderDistanceText;
    public TextMeshProUGUI SensitivityText;

    public bool isPaused = false;


    private void Start()
    {
        controller = gameObject.GetComponent<CharacterController>();
        controller.enabled = false;
        Cursor.lockState = CursorLockMode.Locked;
        chunkManager = ChunkManager.Instance;
        StartCoroutine(WaitForInit());
        RenderDistanceSlider.value = PlayerPrefs.GetInt("RenderDistance", 8);
        SensitivitySlider.value = PlayerPrefs.GetFloat("Sensitivity", 100);
    }

    IEnumerator WaitForInit()
    {
        LoadingScreen.SetActive(true);
        AllowMovementInit = false;
        yield return new WaitUntil(() => chunkManager.SpawnPosition != Vector3.zero);
        yield return new WaitForSeconds(3);
        AllowMovementInit = true;
        LoadingScreen.SetActive(false);
    }

    void Update()
    {
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



        if (AllowMovement == false)
        {
            playerVelocity = Vector3.zero;
        }

        maxDeltaTime += Time.deltaTime;
        deltaTimeCount++;
        averageDeltaTime = maxDeltaTime / deltaTimeCount;

        Move();
        MouseLook();

        float playerVelocityAmount = new Vector3(playerVelocity.x, 0, playerVelocity.z).magnitude;
        float MoveAmount = Mathf.Clamp01(Mathf.Abs(Mathf.RoundToInt(playerVelocityAmount)));

        if (MoveAmount == 1)
        {
            Anim.SetBool("Moving", true);
            MovementDotY = (Vector3.Dot(transform.right, playerVelocity)) * 5;
        }
        else
        {
            Anim.SetBool("Moving", false);
        }

        Head.transform.rotation = Quaternion.Euler(playerCamera.transform.eulerAngles.z, playerCamera.transform.eulerAngles.y + 90, playerCamera.transform.eulerAngles.x);
        Hand.transform.localPosition = new Vector3(Mathf.Sin(Time.time * playerSpeed * HandAmountX * MoveAmount) * HandAmount, Mathf.Sin(Time.time * playerSpeed * HandAmountY * MoveAmount) * HandAmount, 0);
        Anim.transform.localRotation = Quaternion.Euler(0, MovementDotY + 90, 0);
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
        groundedPlayer = Physics.SphereCast(transform.position, 0.4f, -Vector3.up, out hit, (1.1f - 0.4f), ~ignore);
        if (groundedPlayer && playerVelocity.y < 0)
        {
            playerVelocity.y = 0f;
        }

        Vector3 move = isPaused ? Vector3.zero : transform.right * Input.GetAxis("Horizontal") + transform.forward * Input.GetAxis("Vertical");
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
        chunkManager.SaveGame(Application.dataPath + "/../" + "Saves/" + PlayerPrefs.GetString("WorldName") + ".dat");
        SceneManager.LoadScene(0);
    }
}
