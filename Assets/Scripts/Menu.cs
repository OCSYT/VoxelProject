using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Text;
using System;
using UnityEngine.SceneManagement;
using System.IO;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    public TMP_InputField Seed;
    public TMP_InputField WorldName;
    public TMP_InputField CodeField;
    public GameObject SavePrefab;
    public Transform Content;
    public TMP_Dropdown WorldTypeDropdown;

    void Start()
    {

        DeleteFolderContents(Application.dataPath + "/../" + "SaveCache/");

        WorldName.text = "New World";
        Seed.text = GenerateSeed();
        PlayerPrefs.SetString("WorldName", "");
        PlayerPrefs.SetInt("Seed", 0);
        PlayerPrefs.SetInt("LoadingMode", 0);

        DisplaySaves();
    }

    public void DeleteFolderContents(string folderPath)
    {
        if (Directory.Exists(folderPath))
        {
            // Delete all files
            string[] files = Directory.GetFiles(folderPath);
            foreach (string file in files)
            {
                File.Delete(file);
            }

            // Delete all subdirectories and their contents
            string[] subdirectories = Directory.GetDirectories(folderPath);
            foreach (string subdirectory in subdirectories)
            {
                Directory.Delete(subdirectory, true); // true to delete subdirectories and their contents
            }

            Debug.Log("Folder contents deleted successfully.");
        }
        else
        {
            Debug.Log("The specified folder does not exist: " + folderPath);
        }
    }


    void DisplaySaves()
    {
        foreach(Transform child in Content)
        {
            Destroy(child.gameObject);
        }

        string savesDirectory = Application.dataPath + "/../Saves/";
        if (!Directory.Exists(savesDirectory))
        {
            Debug.LogWarning("Saves directory does not exist: " + savesDirectory);
            return;
        }

        string[] saveFiles = Directory.GetFiles(savesDirectory);
        StringBuilder savesListBuilder = new StringBuilder("Saves:\n");

        foreach (string saveFile in saveFiles)
        {
            GameObject button = Instantiate(SavePrefab, Content);
            string _WorldName = Path.GetFileNameWithoutExtension(saveFile);
            button.transform.GetChild(0).GetComponent<Button>().onClick.AddListener(delegate { LoadGame(_WorldName); });
            button.transform.GetChild(0).GetChild(0).GetComponent<TextMeshProUGUI>().text = _WorldName;
            button.transform.GetChild(1).GetComponent<Button>().onClick.AddListener(delegate { DeleteSave(_WorldName); });
        }
    }

    private string GenerateSeed()
    {
        string guidString = System.Guid.NewGuid().ToString();
        int seed = guidString.GetHashCode();
        return seed.ToString();
    }

    public static string StringToHex(string input)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char c in input)
        {
            sb.Append(((int)c).ToString("X2"));
        }
        return sb.ToString();
    }

    public static int HexStringToInt(string hexString)
    {
        // Convert the hexadecimal string to a 32-bit integer using GetHashCode
        return hexString.GetHashCode();
    }

    public void StartGame()
    {
        string WorldTypeSetting = "";
        if(WorldTypeDropdown.value == 0)
        {
            WorldTypeSetting = "normal";
        }
        else if (WorldTypeDropdown.value == 1)
        {
            WorldTypeSetting = "superflat";
        }
        else if(WorldTypeDropdown.value == 2)
        {
            WorldTypeSetting = "earth";
        }
        Debug.Log(WorldTypeSetting);
        PlayerPrefs.SetString("WorldType", WorldTypeSetting);
        PlayerPrefs.SetInt("LoadingMode", 0);
        string sanitizedWorldName = SanitizeFileName(WorldName.text);
        PlayerPrefs.SetString("WorldName", sanitizedWorldName);
        PlayerPrefs.SetInt("Seed", Seed.text.GetHashCode());
        NetworkMenu.instance.Host();
    }
    private string SanitizeFileName(string filePath)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            filePath = filePath.Replace(invalidChar, '_');
        }
        return filePath;
    }


    public void DeleteSave(string WorldName)
    {

        string savesDirectory = Application.dataPath + "/../Saves/";
        if (!Directory.Exists(savesDirectory))
        {
            Debug.LogWarning("Saves directory does not exist: " + savesDirectory);
            return;
        }
        if(File.Exists(savesDirectory + WorldName + ".dat")) {
            File.Delete(savesDirectory + WorldName + ".dat");
        }
        DisplaySaves();
    }

    public void LoadGame(string WorldName)
    {
        PlayerPrefs.SetInt("Superflat", 0);
        PlayerPrefs.SetInt("LoadingMode", 1);
        PlayerPrefs.SetString("WorldName", WorldName);
        PlayerPrefs.SetInt("Seed", 0);
        NetworkMenu.instance.Host();
    }

    public void JoinCode()
    {
        ulong result;

        if (ulong.TryParse(CodeField.text, out result))
        {
            NetworkMenu.instance.ClientJoinCode(result);
        }

    }

    // Update is called once per frame
    void Update()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
