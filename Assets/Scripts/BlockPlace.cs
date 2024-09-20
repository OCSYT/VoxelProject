using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using System.Linq;

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

    public GameObject TNT;
    public GameObject TNTFX;
    public int TNT_Radius = 8;
    public float TNT_Time = 3;
    public class Ref<T>
    {
        private T backing;
        public T Value
        {
            get { return backing; }
            set { backing = value; }
        }

        public Ref(T reference)
        {
            backing = reference;
        }
    }

    Ref<bool> CanBreak = new Ref<bool>(true);
    Ref<bool> CanPlace = new Ref<bool>(true);
    private float BlockEventTime = .15f;


    IEnumerator BlockChangeEvent(Ref<bool> EventVal)
    {
        EventVal.Value = false;
        yield return new WaitForSeconds(BlockEventTime);
        EventVal.Value = true;
    }

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
            while(player.AllowMovement == false)
            {
                yield return new WaitForSeconds(.1f);
            }
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

        if (Input.GetKeyDown(KeyCode.Q))
        {
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
        if (ChunkManager.Instance != null)
        {
            if (Input.GetMouseButton(0) && CanBreak.Value)
            {

                StartCoroutine(BlockChangeEvent(CanBreak));

                RaycastHit hit;

                if (Physics.Raycast(player.playerCamera.transform.position, player.playerCamera.transform.forward, out hit, Dist, ~ignore))
                {
                    Vector3 targetPosition = hit.point - hit.normal / 2f;

                    if (Vector3Int.FloorToInt(targetPosition).y != ChunkManager.Instance.worldFloor)
                    {

                        byte BlockID = ChunkManager.Instance.GetVoxelPosition(targetPosition);

                        string BlockName = ChunkManager.Instance.GetBlockKey(ChunkManager.Instance.BlockList, BlockID);


                        if (BlockName == "TNT" && player.Crouch.Value == false)
                        {
                            TNTServerRPC(targetPosition);
                        }

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
            }

            // Right click to place blocks
            if (Input.GetMouseButton(1) && HotbarBlock[selectedBlockIndex] != 0 && CanPlace.Value)
            {

                StartCoroutine(BlockChangeEvent(CanPlace));

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

                    ChunkManager.Instance.SetVoxelAtWorldPosition(targetPosition, BlockDir, HotbarBlock[selectedBlockIndex], true, true);
                    if (IsHost)
                    {
                        PlaceBlockServerRPC(targetPosition, BlockDir, HotbarBlock[selectedBlockIndex], true, true, NetworkManager.LocalClientId);
                    }
                    else
                    {
                        PlaceBlockServerRPC(targetPosition, BlockDir, HotbarBlock[selectedBlockIndex], false, true, NetworkManager.LocalClientId);
                    }
                    GameObject SFX = GameObject.Instantiate(PlaceSFX, targetPosition, Quaternion.identity);
                    Destroy(SFX, 5f);
                }
            }
        }
    }


    public static async Task<byte[]> SerializeChunksAsync(List<Vector3Int> chunks)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(chunks.Count);
                foreach (var chunk in chunks)
                {
                    writer.Write(chunk.x);
                    writer.Write(chunk.y);
                    writer.Write(chunk.z);
                }
            }
            return await Task.FromResult(memoryStream.ToArray());
        }
    }

    // Convert the byte array back to a List<Vector3Int>
    public static async Task<List<Vector3Int>> DeserializeChunksAsync(byte[] data)
    {
        List<Vector3Int> chunks = new List<Vector3Int>();
        using (MemoryStream memoryStream = new MemoryStream(data))
        {
            using (BinaryReader reader = new BinaryReader(memoryStream))
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    int x = reader.ReadInt32();
                    int y = reader.ReadInt32();
                    int z = reader.ReadInt32();
                    chunks.Add(new Vector3Int(x, y, z));
                }
            }
        }
        return await Task.FromResult(chunks);
    }

    // Compress the serialized byte array
    public static async Task<byte[]> CompressAsync(byte[] data)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            using (GZipStream gzip = new GZipStream(memoryStream, CompressionMode.Compress))
            {
                await gzip.WriteAsync(data, 0, data.Length);
            }
            return memoryStream.ToArray();
        }
    }

    // Decompress the byte array
    public static async Task<byte[]> DecompressAsync(byte[] compressedData)
    {
        using (MemoryStream memoryStream = new MemoryStream(compressedData))
        {
            using (MemoryStream decompressedStream = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    await gzip.CopyToAsync(decompressedStream);
                }
                return decompressedStream.ToArray();
            }
        }
    }



    [ServerRpc]
    void TNTServerRPC(Vector3 position)
    {
        TNTClientRPC(position);
    }

    HashSet<Rigidbody> TNT_Instances = new HashSet<Rigidbody>();
    [ClientRpc]
    void TNTClientRPC(Vector3 position)
    {
        Vector3 targetPoint = new Vector3(
            Mathf.Floor(position.x) + 0.5f,
            Mathf.Floor(position.y) + 0.5f,
            Mathf.Floor(position.z) + 0.5f
        );

        GameObject tntInstance = GameObject.Instantiate(TNT, targetPoint, Quaternion.identity);
        Rigidbody tntRigidbody = tntInstance.GetComponent<Rigidbody>();
        TNT_Instances.Add(tntRigidbody);
        StartCoroutine(TNT_Explode(tntInstance, tntRigidbody));
        StartCoroutine(TNT_Check(tntInstance));
    }

    IEnumerator TNT_Check(GameObject instance)
    {
        Rigidbody tntRB = instance.GetComponent<Rigidbody>();
        while (instance != null)
        {
            Chunk chunk = ChunkManager.Instance.GetChunk(instance.transform.position);
            tntRB.isKinematic = chunk == null;
            yield return new WaitForSecondsRealtime((TNT_Instances.Count / 10) / 2);
        }
    }

    IEnumerator TNT_Explode(GameObject instance, Rigidbody instanceBody)
    {
        yield return new WaitForSeconds(TNT_Time - UnityEngine.Random.value);
        Vector3 instancePoint = instance.transform.position;
        float sqrTNTRadius = TNT_Radius * TNT_Radius;

        if (IsOwner)
        {
            HashSet<Vector3Int> chunksToRegenerate = new HashSet<Vector3Int>();
            List<Vector3> tntPoints = new List<Vector3>();
            List<Vector3> points = new List<Vector3>();


            for (int x = -TNT_Radius; x <= TNT_Radius; x++)
            {
                yield return new WaitForFixedUpdate();
                for (int y = -TNT_Radius; y <= TNT_Radius; y++)
                {
                    for (int z = -TNT_Radius; z <= TNT_Radius; z++)
                    {
                        Vector3 offset = new Vector3(x, y, z);
                        Vector3 currentPoint = instancePoint + offset;

                        if (offset.sqrMagnitude <= sqrTNTRadius)
                        {
                            Chunk chunk = ChunkManager.Instance.GetChunk(currentPoint);
                            if (chunk != null)
                            {
                                chunksToRegenerate.Add(chunk.currentPos);
                            }

                            byte voxelID = ChunkManager.Instance.GetVoxelPosition(currentPoint);
                            foreach (var blockInfo in ChunkManager.Instance.Blocks)
                            {
                                if (voxelID == blockInfo.Value)
                                {
                                    if (blockInfo.Name == "TNT" && Vector3Int.FloorToInt(currentPoint) != Vector3Int.FloorToInt(instancePoint))
                                    {
                                        tntPoints.Add(currentPoint);
                                    }

                                    if (blockInfo.Destructable)
                                    {
                                        ChunkManager.Instance.SetVoxelAtWorldPosition(currentPoint, Vector3.zero, 0, false, true);
                                        if (IsHost)
                                        {
                                            PlaceBlockServerRPC(currentPoint, Vector3.zero, 0, true, false, NetworkManager.LocalClientId);
                                        }
                                        else
                                        {
                                            PlaceBlockServerRPC(currentPoint, Vector3.zero, 0, false, false, NetworkManager.LocalClientId);
                                        }
                                    }
                                    break; // Exit loop after finding the block
                                }
                            }
                        }
                    }
                }
            }


            yield return new WaitForSecondsRealtime(0.1f);
            RegnenerateTNTChunksServerRPCAsync(chunksToRegenerate.ToList());

            TNT_Instances.Remove(instanceBody);
            Destroy(instance);

            GameObject _TNTFX = GameObject.Instantiate(TNTFX, instancePoint, Quaternion.identity);
            Destroy(_TNTFX, 5f);


            ApplyExplosionForce(instancePoint);

            foreach (Vector3 point in tntPoints)
            {
                TNTServerRPC(point);
                yield return new WaitForFixedUpdate();
            }
        }
        else
        {
            TNT_Instances.Remove(instanceBody);
            Destroy(instance);
            GameObject _TNTFX = GameObject.Instantiate(TNTFX, instancePoint, Quaternion.identity);
            Destroy(_TNTFX, 5f);
            ApplyExplosionForce(instancePoint);
        }
    }

    private void ApplyExplosionForce(Vector3 instancePoint)
    {
        foreach (Rigidbody tnt in TNT_Instances)
        {
            if (tnt != null)
            {
                Vector3 direction = (tnt.transform.position - instancePoint);
                direction.y = 1;
                tnt.AddExplosionForce(TNT_Radius * 2, instancePoint, TNT_Radius, 3.0f, ForceMode.Impulse);
            }
        }
    }


    async void RegnenerateTNTChunksServerRPCAsync(List<Vector3Int> chunks)
    {
        RegnenerateTNTChunksServerRPC(await SerializeChunksAsync(chunks));
    }

    [ServerRpc]
    void RegnenerateTNTChunksServerRPC(byte[] chunksToRegenerate)
    {
        RegnenerateTNTChunksClientRPC(chunksToRegenerate);
    }

    [ClientRpc]
    void RegnenerateTNTChunksClientRPC(byte[] chunksToRegenerateBytes)
    {
        RegnenerateTNTChunksClientAsync(chunksToRegenerateBytes);
    }

    async void RegnenerateTNTChunksClientAsync(byte[] chunksToRegenerateBytes)
    {
        List<Vector3Int> chunksToRegenerate = await DeserializeChunksAsync(chunksToRegenerateBytes);
        if (ChunkManager.Instance != null)
        {
            foreach (Vector3Int chunk in chunksToRegenerate)
            {
                if (ChunkManager.Instance.activeChunks.ContainsKey(chunk))
                {
                    ChunkManager.Instance.activeChunks[chunk].GenerateMesh(true);
                }
            }
        }
    }

    [ServerRpc]
    void PlaceBlockServerRPC(Vector3 position, Vector3 normal, byte blockId, bool buffer, bool regenerate, ulong id)
    {
        PlaceBlockClientRPC(position, normal, blockId, buffer, regenerate, id);
    }

    [ClientRpc]
    void PlaceBlockClientRPC(Vector3 position, Vector3 normal, byte blockId, bool buffer, bool regenerate, ulong id)
    {
        if (NetworkManager.LocalClientId != id)
        {
            StartCoroutine(WaitForChunkManagerThenPlaceBlock(position, normal, blockId, buffer, regenerate, id));
        }
        if(buffer && IsHost)
        {
            BufferedBlockEvents.Add((position, normal, blockId));
        }
    }

    private IEnumerator WaitForChunkManagerThenPlaceBlock(Vector3 position, Vector3 normal, byte blockId, bool buffer, bool regenerate, ulong id)
    {

        while (ChunkManager.Instance == null)
        {
            yield return null;  
        }

        if (NetworkManager.LocalClientId != id)
        {
            ChunkManager.Instance.SetVoxelAtWorldPosition(position, normal, blockId, regenerate, true);
            if (regenerate)
            {
                if (blockId != 0)
                {
                    GameObject SFX = GameObject.Instantiate(PlaceSFX, position, Quaternion.identity);
                    Destroy(SFX, 5f);
                }
                else
                {
                    GameObject SFX = GameObject.Instantiate(BreakSFX, position, Quaternion.identity);
                    Destroy(SFX, 5f);
                }
            }
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
    public void PlaceBufferedBlocks()
    {
        foreach ((Vector3, Vector3, byte) BlockEvent in BufferedBlockEvents)
        {
            ChunkManager.Instance.SetVoxelAtWorldPosition(BlockEvent.Item1, BlockEvent.Item2, BlockEvent.Item3, false, true);
        }
        ChunkManager.Instance.ClearChunks();
        BufferedBlockEvents.Clear();
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

        foreach (Collider _collision in Collisions)
        {
            if (_collision.gameObject.GetComponent<Player>() != null)
            {
                return true;
            }
        }

        return false;
    }

}
