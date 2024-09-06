using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    public GameObject PlaceSFX;
    public GameObject BreakSFX;

    public GameObject HandModel;
    public GameObject BlockModel;

    // Hotbar elements
    public Material TexRef;
    public List<byte> HotbarBlock;
    public List<GameObject> HotbarSlot;
    public GameObject HotbarSelect; // Prefab for selected block highlight
    public int CurrentHotbarslot = 0;

    [HideInInspector]
    public List<(Vector3, Vector3, byte)> BufferedBlockEvents = new List<(Vector3, Vector3, byte)>();

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

                    BOption.transform.GetChild(0).GetComponent<RawImage>().uvRect = new Rect(blockX, blockY, 1 / _BlockSize, 1 / _BlockSize);

                    int capturedIndex = index;
                    BOption.GetComponent<Button>().onClick.AddListener(delegate { SwitchBlock(capturedIndex); });
                    index++;
                }
            }
        }

        if (IsOwner)
        {
            InitializeHotbar();
            UpdateBlockVisual();
        }
    }

    // Initialize the hotbar with the blocks and update visuals
    bool SetMaterial;
    public void InitializeHotbar()
    {

        for (int i = 0; i < HotbarSlot.Count; i++)
        {
            if (i < HotbarBlock.Count)
            {

                byte blockId = HotbarBlock[i];

                if (blockId == 0)
                {
                    HotbarSlot[i].transform.GetComponent<RawImage>().material = null;
                    HotbarSlot[i].transform.GetComponent<RawImage>().color = Color.white / 4;
                }
                else
                {
                    HotbarSlot[i].transform.GetComponent<RawImage>().material = TexRef;
                    HotbarSlot[i].transform.GetComponent<RawImage>().color = Color.white;
                    int textureID = chunkManager.BlockFaces[blockId][0];

                    // Update hotbar slot UI with block texture
                    float atlasSize = TextureSize;
                    float _BlockSize = BlockSize;
                    float blocksPerRow = atlasSize / _BlockSize;
                    float row = Mathf.Floor(textureID / blocksPerRow);
                    float col = textureID % blocksPerRow;
                    float blockX = col * (_BlockSize / atlasSize);
                    float blockY = row * (_BlockSize / atlasSize);
                    HotbarSlot[i].transform.GetComponent<RawImage>().uvRect = new Rect(blockX, blockY, 1 / _BlockSize, 1 / _BlockSize);
                }
            }
        }

        SetMaterial = true;
        UpdateHotbarSelection();
    }


    private GameObject HotbarSelectPrev;
    // Update the selected hotbar slot visual
    private void UpdateHotbarSelection()
    {
        if (HotbarSelectPrev)
        {
            Destroy(HotbarSelectPrev);
            HotbarSelectPrev = null;
        }
        HotbarSelectPrev = GameObject.Instantiate(HotbarSelect, HotbarSlot[CurrentHotbarslot].transform);
        HotbarSelect.transform.localPosition = Vector3.zero;
    }


    public void SwitchBlock(int index)
    {
        HotbarBlock[CurrentHotbarslot] = blockList[index].blockId;
        InitializeHotbar();
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
            byte newTexID = (byte)chunkManager.BlockFaces[(byte)HotbarBlock[selectedBlockIndex]][i];
            byte blockId = HotbarBlock[selectedBlockIndex];

            int textureID = newTexID == 0 ? blockId - 1 : newTexID;

            float atlasSize = TextureSize;
            float blockSize = BlockSize;
            float blocksPerRow = atlasSize / blockSize;
            float row = Mathf.Floor(textureID / blocksPerRow);
            float col = textureID % blocksPerRow;
            float blockX = col * (blockSize / atlasSize);
            float blockY = row * (blockSize / atlasSize);

            Material copyMat = HandBlock[i].material;
            copyMat.SetColor("_Offset", new Color(blockX, blockY, 0));
            HandBlock[i].material = copyMat;
        }
    }

    void Update()
    {
        if (IsOwner == false) return;

        if (HotbarBlock[selectedBlockIndex] == 0)
        {
            HandModel.SetActive(true);
            BlockModel.SetActive(false);
        }
        else
        {
            HandModel.SetActive(false);
            BlockModel.SetActive(true);
        }

        blockPreview.gameObject.SetActive(false);
        if (player.IsPaused || player.Chatting || !player.AllowMovement) return;

        if(Input.GetKeyDown(KeyCode.Q)) {
            HotbarBlock[CurrentHotbarslot] = 0;
            UpdateHotbarSelection();
            InitializeHotbar();
            UpdateBlockVisual();
        }

        for (int i = 0; i < HotbarSlot.Count; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                CurrentHotbarslot = i;
                selectedBlockIndex = CurrentHotbarslot;
                UpdateHotbarSelection();
                UpdateBlockVisual();
            }
        }


        if (player.PickingBlock)
        {
            return;
        }
        // Scroll through the hotbar
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f)
        {
            CurrentHotbarslot = (CurrentHotbarslot + 1) % HotbarSlot.Count;
            selectedBlockIndex = CurrentHotbarslot;
            UpdateHotbarSelection();
            UpdateBlockVisual();
        }
        else if (scroll < 0f)
        {
            CurrentHotbarslot = (CurrentHotbarslot - 1 + HotbarSlot.Count) % HotbarSlot.Count;
            selectedBlockIndex = CurrentHotbarslot;
            UpdateHotbarSelection();
            UpdateBlockVisual();
        }


        Chunk currentChunk = chunkManager.GetChunk(transform.position);
        if (currentChunk != null)
        {
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

        // Left click to break blocks
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;

            if (Physics.Raycast(player.playerCamera.transform.position, player.playerCamera.transform.forward, out hit, Dist, ~ignore))
            {
                Vector3 targetPosition = hit.point - hit.normal / 2f;

                ChunkManager.Instance.SetVoxelAtWorldPosition(targetPosition, Vector3.zero, 0, true, true);
                if (IsHost)
                {
                    PlaceBlockServerRPC(targetPosition, Vector3.zero, 0, true, true, NetworkManager.LocalClientId);
                }
                else
                {
                    PlaceBlockServerRPC(targetPosition, Vector3.zero, 0, false, true, NetworkManager.LocalClientId);
                }
                GameObject SFX = GameObject.Instantiate(BreakSFX, targetPosition, Quaternion.identity);
                Destroy(SFX, 5f);
            }
        }

        // Right click to place blocks
        if (Input.GetMouseButtonDown(1) && HotbarBlock[selectedBlockIndex] != 0)
        {
            RaycastHit hit;

            if (Physics.Raycast(player.playerCamera.transform.position, player.playerCamera.transform.forward, out hit, Dist, ~ignore))
            {
                Vector3 targetPosition = hit.point + hit.normal / 2f;
                if (PlayerInWay(targetPosition)) return;

                Vector3 BlockDir = hit.normal.normalized;
                if (BlockDir == Vector3.up || BlockDir == Vector3.down)
                {
                    BlockDir = -player.transform.forward.normalized;
                }

                ChunkManager.Instance.SetVoxelAtWorldPosition(targetPosition, BlockDir, HotbarBlock[selectedBlockIndex], true, false);
                if (IsHost)
                {
                    PlaceBlockServerRPC(targetPosition, BlockDir, HotbarBlock[selectedBlockIndex], true, false, NetworkManager.LocalClientId);
                }
                else
                {
                    PlaceBlockServerRPC(targetPosition, BlockDir, HotbarBlock[selectedBlockIndex], false, false, NetworkManager.LocalClientId);
                }
                GameObject SFX = GameObject.Instantiate(PlaceSFX, targetPosition, Quaternion.identity);
                Destroy(SFX, 5f);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlaceBlockServerRPC(Vector3 Target, Vector3 Direction, byte BlockID, bool Host, bool Remove, ulong owner)
    {
        if (!Remove)
        {
            ChunkManager.Instance.SetVoxelAtWorldPosition(Target, Direction, BlockID, Host, Remove);
        }
        else
        {
            ChunkManager.Instance.SetVoxelAtWorldPosition(Target, Vector3.zero, 0, Host, Remove);
        }
        BufferedBlockEvents.Add((Target, Direction, BlockID));
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
    public void PlaceBufferedBlocks()
    {
        foreach ((Vector3, Vector3, byte) BlockEvent in BufferedBlockEvents)
        {
            ChunkManager.Instance.SetVoxelAtWorldPosition(BlockEvent.Item1, BlockEvent.Item2, BlockEvent.Item3, false, true);
        }
        ChunkManager.Instance.ClearChunks();
        BufferedBlockEvents.Clear();
    }
    public bool PlayerInWay(Vector3 BlockPosition)
    {
        if (Physics.CheckBox(BlockPosition, new Vector3(0.3f, 0.3f, 0.3f), Quaternion.identity, 1 << 3))
        {
            return true;
        }
        return false;
    }
}
