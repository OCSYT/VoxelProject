using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class MaterialSetter : MonoBehaviour
{
    public Texture2D FallBack;
    public List<MaterialTexturePair> textureMappings = new List<MaterialTexturePair>();

    [System.Serializable]
    public class MaterialTexturePair
    {
        public Material material;
        public string textureNameMat = "_MainTex";
        public string textureName;
    }

    void Start()
    {
        foreach (var pair in textureMappings)
        {
            Texture2D loadedTexture = LoadTextureFromPath("/Textures/" + pair.textureName);
            if (loadedTexture != null)
            {
                pair.material.SetTexture(pair.textureNameMat, loadedTexture);
            }
        }
    }

    public Texture2D LoadTextureFromPath(string path)
    {
        try
        {
            path = Application.dataPath + "/../" + path;
            // Load the image file into a byte array
            byte[] fileData = System.IO.File.ReadAllBytes(path);

            // Create a new Texture2D
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            // Load the image data into the Texture2D
            texture.LoadImage(fileData);

            // Set texture filtering mode to Point
            texture.filterMode = FilterMode.Point;

            texture.Apply(false); 

            return texture;
        }
        catch (System.Exception ex)
        {
            return FallBack;
        }
    }


    // Update is called once per frame
    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            foreach (var pair in textureMappings)
            {
                Texture2D loadedTexture = LoadTextureFromPath("/Textures/" + pair.textureName);
                if (loadedTexture != null)
                {
                    if (pair.material.HasProperty(pair.textureNameMat))
                    {
                        pair.material.SetTexture(pair.textureNameMat, loadedTexture);
                    }
                }
            }
        }
#endif
        }
    }
