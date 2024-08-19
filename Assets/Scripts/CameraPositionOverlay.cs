using UnityEngine;
using UnityEngine.UI;

public class CameraPositionOverlay : MonoBehaviour
{
    public Camera mainCamera; // Reference to the main camera
    public RectTransform mapImage; // The RectTransform of the map image
    public RectTransform cameraMarker; // The RectTransform of the camera marker
    public Vector2 mapWorldSize; // The world size that the map image represents

    void Update()
    {
        // Get the camera's position in the world
        Vector3 cameraWorldPos = mainCamera.transform.position;

        // Normalize the camera's position relative to the map's world size
        float normalizedX = (cameraWorldPos.x) / mapWorldSize.x;
        float normalizedY = (cameraWorldPos.z) / mapWorldSize.y;

        // Convert the normalized position to UI coordinates
        float uiPosX = normalizedX * mapImage.rect.width;
        float uiPosY = normalizedY * mapImage.rect.height;

        // Set the position of the camera marker on the UI map
        cameraMarker.localPosition = new Vector2(uiPosX + mapImage.rect.center.x, uiPosY + mapImage.rect.center.y);
        cameraMarker.transform.localRotation = Quaternion.Euler(0, 0, -mainCamera.transform.eulerAngles.y + 180);
    }
}
