using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // Import the TextMeshPro namespace

public class TextPropertyReader : MonoBehaviour
{
    public Component ComponentToRead;
    public TextMeshProUGUI TextField;
    public string Format;
    public string FieldName;

    void Update()
    {

        if (ComponentToRead != null && TextField != null)
        {
            string fieldValue = "";

            // Check if the ComponentToRead is a Transform
            if (ComponentToRead is Transform)
            {
                Transform transform = (Transform)ComponentToRead;

                // Get the value based on the FieldName
                switch (FieldName)
                {
                    case "position":
                        fieldValue = transform.position.ToString();
                        break;
                    case "rotation":
                        fieldValue = transform.rotation.eulerAngles.ToString();
                        break;
                    case "scale":
                        fieldValue = transform.localScale.ToString();
                        break;
                    // Add cases for other properties if needed
                    default:
                        // Handle other cases or show an error message
                        Debug.LogError("Unsupported field name for Transform: " + FieldName);
                        break;
                }
            }
            else
            {
                // If it's not a Transform, proceed as before
                fieldValue = ComponentToRead.GetType().GetField(FieldName)?.GetValue(ComponentToRead)?.ToString() ?? "";
            }

            TextField.text = string.Format(Format, fieldValue);
        }

    }
}
