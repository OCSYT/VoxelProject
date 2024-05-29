#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class RenderCubemapWizard : ScriptableWizard
{
    public Transform renderFromPosition;
    public int cubemapSize = 1024;

    void OnWizardUpdate()
    {
        helpString = "Select transform to render from and specify cubemap size";
        isValid = (renderFromPosition != null);
    }

    void OnWizardCreate()
    {
        // Create temporary camera for rendering
        GameObject go = new GameObject("CubemapCamera");
        Camera camera = go.AddComponent<Camera>();

        // Place it on the object
        go.transform.position = renderFromPosition.position;
        go.transform.rotation = Quaternion.identity;

        // Rotate the camera 180 degrees around the up axis
        go.transform.Rotate(Vector3.up, 180f);

        // Create a temporary RenderTexture for the cubemap
        RenderTexture renderTexture = new RenderTexture(cubemapSize, cubemapSize, 24);
        renderTexture.dimension = UnityEngine.Rendering.TextureDimension.Cube;
        renderTexture.useMipMap = false;
        renderTexture.autoGenerateMips = false;

        // Render to the cubemap
        camera.RenderToCubemap(renderTexture);
        Debug.Log("Rendered to cubemap.");

        // Save the cubemap faces as PNG images
        string path = "Assets/CubemapRenderTexture";
        SaveRenderTextureAsCubemap(renderTexture, path);

        // Clean up
        DestroyImmediate(renderTexture);
        DestroyImmediate(go);
        Debug.Log("Destroyed temporary camera and render texture.");
    }

    void SaveRenderTextureAsCubemap(RenderTexture rt, string path)
    {
        // Ensure the directory exists
        Directory.CreateDirectory(path);

        // Create a new Texture2D array to hold the cubemap faces
        Texture2D[] faces = new Texture2D[6];
        for (int i = 0; i < 6; i++)
        {
            faces[i] = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        }

        // Copy each face from the RenderTexture
        for (int i = 0; i < 6; i++)
        {
            RenderTexture.active = rt;
            Graphics.SetRenderTarget(rt, 0, (CubemapFace)i);
            faces[i].ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            faces[i].Apply();
        }

        RenderTexture.active = null;

        // Save each face as a PNG
        for (int i = 0; i < 6; i++)
        {
            // Flip the texture vertically before saving to correct orientation
            Texture2D flipped = FlipTextureVertically(faces[i]);
            byte[] bytes = flipped.EncodeToPNG();
            string facePath = Path.Combine(path, "face" + i + ".png");
            File.WriteAllBytes(facePath, bytes);
        }

        Debug.Log("Cubemap faces saved as PNG files.");
    }

    Texture2D FlipTextureVertically(Texture2D original)
    {
        Texture2D flipped = new Texture2D(original.width, original.height);
        for (int y = 0; y < original.height; y++)
        {
            for (int x = 0; x < original.width; x++)
            {
                flipped.SetPixel(x, (original.height - y - 1), original.GetPixel(x, y));
            }
        }
        flipped.Apply();
        return flipped;
    }

    [MenuItem("GameObject/Render into Cubemap RenderTexture")]
    static void RenderCubemap()
    {
        ScriptableWizard.DisplayWizard<RenderCubemapWizard>(
            "Render cubemap RenderTexture", "Render!");
    }
}
#endif
