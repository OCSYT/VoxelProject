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
    public LayerMask ignore;
    public LayerMask playerLayer;
    public MeshRenderer[] HandBlock;
    public float TextureSize;
    public float BlockSize;
    public Player player;
    public Transform blockPreview;
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
        UpdateBlockVisual();
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

    void UpdateBlockVisual()
    {
        for (int i = 0; i < HandBlock.Length; i++)
        {
            byte newTexID = (byte)chunkManager.BlockFaces[(byte)blockList[selectedBlockIndex].blockId][i];
            byte blockId = blockList[selectedBlockIndex].blockId;

            int textureID = newTexID == 0 ? blockId - 1 : newTexID;

            float atlasSize = TextureSize;
            float blockSize = BlockSize;
            float blocksPerRow = atlasSize / blockSize;
            float row = Mathf.Floor(textureID / blocksPerRow);
            float col = textureID % blocksPerRow;
            float blockX = col * (blockSize / atlasSize);
            float blockY = row * (blockSize / atlasSize);
            float uvSize = 1.0f / blocksPerRow;
            Material copyMat = HandBlock[i].material;
            copyMat.SetColor("_Offset", new Color(blockX, blockY, 0));
            HandBlock[i].material = copyMat;
        }
    }

    void Update()
    {
        if (player.isPaused || !player.AllowMovement) return;
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll > 0f)
        {
            selectedBlockIndex = (selectedBlockIndex + 1) % blockList.Count;
            UpdateBlockVisual();
        }
        else if (scroll < 0f)
        {
            selectedBlockIndex = (selectedBlockIndex - 1 + blockList.Count) % blockList.Count;
            UpdateBlockVisual();
        }




        Chunk currentChunk = chunkManager.GetChunk(transform.position);
        if (currentChunk != null) {
            ChunkPosition = currentChunk.currentPos;
        }
        RaycastHit CheckHit;
        Ray CheckRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(CheckRay, out CheckHit, Dist, ~ignore))
        {
            Vector3 targetPosition = CheckHit.point - CheckHit.normal / 2f;

            Vector3 targetPositionPreview = CheckHit.point - CheckHit.normal / 2;

            // Calculate the correct aligned position
            blockPreview.position = new Vector3(
                Mathf.Floor(targetPositionPreview.x) + 0.5f,
                Mathf.Floor(targetPositionPreview.y) + 0.5f,
                Mathf.Floor(targetPositionPreview.z) + 0.5f
            );


            blockPreview.transform.rotation = Quaternion.identity;
            blockPreview.gameObject.SetActive(true);
            LookingAt = FindKeyFromValue(chunkManager.BlockList, chunkManager.GetVoxelPosition(targetPosition));
            Debug.DrawLine(transform.position, CheckHit.point, Color.red);
        }
        else
        {
            LookingAt = "Air";
            blockPreview.gameObject.SetActive(false);
        }



        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            // Perform a raycast
            if (Physics.Raycast(ray, out hit, Dist, ~ignore))
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
            if (Physics.Raycast(ray, out hit, Dist, ~ignore))
            {

                Vector3 targetPosition = hit.point + hit.normal / 2f;
                RaycastHit PlayerRay;
                if (!Physics.SphereCast(hit.point - hit.normal, 1f, hit.normal, out PlayerRay, 1f, playerLayer))
                {
                    PlaceBlock(targetPosition, blockList[selectedBlockIndex].blockId);
                }
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