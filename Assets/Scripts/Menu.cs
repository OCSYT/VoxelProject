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
    public GameObject SavePrefab;
    public Transform Content;
    void Start()
    {
        WorldName.text = "New World";
        Seed.text = GenerateSeed();
        PlayerPrefs.SetString("WorldName", "");
        PlayerPrefs.SetInt("Seed", 0);
        PlayerPrefs.SetInt("LoadingMode", 0);

        DisplaySaves();
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
        PlayerPrefs.SetInt("LoadingMode", 0);
        PlayerPrefs.SetString("WorldName", WorldName.text);
        PlayerPrefs.SetInt("Seed", Seed.text.GetHashCode());
        NetworkMenu.instance.Host();
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
        PlayerPrefs.SetInt("LoadingMode", 1);
        PlayerPrefs.SetString("WorldName", WorldName);
        PlayerPrefs.SetInt("Seed", 0);
        NetworkMenu.instance.Host();
    }

    // Update is called once per frame
    void Update()
    {

    }
}
