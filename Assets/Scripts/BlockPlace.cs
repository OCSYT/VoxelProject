using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class BlockPlace : NetworkBehaviour
{
    public float Dist = 25;
    public List<string> IgnoreBlocks = new List<string> { "air", "water", "lava" };
    public List<Block> blockList; 
    public Dictionary<byte, int[]> blockFaces = new Dictionary<byte, int[]>(); 
    public string LookingAt = "";
    public Vector3 ChunkPosition;
    private int selectedBlockIndex = 0;
    private ChunkManager chunkManager;
    public LayerMask ignore;
    public MeshRenderer[] HandBlock;
    public float TextureSize;
    public float BlockSize;
    public Player player;
    public Transform blockPreview;
    public GameObject BlockOption;
    public Transform BlockContent;


    [HideInInspector]
    public List<(Vector3, byte)> BufferedBlockEvents = new List<(Vector3, byte) > ();
    private IEnumerator Start()
    {
        yield return new WaitUntil(() => ChunkManager.Instance != null);
        chunkManager = ChunkManager.Instance;
        if (blockList == null)
        {
            blockList = new List<Block>();
            foreach (KeyValuePair<byte, int[]> item in chunkManager.BlockFaces)
            {
                blockFaces.Add(item.Key, item.Value);
            }

            int index = 0;
            foreach (var item in chunkManager.BlockList)
            {
                if (IgnoreBlocks.Contains(item.Key.ToLower())) { continue; }
                blockList.Add(new Block(item.Key, item.Value));
                if (IsOwner)
                {
                    GameObject BOption = GameObject.Instantiate(BlockOption, BlockContent);
                    BOption.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = item.Key;

                    int newTexID = (byte)blockFaces[item.Value][0];
                    byte blockId = 0;
                    if (blockId == 0)
                    {
                        blockId = (byte)item.Value;
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

                    BOption.transform.GetChild(0).GetComponent<RawImage>().uvRect = new Rect(blockX, blockY, 1 / _BlockSize, 1 / _BlockSize);

                    int capturedIndex = index;
                    BOption.GetComponent<Button>().onClick.AddListener(delegate { SwitchBlock(capturedIndex); });
                    index++;
                }
            }
        }
        if (IsOwner)
        {
            UpdateBlockVisual();
        }
    }

    public void SwitchBlock(int index)
    {
        selectedBlockIndex = index;
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
        if (IsOwner == false) return;
        blockPreview.gameObject.SetActive(false);
        if (player.IsPaused || player.Chatting || !player.AllowMovement || player.PickingBlock) return;
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
        if (Physics.Raycast(player.playerCamera.transform.position, player.playerCamera.transform.forward, out CheckHit, Dist, ~ignore))
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
        }



        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;

            // Perform a raycast
            if (Physics.Raycast(player.playerCamera.transform.position, player.playerCamera.transform.forward, out hit, Dist, ~ignore))
            {

                Vector3 targetPosition = hit.point - hit.normal / 2f;

                ChunkManager.Instance.SetVoxelAtWorldPosition(targetPosition, 0, true, true);
                if (IsHost)
                {
                    PlaceBlockServerRPC(targetPosition, 0, true, true, NetworkManager.LocalClientId);
                }
                else
                {
                    PlaceBlockServerRPC(targetPosition, 0, false, true, NetworkManager.LocalClientId);
                }
            }
        }
        if (Input.GetMouseButtonDown(1))
        {
            RaycastHit hit;

            // Perform a raycast
            if (Physics.Raycast(player.playerCamera.transform.position, player.playerCamera.transform.forward, out hit, Dist, ~ignore))
            {

                Vector3 targetPosition = hit.point + hit.normal / 2f;
                if (PlayerInWay(targetPosition)) return;

                ChunkManager.Instance.SetVoxelAtWorldPosition(targetPosition, blockList[selectedBlockIndex].blockId, true, true);
                if (IsHost)
                {
                    PlaceBlockServerRPC(targetPosition, blockList[selectedBlockIndex].blockId, true, true, NetworkManager.LocalClientId);
                }
                else
                {
                    PlaceBlockServerRPC(targetPosition, blockList[selectedBlockIndex].blockId, false, true, NetworkManager.LocalClientId);
                }
            }
        }
    }

    bool PlayerInWay(Vector3 point)
    {
        Vector3 TargetPoint = new Vector3(
            Mathf.Floor(point.x) + 0.5f,
            Mathf.Floor(point.y) + 0.5f,
            Mathf.Floor(point.z) + 0.5f
        );

        Vector3 halfExtents = new Vector3(.5f, .5f, .5f);

        Collider[] Collisions = Physics.OverlapBox(TargetPoint, halfExtents);

        foreach(Collider _collision in Collisions)
        {
            if(_collision.gameObject.GetComponent<Player>() != null)
            {
                return true;
            }
        }

        return false;
    }


    public void PlaceBufferedBlocks()
    {
        foreach ((Vector3, byte) BlockEvent in BufferedBlockEvents)
        {
            ChunkManager.Instance.SetVoxelAtWorldPosition(BlockEvent.Item1, BlockEvent.Item2, false, true);
        }
        ChunkManager.Instance.ClearChunks();
        BufferedBlockEvents.Clear();
    }

    [ServerRpc]
    void PlaceBlockServerRPC(Vector3 position, byte blockId, bool buffer, bool regenerate, ulong id)
    {
        PlaceBlockClientRPC(position, blockId, buffer, regenerate, id);
    }
    [ClientRpc]
    void PlaceBlockClientRPC(Vector3 position, byte blockId, bool buffer, bool regenerate, ulong id)
    {
        if (NetworkManager.LocalClientId != id)
        {
            ChunkManager.Instance.SetVoxelAtWorldPosition(position, blockId, regenerate, true);
        }
        if (buffer && IsHost)
        {
            BufferedBlockEvents.Add((position, blockId));
        }
    }

    public class Block
    {
        public string name;
        public byte blockId;

        public Block(string name, byte blockId)
        {
            this.name = name;
            this.blockId = blockId;
        }
    }
}
