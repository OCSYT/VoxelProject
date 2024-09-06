using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

public class SkinManager : NetworkBehaviour
{
    public Player player;
    public TMP_InputField URLField;
    public SkinnedMeshRenderer[] materials;


    private IEnumerator Start()
    {
        if (IsOwner)
        {
            ApplyTexture(PlayerPrefs.GetString("SkinURL", ""));
            if (PlayerPrefs.GetString("SkinURL", "") != "")
            {
                ApplyTextureServerRPC(PlayerPrefs.GetString("SkinURL", ""));
            }
        }
        else
        {
            yield return new WaitForSeconds(1);
            while (player.SkinURL.Value.ToString() == "")
            {
                yield return new WaitForSeconds(1);
            }
            ApplyTexture(player.SkinURL.Value.ToString());
        }
    }


    public void ApplySettings()
    {
        PlayerPrefs.SetString("SkinURL", URLField.text);
        player.SkinURL.Value = PlayerPrefs.GetString("SkinURL", "");
        ApplyTexture(URLField.text);
        ApplyTextureServerRPC(URLField.text);
    }

    [ServerRpc]
    public void ApplyTextureServerRPC(string URL)
    {
        ApplyTextureClientRPC(URL);
    }
    [ClientRpc]
    public void ApplyTextureClientRPC(string URL)
    {
        ApplyTexture(URL);
    }


    public void ApplyTexture(string URL)
    {
        StartCoroutine(DownloadTexture(URL));
    }

    private IEnumerator DownloadTexture(string URL)
    {
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(URL))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Invalid URL");
            }
            else
            {
                // Get the downloaded texture
                Texture downloadedTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                downloadedTexture.filterMode = FilterMode.Point; 
                downloadedTexture.mipMapBias = 0; 

                foreach (var renderer in materials)
                {
                    renderer.material.mainTexture = downloadedTexture;
                }
            }
        }
    }
}
