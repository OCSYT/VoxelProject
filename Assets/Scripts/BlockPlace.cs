using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockPlace : MonoBehaviour
{
    public float Dist = 25;
    public List<Block> blockList; // List of available blocks to place
    public int selectedBlockIndex = 0;
    void Start()
    {
        if (blockList == null)
        {
            blockList = new List<Block>();
            blockList.Add(new Block("Grass", (byte)1));
            blockList.Add(new Block("Dirt", (byte)2));
            blockList.Add(new Block("Stone", (byte)3));
            blockList.Add(new Block("Cobblestone", (byte)4));
            blockList.Add(new Block("Stone Bricks", (byte)5));
            blockList.Add(new Block("Oak Planks", (byte)6));
            blockList.Add(new Block("Oak Log", (byte)7));
            blockList.Add(new Block("Leaves", (byte)8));
            blockList.Add(new Block("Glass", (byte)9));
            blockList.Add(new Block("Glowstone", (byte)10));
            blockList.Add(new Block("Sand", (byte)11));
            blockList.Add(new Block("Lava", (byte)15));
            blockList.Add(new Block("Water", (byte)16));
        }
    }

    // Update is called once per frame
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