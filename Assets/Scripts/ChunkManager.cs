using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
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
    public int SkyIntensity = 15;
    public static ChunkManager Instance { get; private set; }
    public Dictionary<Vector3Int, Chunk> activeChunks = new Dictionary<Vector3Int, Chunk>();
    public Dictionary<Vector3Int, GameObject> activeChunksObj = new Dictionary<Vector3Int, GameObject>();
    private Coroutine generateChunksCoroutine;
    private Vector3 previousTargetPosition;
    private Dictionary<Vector3Int, byte[,,]> chunkCache = new Dictionary<Vector3Int, byte[,,]>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        previousTargetPosition = target.position;
        generateChunksCoroutine = StartCoroutine(GenerateChunks());
    }

    void Update()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        UpdateChunks();


        if (Vector3.Distance(target.position, previousTargetPosition) > ChunkSize)
        {
            if (generateChunksCoroutine != null)
            {
                StopCoroutine(generateChunksCoroutine);
            }

            generateChunksCoroutine = StartCoroutine(GenerateChunks());


            previousTargetPosition = target.position;
        }
    }

    IEnumerator GenerateChunks()
    {

        List<Vector3Int> sortedChunkPositions = new List<Vector3Int>();

        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int y = -renderDistance; y <= renderDistance; y++)
            {
                for (int z = -renderDistance; z <= renderDistance; z++)
                {
                    Vector3Int chunkPosition = new Vector3Int(
                        Mathf.FloorToInt(target.position.x / ChunkSize) + x,
                        Mathf.FloorToInt(target.position.y / ChunkSize) + y,
                        Mathf.FloorToInt(target.position.z / ChunkSize) + z
                    );

                    float distance = Vector3.Distance(target.position, chunkPosition * ChunkSize);

                    if (distance < renderDistance * ChunkSize)
                    {
                        sortedChunkPositions.Add(chunkPosition);
                    }
                }
            }
        }
        sortedChunkPositions.Sort((a, b) =>
        {
            float distanceA = Vector3.Distance(target.position, a * ChunkSize);
            float distanceB = Vector3.Distance(target.position, b * ChunkSize);
            return distanceA.CompareTo(distanceB);
        });

        foreach (Vector3Int chunkPosition in sortedChunkPositions)
        {
            if (!activeChunks.ContainsKey(chunkPosition))
            {
                GameObject newChunk = new GameObject();
                newChunk.name = chunkPosition.x + "_" + chunkPosition.y + "_" + chunkPosition.z;
                newChunk.transform.position = chunkPosition * ChunkSize;

                if (ChunkBorders)
                {
                    GameObject Border = GameObject.Instantiate(ChunkBorderPrefab, newChunk.transform);
                    Border.transform.localScale = Vector3.one * ChunkSize;
                }

                Chunk chunk = newChunk.AddComponent<Chunk>();
                chunk.Init(mat, transparent, TransparentBlocks.ToArray(), NoCollisonBlocks.ToArray(), ChunkSize, TextureSize, BlockSize, chunkPosition);


                if (!chunkCache.ContainsKey(chunkPosition))
                {
                    chunk.SetData(SetTerrain(chunk));
                }
                else
                {
                    chunk.SetData(chunkCache[chunkPosition]);
                }
                chunk.GenerateTerrain();
                activeChunks.Add(chunkPosition, chunk);
                activeChunksObj.Add(chunkPosition, newChunk);
            }
            yield return null;
        }
    }



    byte[,,] SetTerrain(Chunk chunk)
    {
        byte[,,] VoxelData = new byte[ChunkSize, ChunkSize, ChunkSize];
        Vector3 chunkCornerWorldPos = chunk.transform.position;

        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    Vector3 voxelPosition = chunkCornerWorldPos + new Vector3(x, y, z);

                    byte voxelValue = 0;

                    float waterLevel = 10;
                    float height = 25;
                    float scale = 0.01f; 
                    float perlinValue = Mathf.PerlinNoise(voxelPosition.x * scale, voxelPosition.z * scale) * height;

                    int ycoord = (byte)(Mathf.RoundToInt(perlinValue));


                    if (voxelPosition.y > ycoord && voxelPosition.y <= waterLevel)
                    {
                        voxelValue = 16;
                    }
                    else
                    {
                        if (voxelPosition.y == ycoord)
                        {
                            if(voxelPosition.y == waterLevel)
                            {
                                voxelValue = 11;
                            }
                            else
                            {
                                if(voxelPosition.y < waterLevel)
                                {
                                    voxelValue = 11;
                                }
                                else
                                {
                                    voxelValue = 1; //grass
                                }
                            }
                        }
                        else
                        {
                            if (voxelPosition.y < ycoord)
                            {
                                voxelValue = 2; //dirt
                            }
                            if (voxelPosition.y < ycoord - 4)
                            {
                                voxelValue = 3; //stone
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
                    if (VoxelData[x, y, z] == 1) // check for grass
                    {
                        if (y + 10 < ChunkSize && x + 2 < ChunkSize && z + 2 < ChunkSize
                            && x - 2 > 0 && z - 2 > 0 
                            && Random.value > 0.95)
                        {
                            for (int i = y + 1; i < y + 4; i++)
                            {
                                VoxelData[x, i, z] = 7; // add trunk
                            }
                            // Add leaves on top
                            for (int offsetY = y + 4; offsetY < y + 8; offsetY++) // adjust the height range as needed
                            {
                                for (int offsetX = -2; offsetX <= 2; offsetX++)
                                {
                                    for (int offsetZ = -2; offsetZ <= 2; offsetZ++)
                                    {
                                        VoxelData[x + offsetX, offsetY, z + offsetZ] = 8; // assuming 8 is the ID for leaves
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

    public static float Perlin3D(float x, float y, float z, float scale)
    {
        float XY = Mathf.PerlinNoise(x * scale, y * scale);
        float YZ = Mathf.PerlinNoise(y * scale, z * scale);
        float XZ = Mathf.PerlinNoise(x * scale, z * scale);

        float YX = Mathf.PerlinNoise(y * scale, x * scale);
        float ZY = Mathf.PerlinNoise(z * scale, y * scale);
        float ZX = Mathf.PerlinNoise(z * scale, x * scale);

        // Sum up the 2D Perlin noise values and normalize
        float val = (XY + YZ + XZ + YX + ZY + ZX) / 6f;
        return val;
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
        else
        {
            Debug.LogWarning("Chunk at position " + chunkPosition + " is not active.");
        }
        return 0;
    }


    public void SetLightAtWorldPosition(Vector3 worldPosition, int channel, byte light)
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

            chunk.SetLight(voxelX, voxelY, voxelZ, channel, light);
        }
        else
        {
            Debug.LogWarning("Chunk at position " + chunkPosition + " is not active.");
        }
    }

    public (byte, byte, byte) GetLightAtWorldPosition(Vector3 worldPosition)
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


            byte R = chunk.GetLightDataR()[voxelX, voxelY, voxelZ];
            byte G = chunk.GetLightDataG()[voxelX, voxelY, voxelZ];
            byte B = chunk.GetLightDataB()[voxelX, voxelY, voxelZ];
            return (R, G, B);   
        }
        else
        {
            Debug.LogWarning("Chunk at position " + chunkPosition + " is not active.");
        }
        return (0, 0, 0);
    }





    public Chunk GetNeighboringChunk(Vector3Int currentChunkPosition, int dx, int dy, int dz)
    {
        Vector3Int neighboringChunkPosition = currentChunkPosition + new Vector3Int(dx, dy, dz);

        if (activeChunks.ContainsKey(neighboringChunkPosition))
        {
            Chunk neighboringChunk = activeChunks[neighboringChunkPosition];
            return neighboringChunk;
        }

        return null;
    }


    void UpdateChunks()
    {
        List<Vector3Int> chunksToRemove = new List<Vector3Int>();

        foreach (var chunk in activeChunksObj)
        {
            Vector3Int chunkPosition = chunk.Key;
            GameObject chunkObj = chunk.Value;
            float distance = Vector3.Distance(chunkObj.transform.position, target.position);


            if (distance > renderDistance * ChunkSize)
            {
                Destroy(chunk.Value);
                chunksToRemove.Add(chunkPosition);
            }
        }

        foreach (var chunkPosition in chunksToRemove)
        {
            activeChunks.Remove(chunkPosition);
            activeChunksObj.Remove(chunkPosition);
        }
    }
}
