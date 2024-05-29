using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using UnityEngine;
using System.Xml.Serialization;
using Newtonsoft.Json;

public class ChunkManager : MonoBehaviour
{

    public AnimationCurve terrainCurve;


    [Serializable]
    public class SerializableVector3Int
    {
        public int x, y, z;
        public SerializableVector3Int() { }

        public SerializableVector3Int(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public SerializableVector3Int(Vector3Int vector)
        {
            this.x = vector.x;
            this.y = vector.y;
            this.z = vector.z;
        }

        public Vector3Int ToVector3Int()
        {
            return new Vector3Int(x, y, z);
        }
    }

    [Serializable]
    public class ChunkData
    {
        public SerializableVector3Int Position;
        public List<byte> Data;

        public ChunkData() { }
        public ChunkData(Vector3Int position, byte[,,] data)
        {
            Position = new SerializableVector3Int(position);

            Data = new List<byte>();
            int sizeX = data.GetLength(0);
            int sizeY = data.GetLength(1);
            int sizeZ = data.GetLength(2);
            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        Data.Add(data[x, y, z]);
                    }
                }
            }
        }
    }

    [Serializable]
    public class SaveData
    {
        public int Seed;
        public List<ChunkData> Chunks;

        public SaveData() { }
        public SaveData(int seed, Dictionary<Vector3Int, byte[,,]> chunkCache)
        {
            Seed = seed;
            Chunks = chunkCache.Select(kvp => new ChunkData(kvp.Key, kvp.Value)).ToList();
        }
    }

    public void SaveGame(string filePath)
    {
        SaveData saveData = new SaveData(seed, chunkCache);

        // Serialize the SaveData object to JSON
        string jsonString = JsonConvert.SerializeObject(saveData, Formatting.Indented);

        // Write the JSON string to the file
        File.WriteAllText(filePath, jsonString);

        Debug.Log("Game saved successfully!");
    }

    public void LoadGame(string filePath)
    {
        if (File.Exists(filePath))
        {
            // Read the JSON string from the file
            string jsonString = File.ReadAllText(filePath);

            // Deserialize the JSON string to a SaveData object
            SaveData saveData = JsonConvert.DeserializeObject<SaveData>(jsonString);

            // Restore seed
            seed = saveData.Seed;

            // Clear current chunks
            foreach (var chunkPosition in activeChunksObj.Keys.ToList())
            {
                Destroy(activeChunksObj[chunkPosition]);
            }
            activeChunks.Clear();
            activeChunksObj.Clear();

            // Load chunk data
            chunkCache = saveData.Chunks.ToDictionary(
                chunk => chunk.Position.ToVector3Int(),
                chunk => ConvertTo3DArray(chunk.Data, ChunkSize, ChunkSize, ChunkSize)
            );

            // Regenerate chunks based on loaded data
            foreach (var chunk in chunkCache.Keys)
            {
                CreateChunk(chunk);
            }

            Debug.Log("Game loaded successfully!");
        }
        else
        {
            Debug.LogError("Save file not found!");
        }
    }





    [Serializable]
    public class Block
    {
        public string Name = "NewBlock";
        public byte Value = 0;
        public int FrontFace = 0;
        public int BackFace = 0;
        public int LeftFace = 0;
        public int RightFace = 0;
        public int TopFace = 0;
        public int BottomFace = 0;

        [ColorUsage(true, true)]
        public Color Light = Color.black;
    }
    public List<Block> Blocks = new List<Block>();

    public int seed = System.Guid.NewGuid().GetHashCode();
    public GameObject ChunkBorderPrefab;
    public bool ChunkBorders;
    public int renderDistance = 5;
    public Transform target;
    public int ChunkSize;
    public float TextureSize = 256;
    public float BlockSize = 16;
    public Material mat;
    public Material transparent;
    public List<byte> NoCollisonBlocks;
    public List<byte> TransparentBlocks;
    public float Daylength = 1;
    public float GameTime = 0;
    public static ChunkManager Instance { get; private set; }
    public Dictionary<Vector3Int, Chunk> activeChunks = new Dictionary<Vector3Int, Chunk>();
    public Dictionary<Vector3Int, GameObject> activeChunksObj = new Dictionary<Vector3Int, GameObject>();
    private Vector3 previousTargetPosition;
    private float previousTargetRotation;
    private Dictionary<Vector3Int, byte[,,]> chunkCache = new Dictionary<Vector3Int, byte[,,]>();
    public Dictionary<string, byte> BlockList = new Dictionary<string, byte>();
    public Dictionary<byte, Color> BlockListLight = new Dictionary<byte, Color>();
    public Dictionary<byte, int[]> BlockFaces = new Dictionary<byte, int[]>();
    private Light DirectionalLight;
    public Color AmbientColor;
    public float MinimumAmbient;
    public int IgnoreLayer;
    private ConcurrentQueue<Vector3Int> chunksToCreate = new ConcurrentQueue<Vector3Int>();

    void Awake()
    {
        foreach (var block in Blocks)
        {
            BlockList.Add(block.Name, block.Value);
            BlockListLight.Add(block.Value, block.Light);
            BlockFaces.Add(block.Value, new int[] { block.FrontFace, block.BackFace, block.LeftFace, block.RightFace,block.TopFace, block.BottomFace });
        }

        if (Instance == null)
        {
            Instance = this;
        }
        previousTargetPosition = target.position;
        previousTargetRotation = Mathf.Round(target.rotation.eulerAngles.y);
        DirectionalLight = GameObject.FindObjectOfType<Light>();
        InvokeRepeating("GenerateChunkUpdate", 0, .1f);
    }

    private byte[,,] ConvertTo3DArray(List<byte> data, int sizeX, int sizeY, int sizeZ)
    {
        byte[,,] result = new byte[sizeX, sizeY, sizeZ];
        int index = 0;
        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    result[x, y, z] = data[index++];
                }
            }
        }
        return result;
    }



    bool cancelledGeneration = false;
    async void GenerateChunkUpdate()
    {
        Vector3 targetPosition = target.position;
        await Task.Run(async () =>
        {

            Vector3 halfChunkSize = new Vector3(ChunkSize / 2f, ChunkSize / 2f, ChunkSize / 2f);
            float distSquared = renderDistance * ChunkSize * renderDistance * ChunkSize;

            if (generatingChunks)
                return;

            generatingChunks = true;


            Vector3Int targetChunkPos = new Vector3Int(
                Mathf.FloorToInt(targetPosition.x / ChunkSize),
                Mathf.FloorToInt(targetPosition.y / ChunkSize),
                Mathf.FloorToInt(targetPosition.z / ChunkSize)
            );

            ConcurrentBag<Vector3Int> chunkPositionsBag = new ConcurrentBag<Vector3Int>();

            Parallel.For(-renderDistance, renderDistance + 1, x =>
            {
                for (int y = -renderDistance; y <= renderDistance; y++)
                {
                    for (int z = -renderDistance; z <= renderDistance; z++)
                    {
                        Vector3Int chunkPosition = new Vector3Int(
                            targetChunkPos.x + x,
                            targetChunkPos.y + y,
                            targetChunkPos.z + z
                        );

                        Vector3 chunkCenter = chunkPosition * ChunkSize + halfChunkSize;
                        float distance = (chunkCenter - targetPosition).sqrMagnitude;

                        if (distance < distSquared && !activeChunks.ContainsKey(chunkPosition))
                        {
                            chunkPositionsBag.Add(chunkPosition);
                        }
                    }
                }
            });

            List<Vector3Int> sortedChunkPositions = chunkPositionsBag.ToList();
            sortedChunkPositions.Sort((a, b) =>
            {
                float distanceA = Vector3.Distance(targetPosition, a * ChunkSize);
                float distanceB = Vector3.Distance(targetPosition, b * ChunkSize);
                return distanceA.CompareTo(distanceB);
            });

            foreach (Vector3Int chunkPosition in sortedChunkPositions)
            {
                await Task.Delay(16);
                if (cancelledGeneration)
                {
                    cancelledGeneration = false;
                    break;
                }
                chunksToCreate.Enqueue(chunkPosition);
            }

            generatingChunks = false;
            await Task.CompletedTask;


        });


    }


 
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            SaveGame(Application.dataPath + "/../" + "Saves/Save.dat");
        }
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            LoadGame(Application.dataPath + "/../" + "Saves/Save.dat");
        }

        if (Vector3.Distance(target.position, previousTargetPosition) > ChunkSize || previousTargetRotation != Mathf.Round(target.rotation.eulerAngles.y))
        {
            previousTargetPosition = target.position;
            previousTargetRotation = Mathf.Round(target.rotation.eulerAngles.y);
            cancelledGeneration = true;
        }

        GameTime += Time.deltaTime;
        float SkyValue = ((Mathf.Sin(GameTime * Daylength / 500) * 360)) % 360;
        DirectionalLight.transform.rotation = Quaternion.Euler(SkyValue, 45, 0);
        DirectionalLight.intensity = 2 * Vector3.Dot(DirectionalLight.transform.forward, -Vector3.up);
        RenderSettings.ambientLight = AmbientColor * Mathf.Clamp(Vector3.Dot(DirectionalLight.transform.forward, -Vector3.up), MinimumAmbient, 1);

        if (Instance == null)
        {
            Instance = this;
        }


        while (chunksToCreate.TryDequeue(out Vector3Int chunkPosition))
        {
            CreateChunk(chunkPosition);
        }

        UpdateChunks();

    }


    private bool generatingChunks = false;

    private void CreateChunk(Vector3Int chunkPosition)
    {
        if (activeChunks.ContainsKey(chunkPosition))
            return;

        GameObject newChunk = new GameObject
        {
            name = $"{chunkPosition.x}_{chunkPosition.y}_{chunkPosition.z}",
            transform = { position = chunkPosition * ChunkSize }
        };

        if (ChunkBorders)
        {
            GameObject border = Instantiate(ChunkBorderPrefab, newChunk.transform);
            border.transform.localScale = Vector3.one * ChunkSize;
        }

        Chunk chunk = newChunk.AddComponent<Chunk>();
        chunk.Init(mat, transparent, TransparentBlocks.ToArray(), NoCollisonBlocks.ToArray(), ChunkSize, TextureSize, BlockSize, chunkPosition);

        activeChunks.Add(chunkPosition, chunk);
        activeChunksObj.Add(chunkPosition, newChunk);

        byte[,,] newData;
        if (!chunkCache.ContainsKey(chunkPosition))
        {
            newData = SetTerrain(chunk);
        }
        else
        {
            newData = chunkCache[chunkPosition];
        }
        chunk.SetData(newData);
        chunk.GenerateTerrain();
    }





    public float waterLevel = 10;
    public float height = 25;
    public float scale = 0.01f;



    float Errosion(Vector3 position, float scale)
    {
        float initialOffset = seed / 1000f;
        float frequency = scale;

        float sampleX = ((position.x) + initialOffset) * frequency;
        float sampleY = ((position.z) + initialOffset) * frequency;


        float perlinValue = (Mathf.PerlinNoise(sampleX, sampleY) - 0.5f) * 2;

        return perlinValue;
    }

    float Continentalness(Vector3 position, float scale, int octaves, float persistence, float lacunarity)
    {
        float initialOffset = seed / 1000f;

        float amplitude = 1f;
        float frequency = scale;
        float continentalness = 0f;

        for (int octave = 0; octave < octaves; octave++)
        {
            // Convert position to float and adjust based on scale and frequency
            float sampleX = ((position.x) + initialOffset) * frequency;
            float sampleY = ((position.z) + initialOffset) * frequency;

            // Sample the Perlin noise
            float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;
            continentalness += perlinValue * amplitude;

            // Update amplitude and frequency for the next octave
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        // Normalize the result to range between 0 and 1
        continentalness = Mathf.Clamp01((continentalness + 1f) / 2f);
        continentalness = terrainCurve.Evaluate(continentalness);

        return continentalness;
    }


    byte[,,] SetTerrain(Chunk chunk)
    {
        System.Random random = new System.Random(seed + (int)chunk.transform.position.x
            + (int)chunk.transform.position.y + (int)chunk.transform.position.z);

        byte[,,] VoxelData = new byte[ChunkSize, ChunkSize, ChunkSize];
        Vector3 chunkCornerWorldPos = chunk.transform.position;


        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                Vector3 voxelPosition2D = chunkCornerWorldPos + new Vector3(x, 0, z);
                float _Continentalness = Continentalness(voxelPosition2D, scale, 4, 0.5f, 2);
                float _Errosion = Errosion(voxelPosition2D, scale / 5);

                float perlinRaw = (_Continentalness * height) * _Errosion;

                for (int y = 0; y < ChunkSize; y++)
                {
                    Vector3 voxelPosition = chunkCornerWorldPos + new Vector3(x, y, z);

                    byte voxelValue = 0;

                    float offset = (seed / 1000);


                    float perlinValue = perlinRaw;

                    int perlinRounded = (Mathf.RoundToInt(perlinValue));


                    if (voxelPosition.y > perlinRounded && voxelPosition.y <= waterLevel)
                    {
                        voxelValue = BlockList["Water"];
                    }
                    else
                    {
                        if (voxelPosition.y == perlinRounded)
                        {
                            if(voxelPosition.y == waterLevel)
                            {
                                voxelValue = BlockList["Sand"];
                            }
                            else
                            {
                                if(voxelPosition.y < waterLevel)
                                {
                                    voxelValue = BlockList["Sand"];
                                }
                                else
                                {
                                    voxelValue = BlockList["Grass"]; //grass
                                }
                            }
                        }
                        else
                        {
                            if (voxelPosition.y < perlinRounded)
                            {
                                voxelValue = BlockList["Dirt"]; //dirt
                            }
                            if (voxelPosition.y < perlinRounded - 4)
                            {
                                voxelValue = BlockList["Stone"]; //stone
                            }
                        }
                    }

                    VoxelData[x, y, z] = voxelValue;
                }
            }
        }

        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    if (VoxelData[x, y, z] == BlockList["Grass"]) 
                    {

                        if (y + 10 < ChunkSize && x + 2 < ChunkSize && z + 2 < ChunkSize
                            && x - 2 > 0 && z - 2 > 0 
                            && random.NextDouble() > .975f && !HasWoodNeighbor(VoxelData, x, y, z, 3))
                        {
                            for (int i = y + 1; i < y + 4; i++)
                            {
                                if (VoxelData[x, i, z] == 0)
                                {
                                    VoxelData[x, i, z] = BlockList["Oak Log"];
                                }
                            }
                            for (int offsetY = y + 4; offsetY < y + 8; offsetY++)
                            {
                                for (int offsetX = -2; offsetX <= 2; offsetX++)
                                {
                                    for (int offsetZ = -2; offsetZ <= 2; offsetZ++)
                                    {
                                        if (VoxelData[x + offsetX, offsetY, z + offsetZ] == 0)
                                        {
                                            VoxelData[x + offsetX, offsetY, z + offsetZ] = BlockList["Leaves"];
                                        }
                                    }
                                }
                            }
                        }
                    }

                }
            }
        }

        return VoxelData;
    }


    bool HasWoodNeighbor(byte[,,] voxelData, int x, int y, int z, int radius)
    {
        // Check neighboring blocks within a 3x3x3 cube
        for (int offsetX = -radius; offsetX <= radius; offsetX++)
        {
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetZ = -radius; offsetZ <= radius; offsetZ++)
                {
                    int neighborX = x + offsetX;
                    int neighborY = y + offsetY;
                    int neighborZ = z + offsetZ;

                    // Ensure neighbor is within bounds
                    if (neighborX >= 0 && neighborX < ChunkSize &&
                        neighborY >= 0 && neighborY < ChunkSize &&
                        neighborZ >= 0 && neighborZ < ChunkSize)
                    {
                        // Check if the neighbor is a wood block
                        if (voxelData[neighborX, neighborY, neighborZ] == 7) // Assuming 7 is the ID for wood blocks
                        {
                            return true; // Found a wood neighbor
                        }
                    }
                }
            }
        }
        return false; // No wood neighbors found
    }



    public float Perlin3D(float x, float y, float z, float scale)
    {
        x *= scale;
        y *= scale;
        z *= scale;
        float xy = Mathf.PerlinNoise(x, y);
        float xz = Mathf.PerlinNoise(x, z);
        float yz = Mathf.PerlinNoise(y, z);
        float yx = Mathf.PerlinNoise(y, x);
        float zx = Mathf.PerlinNoise(z, x);
        float zy = Mathf.PerlinNoise(z, y);

        return (xy + xz + yz + yx + zx + zy) / 6;

    }











    public void SetVoxelAtWorldPosition(Vector3 worldPosition, byte voxelValue, bool regenerate, bool useCache)
    {
        Vector3Int chunkPosition = new Vector3Int(
            Mathf.FloorToInt(worldPosition.x / ChunkSize),
            Mathf.FloorToInt(worldPosition.y / ChunkSize),
            Mathf.FloorToInt(worldPosition.z / ChunkSize)
        );


        if (activeChunks.ContainsKey(chunkPosition))
        {
            Chunk chunk = activeChunks[chunkPosition];
            Vector3 localPosition = chunk.transform.InverseTransformPoint(worldPosition);


            int voxelX = Mathf.FloorToInt(localPosition.x);
            int voxelY = Mathf.FloorToInt(localPosition.y);
            int voxelZ = Mathf.FloorToInt(localPosition.z);

            chunk.SetVoxel(voxelX, voxelY, voxelZ, voxelValue);

            if (regenerate)
            {
                chunk.GenerateTerrain();
            }
            if (useCache)
            {
                if (!chunkCache.ContainsKey(chunkPosition))
                {
                    chunkCache[chunkPosition] = chunk.GetData();
                }
                else
                {
                    chunkCache[chunkPosition] = chunk.GetData();
                }
            }
        }
        else
        {
            Debug.LogWarning("Chunk at position " + chunkPosition + " is not active.");
        }
    }

    public byte GetVoxelPosition(Vector3 worldPosition)
    {
        Vector3Int chunkPosition = new Vector3Int(
            Mathf.FloorToInt(worldPosition.x / ChunkSize),
            Mathf.FloorToInt(worldPosition.y / ChunkSize),
            Mathf.FloorToInt(worldPosition.z / ChunkSize)
        );


        if (activeChunks.ContainsKey(chunkPosition))
        {
            Chunk chunk = activeChunks[chunkPosition];
            Vector3 localPosition = chunk.transform.InverseTransformPoint(worldPosition);


            int voxelX = Mathf.FloorToInt(localPosition.x);
            int voxelY = Mathf.FloorToInt(localPosition.y);
            int voxelZ = Mathf.FloorToInt(localPosition.z);

            return chunk.GetData()[voxelX, voxelY, voxelZ];
        }
        return 0;
    }


    public Chunk GetChunk(Vector3 worldPosition)
    {
        Vector3Int chunkPosition = new Vector3Int(
            Mathf.FloorToInt(worldPosition.x / ChunkSize),
            Mathf.FloorToInt(worldPosition.y / ChunkSize),
            Mathf.FloorToInt(worldPosition.z / ChunkSize)
        );


        if (activeChunks.ContainsKey(chunkPosition))
        {
            Chunk chunk = activeChunks[chunkPosition];
            return chunk;
        }
        return null;
    }





    public Chunk GetNeighboringChunk(Vector3Int currentChunkPosition, int dx, int dy, int dz)
    {
        Vector3Int neighboringChunkPosition = currentChunkPosition + new Vector3Int(dx, dy, dz);

        try
        {
            return activeChunks[neighboringChunkPosition];
        }
        catch
        {
            return null;
        }
    }


    void UpdateChunks()
    {

        var chunkKeys = new List<Vector3Int>(activeChunksObj.Keys);


        var chunksToRemove = new ConcurrentBag<Vector3Int>();

        Vector3 TargetPos = target.position;

        // Use Parallel.For to iterate over the chunks
        Parallel.For(0, chunkKeys.Count, i =>
        {
            Vector3Int chunkPosition = chunkKeys[i];
            GameObject chunkObj = activeChunksObj[chunkPosition];
            float distance = Vector3.Distance(chunkPosition * ChunkSize, TargetPos);

            if (distance > renderDistance * ChunkSize)
            {
                chunksToRemove.Add(chunkPosition);
            }
        });
        List<Vector3Int> chunksToRemoveList = new List<Vector3Int>(chunksToRemove);

        foreach (var chunkPosition in chunksToRemoveList)
        {
            Destroy(activeChunksObj[chunkPosition].gameObject);
            activeChunks.Remove(chunkPosition);
            activeChunksObj.Remove(chunkPosition);
        }
    }

}
