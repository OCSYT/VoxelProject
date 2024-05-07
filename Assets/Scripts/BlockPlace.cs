using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockPlace : MonoBehaviour
{
    public float Dist = 25;
    public List<string> IgnoreBlocks = new List<string> { "air", "water", "lava" };
    public List<Block> blockList; // List of available blocks to place
    public string LookingAt = "";
    public Vector3 ChunkPosition;
    private int selectedBlockIndex = 0;
    private ChunkManager chunkManager;
    void Start()
    {
        chunkManager = ChunkManager.Instance;
        if (blockList == null)
        {
            blockList = new List<Block>();
            foreach (var item in ChunkManager.Instance.BlockList)
            {
                if(IgnoreBlocks.Contains(item.Key.ToLower())) { continue; }
                blockList.Add(new Block(item.Key, item.Value));
            }
        }
    }

    private string FindKeyFromValue(Dictionary<string, byte> dictionary, byte value)
    {
        foreach (var kvp in dictionary)
        {
            if (kvp.Value == value)
            {
                return kvp.Key;
            }
        }
        return null;
    }
    void Update()
    {

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll > 0f)
        {
            selectedBlockIndex = (selectedBlockIndex + 1) % blockList.Count;
        }
        else if (scroll < 0f)
        {
            selectedBlockIndex = (selectedBlockIndex - 1 + blockList.Count) % blockList.Count;
        }


        Chunk currentChunk = chunkManager.GetChunk(transform.position);
        if (currentChunk != null) {
            ChunkPosition = currentChunk.currentPos;
        }
        RaycastHit CheckHit;
        Ray CheckRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(CheckRay, out CheckHit, Dist))
        {
            Vector3 targetPosition = CheckHit.point - CheckHit.normal / 2f;
            LookingAt = FindKeyFromValue(chunkManager.BlockList, chunkManager.GetVoxelPosition(targetPosition));
        }
        else
        {
            LookingAt = "Air";
        }


        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            // Perform a raycast
            if (Physics.Raycast(ray, out hit, Dist))
            {

                Vector3 targetPosition = hit.point - hit.normal / 2f;
                PlaceBlock(targetPosition, 0);
            }
        }
        if (Input.GetMouseButtonDown(1))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            // Perform a raycast
            if (Physics.Raycast(ray, out hit, Dist))
            {

                Vector3 targetPosition = hit.point + hit.normal / 2f;
                PlaceBlock(targetPosition, blockList[selectedBlockIndex].blockId);
            }
        }
    }

    void PlaceBlock(Vector3 position, byte blockId)
    {
        ChunkManager.Instance.SetVoxelAtWorldPosition(position, blockId, true, true);
    }
}

public class Block // Example block class
{
    public string name;
    public byte blockId;

    public Block(string name, byte blockId)
    {
        this.name = name;
        this.blockId = blockId;
    }
}