#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using UnityEngine.UIElements;

[CustomEditor(typeof(ChunkManager))]
public class ChunkManagerEditor : Editor
{
    private SerializedProperty blocksProperty;
    private ReorderableList blocksList;
    private bool[] blockFoldouts;
    Texture2D AtlasTexture;
    float BlockSize;
    float TextureSize;

    private void OnEnable()
    {
        blocksProperty = serializedObject.FindProperty("Blocks");
        blocksList = new ReorderableList(serializedObject, blocksProperty, true, true, true, true);
        blocksList.drawElementCallback = DrawBlockElement;
        blocksList.elementHeightCallback = GetBlockElementHeight;
        blocksList.onAddCallback = OnAddBlock;
        blocksList.drawHeaderCallback = DrawBlocksHeader;

        // Initialize block foldouts array
        InitializeFoldoutsArray();

        ChunkManager chunkManager = (ChunkManager)target;
        AtlasTexture = chunkManager.mat.mainTexture as Texture2D;
        BlockSize = chunkManager.BlockSize;
        TextureSize = chunkManager.TextureSize;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw block list
        blocksList.DoLayoutList();

        EditorGUILayout.Space();

        // Iterate over other properties and display them
        var iterator = serializedObject.GetIterator();
        iterator.NextVisible(true); // Skip script field
        while (iterator.NextVisible(false))
        {
            if (iterator.name != "Blocks") // Exclude Blocks property
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBlocksHeader(Rect rect)
    {
        EditorGUI.LabelField(rect, "Blocks");
    }

    private void DrawBlockElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        var element = blocksList.serializedProperty.GetArrayElementAtIndex(index);
        rect.y += 2;

        // Draw foldout toggle for the block
        blockFoldouts[index] = EditorGUI.Foldout(new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight), blockFoldouts[index], element.FindPropertyRelative("Name").stringValue, true);

        if (blockFoldouts[index])
        {
            // Indent properties
            EditorGUI.indentLevel++;

            // Draw the properties of the Block manually
            EditorGUI.PropertyField(new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("Name"));
            EditorGUI.PropertyField(new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight * 2, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("Value"));
            EditorGUI.PropertyField(new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight * 3, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("FrontFace"));
            EditorGUI.PropertyField(new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight * 4, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("BackFace"));
            EditorGUI.PropertyField(new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight * 5, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("LeftFace"));
            EditorGUI.PropertyField(new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight * 6, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("RightFace"));
            EditorGUI.PropertyField(new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight * 7, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("TopFace"));
            EditorGUI.PropertyField(new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight * 8, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("BottomFace"));
            EditorGUI.PropertyField(new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight * 9, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("Light"));

            if (AtlasTexture != null)
            {
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(element.FindPropertyRelative("Name").stringValue + " Previews", GUILayout.Width(1000));
                DrawFacePreview(element, "FrontFace", AtlasTexture, rect);
                DrawFacePreview(element, "BackFace", AtlasTexture, rect);
                DrawFacePreview(element, "LeftFace", AtlasTexture, rect);
                DrawFacePreview(element, "RightFace", AtlasTexture, rect);
                DrawFacePreview(element, "TopFace", AtlasTexture, rect);
                DrawFacePreview(element, "BottomFace", AtlasTexture, rect);
                EditorGUILayout.EndVertical();
            }

            // Draw Move Up and Move Down buttons
            DrawMoveButtons(rect, index);

            // Reduce indent after drawing properties
            EditorGUI.indentLevel--;
        }
    }

    private void DrawMoveButtons(Rect rect, int index)
    {
        float buttonWidth = 20f;
        float buttonHeight = EditorGUIUtility.singleLineHeight;
        float buttonY = rect.y + EditorGUIUtility.singleLineHeight * 10;
        float buttonX = rect.x + rect.width - buttonWidth * 2;

        if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, buttonHeight), "↑") && index > 0)
        {
            blocksProperty.MoveArrayElement(index, index - 1);
            SwapFoldoutStates(index, index - 1);
        }

        if (GUI.Button(new Rect(buttonX + buttonWidth, buttonY, buttonWidth, buttonHeight), "↓") && index < blocksProperty.arraySize - 1)
        {
            blocksProperty.MoveArrayElement(index, index + 1);
            SwapFoldoutStates(index, index + 1);
        }
    }

    private void DrawFacePreview(SerializedProperty element, string faceName, Texture2D atlasTexture, Rect rect)
    {
        EditorGUILayout.LabelField(element.FindPropertyRelative("Name").stringValue + " " + faceName + " Preview", GUILayout.Width(1000));
        SerializedProperty faceProperty = element.FindPropertyRelative(faceName);
        if (faceProperty != null)
        {
            Rect textureRect = EditorGUILayout.GetControlRect(GUILayout.Width(60), GUILayout.Height(60));
            Rect texCoords = new Rect(0, 0, 1, 1); // Default texture coordinates (no tiling or offset)

            int newTexID = (byte)element.FindPropertyRelative(faceName).intValue;
            byte blockId = 0;
            if (blockId == 0)
            {
                blockId = (byte)element.FindPropertyRelative("Value").intValue;
            }
            int textureID = newTexID == 0 ? blockId - 1 : newTexID;

            float atlasSize = TextureSize;
            float _BlockSize = BlockSize;
            float blocksPerRow = atlasSize / _BlockSize;
            float row = Mathf.Floor(textureID / blocksPerRow);
            float col = textureID % blocksPerRow;
            float blockX = col * (_BlockSize / atlasSize);
            float blockY = row * (_BlockSize / atlasSize);
            float uvSize = 1.0f / blocksPerRow;

            texCoords.width *= 1 / _BlockSize;
            texCoords.height *= 1 / _BlockSize;
            texCoords.x += blockX;
            texCoords.y += blockY;

            GUI.DrawTextureWithTexCoords(textureRect, atlasTexture, texCoords);
        }
    }

    private float GetBlockElementHeight(int index)
    {
        float baseHeight = EditorGUIUtility.singleLineHeight;
        if (blockFoldouts[index])
        {
            baseHeight += EditorGUIUtility.singleLineHeight * 11; // Height for 9 properties + 2 buttons in Block class
        }
        return baseHeight;
    }

    private void OnAddBlock(ReorderableList list)
    {
        blocksProperty.arraySize++;
        serializedObject.ApplyModifiedProperties();

        // Add default foldout state for the new block
        InitializeFoldoutsArray();
    }

    private void InitializeFoldoutsArray()
    {
        blockFoldouts = new bool[blocksProperty.arraySize];
        for (int i = 0; i < blockFoldouts.Length; i++)
        {
            blockFoldouts[i] = false; // Default to all foldouts open or adjust as necessary
        }
    }

    private void SwapFoldoutStates(int index1, int index2)
    {
        bool temp = blockFoldouts[index1];
        blockFoldouts[index1] = blockFoldouts[index2];
        blockFoldouts[index2] = temp;
    }
}
#endif
