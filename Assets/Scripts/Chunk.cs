using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;


public class Chunk : MonoBehaviour
{

    private int ChunkSize = 0;
    private byte[,,] VoxelData;
    private byte[,,] VoxelDataDirection;
    private byte[] TransparentIDs;
    private byte[] NoCollisionIDs;
    private byte[] NoCollisionTransparentIDs;
    private MeshFilter _MeshFilter;
    private MeshRenderer _MeshRenderer;
    private Mesh _Mesh;
    private MeshFilter _MeshFilterTransparent;
    private MeshRenderer _MeshRendererTransparent;
    private Mesh _MeshTransparent;
    private MeshFilter _MeshFilterNoCollision;
    private MeshRenderer _MeshRendererNoCollision;
    private Mesh _MeshNoCollision;
    private MeshFilter _MeshFilterNoCollisionTransparent;
    private MeshRenderer _MeshRendererNoCollisionTransparent;
    private Mesh _MeshNoCollisionTransparent;
    private Material _Material;
    private float TextureSize = 256;
    private float BlockSize = 16;
    public Vector3Int currentPos;
    private Transform Main;
    private Transform TransparentObj;
    private Transform NoCollisionObj;
    private Transform NoCollisionObjTransparent;
    private Dictionary<string, byte> BlockList = new Dictionary<string, byte>();
    private Dictionary<byte, Color> BlockListLight = new Dictionary<byte, Color>();
    private Dictionary<byte, int[]> BlockFaces = new Dictionary<byte, int[]>();
    bool isDestroyed = false;

    void Start()
    {
    }
    private void OnDestroy()
    {
        isDestroyed = true;
    }


    public void Init(Material mat, Material transparentMat, byte[] transparent, byte[] nocollision, int _ChunkSize, float _TextureSize, float _BlockSize, Vector3Int Position)
    {
        BlockList = ChunkManager.Instance.BlockList;
        BlockListLight = ChunkManager.Instance.BlockListLight;
        BlockFaces = ChunkManager.Instance.BlockFaces;
        currentPos = Position;
        VoxelData = new byte[_ChunkSize, _ChunkSize, _ChunkSize];
        VoxelDataDirection = new byte[_ChunkSize, _ChunkSize, _ChunkSize];

        TextureSize = _TextureSize;
        BlockSize = _BlockSize;
        ChunkSize = _ChunkSize;
        TransparentIDs = transparent;
        NoCollisionTransparentIDs = transparent.Concat(nocollision).ToArray();

        NoCollisionIDs = nocollision;

        Main = new GameObject("Main").transform;
        Main.transform.parent = transform;
        Main.transform.localPosition = Vector3.zero;
        _MeshFilter = Main.transform.AddComponent<MeshFilter>();
        _MeshRenderer = Main.transform.AddComponent<MeshRenderer>();
        _MeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        _MeshRenderer.sharedMaterial = mat;
        _Mesh = new Mesh();
        _MeshFilter.mesh = _Mesh;

        TransparentObj = new GameObject("Transparent").transform;
        TransparentObj.transform.parent = transform;
        TransparentObj.transform.localPosition = Vector3.zero;
        _MeshFilterTransparent = TransparentObj.transform.AddComponent<MeshFilter>();
        _MeshRendererTransparent = TransparentObj.transform.AddComponent<MeshRenderer>();
        _MeshRendererTransparent.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        _MeshRendererTransparent.sharedMaterial = transparentMat;
        _MeshTransparent = new Mesh();
        _MeshFilterTransparent.mesh = _MeshTransparent;

        NoCollisionObj = new GameObject("NoCollision").transform;
        NoCollisionObj.gameObject.layer = ChunkManager.Instance.IgnoreLayer;
        NoCollisionObj.transform.parent = transform;
        NoCollisionObj.transform.localPosition = Vector3.zero;
        _MeshFilterNoCollision = NoCollisionObj.transform.AddComponent<MeshFilter>();
        _MeshRendererNoCollision = NoCollisionObj.transform.AddComponent<MeshRenderer>();
        _MeshRendererNoCollision.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        _MeshRendererNoCollision.sharedMaterial = mat;
        _MeshNoCollision = new Mesh();
        _MeshFilterNoCollision.mesh = _MeshNoCollision;

        NoCollisionObjTransparent = new GameObject("NoCollisionTransparent").transform;
        NoCollisionObjTransparent.gameObject.layer = ChunkManager.Instance.IgnoreLayer;
        NoCollisionObjTransparent.transform.parent = transform;
        NoCollisionObjTransparent.transform.localPosition = Vector3.zero;
        _MeshFilterNoCollisionTransparent = NoCollisionObjTransparent.transform.AddComponent<MeshFilter>();
        _MeshRendererNoCollisionTransparent = NoCollisionObjTransparent.transform.AddComponent<MeshRenderer>();
        _MeshRendererNoCollisionTransparent.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        _MeshRendererNoCollisionTransparent.sharedMaterial = transparentMat;
        _MeshNoCollisionTransparent = new Mesh();
        _MeshFilterNoCollisionTransparent.mesh = _MeshNoCollisionTransparent;

    }



    public void GenerateTerrain()
    {
        GenerateMesh(true);
    }



    public byte[,,] GetData()
    {
        return VoxelData;
    }
    public byte[,,] SetData(byte[,,] Data)
    {
        VoxelData = Data;
        return VoxelData;
    }




    public byte[,,] SetVoxel(int x, int y, int z, byte value)
    {
        if (IsValidPosition(x, y, z))
        {
            VoxelData[x, y, z] = value;
        }
        else
        {
            Debug.LogError("Invalid voxel position");
        }
        return VoxelData;
    }
    public byte[,,] SetDirection(int x, int y, int z, byte value)
    {
        if (IsValidPosition(x, y, z))
        {
            VoxelDataDirection[x, y, z] = value;
        }
        else
        {
            Debug.LogError("Invalid voxel position");
        }
        return VoxelDataDirection;
    }

    public byte[,,] SetDirectionData(byte[,,] data)
    {
        VoxelDataDirection = data;
        return data;
    }

    public byte[,,] GetDirectionData()
    {
        return VoxelDataDirection;
    }


    private bool IsValidPosition(int x, int y, int z)
    {
        return x >= 0 && x < ChunkSize && y >= 0 && y < ChunkSize && z >= 0 && z < ChunkSize;
    }

    public void BlockChunkUpdate()
    {
        Vector3 chunkCornerWorldPos = transform.position;

        // Forward pass
        Parallel.For(0, ChunkSize, x =>
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    Vector3 voxelPosition = chunkCornerWorldPos + new Vector3(x, y, z);

                    // Check if current voxel is below water level and air
                    if (voxelPosition.y <= ChunkManager.Instance.waterLevel && voxelPosition.y > ChunkManager.Instance.worldFloor && VoxelData[x, y, z] == BlockList["Air"])
                    {
                        // Check if any neighboring voxel is water
                        bool hasWaterNeighbor = CheckForWaterNeighbor(x, y, z);

                        // Fill with water only if there's a water neighbor
                        if (hasWaterNeighbor)
                        {
                            VoxelData[x, y, z] = BlockList["Water"];
                        }
                    }
                }
            }
        });

        // Reverse pass
        Parallel.For(0, ChunkSize, i =>
        {
            int x = ChunkSize - 1 - i;
            for (int y = ChunkSize - 1; y >= 0; y--)
            {
                for (int z = ChunkSize - 1; z >= 0; z--)
                {
                    Vector3 voxelPosition = chunkCornerWorldPos + new Vector3(x, y, z);

                    // Check if current voxel is below water level and air
                    if (voxelPosition.y <= ChunkManager.Instance.waterLevel && voxelPosition.y > ChunkManager.Instance.worldFloor && VoxelData[x, y, z] == BlockList["Air"])
                    {
                        // Check if any neighboring voxel is water
                        bool hasWaterNeighbor = CheckForWaterNeighbor(x, y, z);

                        // Fill with water only if there's a water neighbor
                        if (hasWaterNeighbor)
                        {
                            VoxelData[x, y, z] = BlockList["Water"];
                        }
                    }
                }
            }
        });
    }


    private bool CheckForWaterNeighbor(int x, int y, int z)
    {
        Vector3Int[] directions = new Vector3Int[]
        {
                    new Vector3Int(-1, 0, 0),   // Left
                    new Vector3Int(1, 0, 0),    // Right
                    new Vector3Int(0, -1, 0),   // Bottom
                    new Vector3Int(0, 1, 0),    // Top
                    new Vector3Int(0, 0, -1),   // Back
                    new Vector3Int(0, 0, 1)     // Front
        };

        bool found = false;
        Parallel.For(0, directions.Length, i =>
        {
            Vector3Int direction = directions[i];


            int neighborX = x + direction.x;
            int neighborY = y + direction.y;
            int neighborZ = z + direction.z;
            if (neighborX < 0 || neighborX >= ChunkSize ||
                neighborY < 0 || neighborY >= ChunkSize ||
                neighborZ < 0 || neighborZ >= ChunkSize)
            {
                int neighborChunkX = (x + direction.x + ChunkSize) % ChunkSize;
                int neighborChunkY = (y + direction.y + ChunkSize) % ChunkSize;
                int neighborChunkZ = (z + direction.z + ChunkSize) % ChunkSize;

                Chunk chunk = ChunkManager.Instance.GetNeighboringChunk(currentPos, direction.x, direction.y, direction.z);
                if (chunk)
                {
                    if (chunk.GetData()[neighborChunkX, neighborChunkY, neighborChunkZ] == BlockList["Water"])
                    {
                        found = true;
                    }
                }
            }
            else
            {
                if (VoxelData[neighborX, neighborY, neighborZ] == BlockList["Water"])
                {
                    found = true;
                }
            }
        });

        return found;
    }



    private bool isGeneratingMesh = false;




    public async void GenerateMesh(bool updateNeighbors)
    {
        if (isGeneratingMesh || isDestroyed) return;

        isGeneratingMesh = true;
        CreateLights();
        BlockChunkUpdate();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();


        List<Vector3> verticesTransparent = new List<Vector3>();
        List<int> trianglesTransparent = new List<int>();
        List<Vector2> uvsTransparent = new List<Vector2>();

        List<Vector3> verticesNoCollision = new List<Vector3>();
        List<int> trianglesNoCollision = new List<int>();
        List<Vector2> uvsNoCollision = new List<Vector2>();

        List<Vector3> verticesNoCollisionTransparent = new List<Vector3>();
        List<int> trianglesNoCollisionTransparent = new List<int>();
        List<Vector2> uvsNoCollisionTransparent = new List<Vector2>();


        Vector3 ChunkPosition = transform.position;

        await Task.Run(async () =>
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        if (isDestroyed)
                        {
                            break;
                        }
                        if (VoxelData[x, y, z] == BlockList["Air"])
                            continue;


                        // Define the six directions as offsets from the current voxel position
                        Vector3Int[] directions = new Vector3Int[]
                        {
                    new Vector3Int(-1, 0, 0),   // Left
                    new Vector3Int(1, 0, 0),    // Right
                    new Vector3Int(0, -1, 0),   // Bottom
                    new Vector3Int(0, 1, 0),    // Top
                    new Vector3Int(0, 0, -1),   // Back
                    new Vector3Int(0, 0, 1)     // Front
                        };
                        bool[] directionBool = new bool[directions.Length];

                        for (int i = 0; i < directions.Length; i++)
                        {
                            directionBool[0] = false;
                            directionBool[1] = false;
                            directionBool[2] = false;
                            directionBool[3] = false;
                            directionBool[4] = false;
                            directionBool[5] = false;




                            Vector3Int direction = directions[i];
                            int dx = direction.x;
                            int dy = direction.y;
                            int dz = direction.z;

                            if (IsFaceVisible(x, y, z, dx, dy, dz))
                            {
                                bool isTransparent = TransparentIDs.Contains(VoxelData[x, y, z]);
                                bool hasNoCollision = NoCollisionIDs.Contains(VoxelData[x, y, z]);
                                bool isMain = !isTransparent && !hasNoCollision;
                                bool isTransparentNoCollision = isTransparent && hasNoCollision;
                                bool Inverted = false;

                                Vector3[] quadVertices = new Vector3[4];
                                bool inverted = false;


                                byte BlockDirection = VoxelDataDirection[x,y,z];
                                const byte DEFAULT = 0;
                                const byte NORTH = 1;
                                const byte EAST = 2;
                                const byte SOUTH = 3;
                                const byte WEST = 4;

                                void SetBlockDirection(int face, int BlockDirectionVal)
                                {
                                    if (BlockDirectionVal == DEFAULT || BlockDirectionVal == NORTH) //default/north texture rotation for block
                                    {
                                        if (face == 0) //left face
                                        {
                                            directionBool[2] = true;
                                        }
                                        if (face == 1) //right face
                                        {
                                            directionBool[3] = true;
                                        }
                                        if (face == 2) //bottom face
                                        {
                                            directionBool[5] = true;
                                        }
                                        if (face == 3) //top face
                                        {
                                            directionBool[4] = true;
                                        }
                                        if (face == 4) //back face
                                        {
                                            directionBool[1] = true;
                                        }
                                        if (face == 5) //front face
                                        {
                                            directionBool[0] = true;
                                        }
                                    }
                                    else if (BlockDirectionVal == EAST)
                                    {
                                        if (face == 0) //left face
                                        {
                                            directionBool[1] = true;
                                        }
                                        if (face == 1) //right face
                                        {
                                            directionBool[0] = true;
                                        }
                                        if (face == 2) //bottom face
                                        {
                                            directionBool[5] = true;
                                        }
                                        if (face == 3) //top face
                                        {
                                            directionBool[4] = true;
                                        }
                                        if (face == 4) //back face
                                        {
                                            directionBool[3] = true;
                                        }
                                        if (face == 5) //front face
                                        {
                                            directionBool[2] = true;
                                        }
                                    }
                                    else if (BlockDirectionVal == SOUTH)
                                    {
                                        if (face == 0) //left face
                                        {
                                            directionBool[3] = true;
                                        }
                                        if (face == 1) //right face
                                        {
                                            directionBool[2] = true;
                                        }
                                        if (face == 2) //bottom face
                                        {
                                            directionBool[5] = true;
                                        }
                                        if (face == 3) //top face
                                        {
                                            directionBool[4] = true;
                                        }
                                        if (face == 4) //back face
                                        {
                                            directionBool[0] = true;
                                        }
                                        if (face == 5) //front face
                                        {
                                            directionBool[1] = true;
                                        }
                                    }
                                    else if (BlockDirectionVal == WEST)
                                    {
                                        if (face == 0) //left face
                                        {
                                            directionBool[0] = true;
                                        }
                                        if (face == 1) //right face
                                        {
                                            directionBool[1] = true;
                                        }
                                        if (face == 2) //bottom face
                                        {
                                            directionBool[5] = true;
                                        }
                                        if (face == 3) //top face
                                        {
                                            directionBool[4] = true;
                                        }
                                        if (face == 4) //back face
                                        {
                                            directionBool[2] = true;
                                        }
                                        if (face == 5) //front face
                                        {
                                            directionBool[3] = true;
                                        }
                                    }
                                }

                                if (dx == -1) // Left face
                                {
                                    SetBlockDirection(0, BlockDirection);
                                    quadVertices[0] = new Vector3(x, y, z);
                                    quadVertices[1] = new Vector3(x, y, z + 1);
                                    quadVertices[2] = new Vector3(x, y + 1, z);
                                    quadVertices[3] = new Vector3(x, y + 1, z + 1);
                                    inverted = true;
                                }
                                else if (dx == 1) // Right face
                                {
                                    SetBlockDirection(1, BlockDirection);
                                    quadVertices[0] = new Vector3(x + 1, y, z);
                                    quadVertices[1] = new Vector3(x + 1, y, z + 1);
                                    quadVertices[2] = new Vector3(x + 1, y + 1, z);
                                    quadVertices[3] = new Vector3(x + 1, y + 1, z + 1);
                                }
                                else if (dy == -1) // Bottom face
                                {
                                    SetBlockDirection(2, BlockDirection);
                                    quadVertices[0] = new Vector3(x, y, z);
                                    quadVertices[1] = new Vector3(x, y, z + 1);
                                    quadVertices[2] = new Vector3(x + 1, y, z);
                                    quadVertices[3] = new Vector3(x + 1, y, z + 1);
                                }
                                else if (dy == 1) // Top face
                                {
                                    SetBlockDirection(3, BlockDirection);
                                    quadVertices[0] = new Vector3(x, y + 1, z);
                                    quadVertices[1] = new Vector3(x, y + 1, z + 1);
                                    quadVertices[2] = new Vector3(x + 1, y + 1, z);
                                    quadVertices[3] = new Vector3(x + 1, y + 1, z + 1);
                                    inverted = true;
                                }
                                else if (dz == -1) // Back face
                                {
                                    SetBlockDirection(4, BlockDirection);
                                    quadVertices[0] = new Vector3(x, y, z);
                                    quadVertices[1] = new Vector3(x + 1, y, z);
                                    quadVertices[2] = new Vector3(x, y + 1, z);
                                    quadVertices[3] = new Vector3(x + 1, y + 1, z);
                                }
                                else if (dz == 1) // Front face
                                {
                                    SetBlockDirection(5, BlockDirection);
                                    quadVertices[0] = new Vector3(x, y, z + 1);
                                    quadVertices[1] = new Vector3(x + 1, y, z + 1);
                                    quadVertices[2] = new Vector3(x, y + 1, z + 1);
                                    quadVertices[3] = new Vector3(x + 1, y + 1, z + 1);
                                    inverted = true;
                                }
                                if (inverted)
                                {
                                    // Swap all vertices
                                    Vector3 temp = quadVertices[0];
                                    quadVertices[0] = quadVertices[1];
                                    quadVertices[1] = temp;

                                    temp = quadVertices[2];
                                    quadVertices[2] = quadVertices[3];
                                    quadVertices[3] = temp;
                                }




                                if (isMain)
                                {
                                    AddQuad(vertices, triangles, uvs, quadVertices[0], quadVertices[1], quadVertices[2], quadVertices[3], VoxelData[x, y, z], Inverted, directionBool);
                                }
                                else if (isTransparentNoCollision)
                                {
                                    AddQuad(verticesNoCollisionTransparent, trianglesNoCollisionTransparent, uvsNoCollisionTransparent, quadVertices[0], quadVertices[1], quadVertices[2], quadVertices[3],
                                           VoxelData[x, y, z], Inverted, directionBool);
                                }
                                else if (isTransparent)
                                {
                                    AddQuad(verticesTransparent, trianglesTransparent, uvsTransparent,
                                           quadVertices[0], quadVertices[1], quadVertices[2], quadVertices[3], VoxelData[x, y, z], Inverted, directionBool);
                                }
                                else if (hasNoCollision)
                                {
                                    AddQuad(verticesNoCollision, trianglesNoCollision, uvsNoCollision, quadVertices[0], quadVertices[1], quadVertices[2], quadVertices[3], VoxelData[x, y, z], Inverted, directionBool);
                                }
                            }
                        }
                    }
                }
            }
            await Task.CompletedTask;
        });



        if (isDestroyed || !isGeneratingMesh) return;

        if (Main)
        {
            UpdateMesh(_Mesh, vertices, triangles, uvs, Main.gameObject);
        }

        if (TransparentObj)
        {
            UpdateMesh(_MeshTransparent, verticesTransparent, trianglesTransparent, uvsTransparent, TransparentObj.gameObject);
        }

        if (NoCollisionObj)
        {
            UpdateMesh(_MeshNoCollision, verticesNoCollision, trianglesNoCollision, uvsNoCollision, NoCollisionObj.gameObject);
        }

        if (NoCollisionObjTransparent)
        {
            UpdateMesh(_MeshNoCollisionTransparent, verticesNoCollisionTransparent, trianglesNoCollisionTransparent, uvsNoCollisionTransparent, NoCollisionObjTransparent.gameObject);
        }
        if (updateNeighbors)
        {
            UpdateNeighborChunks();
        }

        isGeneratingMesh = false;
    }


    void UpdateMesh(Mesh mesh, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, GameObject obj)
    {
        MeshCollider meshCollider = obj.GetComponent<MeshCollider>();


        var verticesArray = vertices.ToArray();
        var trianglesArray = triangles.ToArray();
        var uvsArray = uvs.ToArray();

        mesh.Clear();
        mesh.vertices = verticesArray;
        mesh.triangles = trianglesArray;
        mesh.uv = uvsArray;
        mesh.RecalculateNormals();

        if (vertices.Count == 0 || triangles.Count == 0)
        {
            if (meshCollider)
            {
                Destroy(meshCollider);
                meshCollider = null;
            }
            return;
        }
        if (!meshCollider)
        {
            meshCollider = obj.AddComponent<MeshCollider>();
        }
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = true;
        meshCollider.convex = false; // You only need to set convex once
    }


    private GameObject LightParent;
    async void CreateLights()
    {
        if (LightParent == null)
        {
            LightParent = new GameObject("Lights");
            LightParent.transform.parent = transform;
        }

        List<(Vector3, Color)> LightPositions = new List<(Vector3, Color)>();
        Vector3 chunkCornerWorldPos = transform.position;
        await Task.Run(() =>
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        if (isDestroyed)
                        {
                            break;
                        }
                        if (VoxelData[x, y, z] == BlockList["Air"])
                            continue;

                        Vector3 voxelPosition = chunkCornerWorldPos + new Vector3(x, y, z);

                        Color LightData = BlockListLight[VoxelData[x, y, z]];
                        if (LightData.r != 0 || LightData.g != 0 || LightData.b != 0)
                        {
                            LightPositions.Add((voxelPosition + new Vector3(0.5f, 0.5f, 0.5f), LightData));
                        }
                    }
                }
            }
        });

        if (isDestroyed)
        {
            return;
        }

        // Collect existing light positions
        HashSet<Vector3> existingLightPositions = new HashSet<Vector3>();
        foreach (Transform child in LightParent.transform)
        {
            existingLightPositions.Add(child.position);
        }

        // Create new lights if they do not already exist
        HashSet<Vector3> newLightPositions = new HashSet<Vector3>();
        foreach ((Vector3, Color) LightPos in LightPositions)
        {
            if (!existingLightPositions.Contains(LightPos.Item1))
            {
                GameObject LightObj = new GameObject(LightPos.Item1.ToString());
                LightObj.transform.position = LightPos.Item1;
                LightObj.transform.parent = LightParent.transform;
                Light LightComponent = LightObj.AddComponent<Light>();
                LightComponent.type = LightType.Point;
                LightComponent.color = LightPos.Item2;
                LightComponent.intensity = 1 / Mathf.Clamp(((LightPos.Item2.r + LightPos.Item2.g + LightPos.Item2.b) / 3), 1, Mathf.Infinity);
                LightComponent.range = 50;
            }
            newLightPositions.Add(LightPos.Item1);
        }

        // Destroy lights that are not in the current LightPositions
        foreach (Transform child in LightParent.transform)
        {
            if (!newLightPositions.Contains(child.position))
            {
                Destroy(child.gameObject);
            }
        }
    }


    private bool ReturnType(byte id, byte[] idlist)
    {
        for (int i = 0; i < idlist.Length; i++)
        {
            if (id == idlist[i])
            {
                return true;
            }
        }

        return false;
    }


    private bool IsFaceVisible(int x, int y, int z, int dx, int dy, int dz)
    {

        if (x + dx >= 0 && x + dx < ChunkSize &&
            y + dy >= 0 && y + dy < ChunkSize &&
            z + dz >= 0 && z + dz < ChunkSize)
        {
            byte NeighbourBlockID = VoxelData[x + dx, y + dy, z + dz];


            bool isTransparent = ReturnType(VoxelData[x, y, z], TransparentIDs);
            bool isNoCollision = ReturnType(VoxelData[x, y, z], NoCollisionIDs);
            bool isNoCollisionTransparent = ReturnType(VoxelData[x, y, z], NoCollisionTransparentIDs);


            bool NeighborisTransparent = ReturnType(NeighbourBlockID, TransparentIDs);
            bool NeighborisNoCollision = ReturnType(NeighbourBlockID, NoCollisionIDs);
            bool NeighborisNoCollisionTransparent = ReturnType(NeighbourBlockID, NoCollisionTransparentIDs);


            if (!isTransparent)
            {
                if (NeighbourBlockID == BlockList["Air"] || NeighborisTransparent)
                {
                    return true;
                }
            }
            else
            {

                bool isNeighborTransparentBlockSameType = false;
                if (NeighbourBlockID == VoxelData[x, y, z])
                {
                    isNeighborTransparentBlockSameType = true;
                }



                if ((!isNeighborTransparentBlockSameType && NeighborisTransparent) || NeighbourBlockID == BlockList["Air"])
                {
                    return true;
                }
            }
        }
        else
        {
            Chunk neighboringChunk = ChunkManager.Instance.GetNeighboringChunk(currentPos, dx, dy, dz);
            if (neighboringChunk != null)
            {
                int neighborX = (x + dx + ChunkSize) % ChunkSize;
                int neighborY = (y + dy + ChunkSize) % ChunkSize;
                int neighborZ = (z + dz + ChunkSize) % ChunkSize;

                byte[,,] VoxelDataNeighbor = neighboringChunk.GetData();
                if (VoxelDataNeighbor.Length > 0)
                {
                    byte NeighbourBlockID = VoxelDataNeighbor[neighborX, neighborY, neighborZ];

                    bool isTransparent = ReturnType(VoxelData[x, y, z], TransparentIDs);
                    bool isNoCollision = ReturnType(VoxelData[x, y, z], NoCollisionIDs);
                    bool isNoCollisionTransparent = ReturnType(VoxelData[x, y, z], NoCollisionTransparentIDs);


                    bool NeighborisTransparent = ReturnType(NeighbourBlockID, TransparentIDs);
                    bool NeighborisNoCollision = ReturnType(NeighbourBlockID, NoCollisionIDs);
                    bool NeighborisNoCollisionTransparent = ReturnType(NeighbourBlockID, NoCollisionTransparentIDs);


                    if (!isTransparent)
                    {
                        if (NeighbourBlockID == BlockList["Air"] || NeighborisTransparent)
                        {
                            return true;
                        }
                    }
                    else
                    {

                        bool isNeighborTransparentBlockSameType = false;
                        if (NeighbourBlockID == VoxelData[x, y, z])
                        {
                            isNeighborTransparentBlockSameType = true;
                        }



                        if ((!isNeighborTransparentBlockSameType && NeighborisTransparent) || NeighbourBlockID == BlockList["Air"])
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        return false;
    }

    private (Chunk[], bool[]) GetNeighboringChunks(bool useDiagonal)
    {
        List<Chunk> neighboringChunks = new List<Chunk>();
        List<bool> IsDiagonal = new List<bool>();

        Vector3Int[] directions =
        {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
        new Vector3Int(0, 0, -1),
        new Vector3Int(0, 0, 1),
    };

        Vector3Int[] diagonalDirections =
        {
        new Vector3Int(1, 1, 0),
        new Vector3Int(1, -1, 0),
        new Vector3Int(-1, 1, 0),
        new Vector3Int(-1, -1, 0),
        new Vector3Int(1, 0, 1),
        new Vector3Int(1, 0, -1),
        new Vector3Int(-1, 0, 1),
        new Vector3Int(-1, 0, -1),
        new Vector3Int(0, 1, 1),
        new Vector3Int(0, 1, -1),
        new Vector3Int(0, -1, 1),
        new Vector3Int(0, -1, -1),
        new Vector3Int(1, 1, 1),
        new Vector3Int(1, 1, -1),
        new Vector3Int(1, -1, 1),
        new Vector3Int(1, -1, -1),
        new Vector3Int(-1, 1, 1),
        new Vector3Int(-1, 1, -1),
        new Vector3Int(-1, -1, 1),
        new Vector3Int(-1, -1, -1),
    };

        if (useDiagonal)
        {
            // Combine the regular and diagonal directions
            directions = directions.Concat(diagonalDirections).ToArray();
        }

        ChunkManager chunkManager = ChunkManager.Instance;

        // Use Parallel.ForEach to process directions in parallel
        Parallel.ForEach(directions, direction =>
        {
            bool isDiagonal = diagonalDirections.Contains(direction);

            Chunk neighboringChunk = chunkManager.GetNeighboringChunk(currentPos, direction.x, direction.y, direction.z);
            if (neighboringChunk != null)
            {
                lock (neighboringChunks) // Ensure thread-safe access to neighboringChunks list
                {
                    neighboringChunks.Add(neighboringChunk);
                    IsDiagonal.Add(isDiagonal);
                }
            }
        });

        return (neighboringChunks.ToArray(), IsDiagonal.ToArray());
    }






    public void UpdateNeighborChunks()
    {
        (Chunk[], bool[]) chunksData = GetNeighboringChunks(true);
        Chunk[] chunks = chunksData.Item1;
        bool[] isDiagonal = chunksData.Item2;
        for (int i = 0; i < chunks.Length; i++)
        {
            if (!isDiagonal[i])
            {
                chunks[i].GenerateMesh(false);
            }
            else
            {
                continue;
            }
        }
    }



    private void AddQuad(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Vector3 bottomLeft, Vector3 bottomRight, Vector3 topLeft, Vector3 topRight, int blockId, bool inverted, bool[] direction)
    {
        Vector3 center = Vector3.zero;
        int vertIndex = vertices.Count;

        int textureID = 0;

        for (int i = 0; i < BlockFaces[(byte)(blockId)].Length; i++)
        {
            if (direction[i] == true)
            {
                int newTexID = BlockFaces[(byte)(blockId)][i];
                textureID = newTexID == 0 ? blockId - 1 : newTexID;
                break;
            }
        }



        // Add vertices
        vertices.Add(bottomLeft);
        vertices.Add(bottomRight);
        vertices.Add(topLeft);
        vertices.Add(topRight);

        // Add UVs
        float atlasSize = TextureSize;
        float blockSize = BlockSize;
        float blocksPerRow = atlasSize / blockSize;
        float row = Mathf.Floor(textureID / blocksPerRow);
        float col = textureID % blocksPerRow;
        float blockX = col * (blockSize / atlasSize);
        float blockY = row * (blockSize / atlasSize);
        float uvSize = 1.0f / blocksPerRow;

        Vector2[] quadUVs;
        if (!inverted)
        {
            quadUVs = new Vector2[]
            {
            new Vector2(blockX, blockY),
            new Vector2(blockX + uvSize, blockY),
            new Vector2(blockX, blockY + uvSize),
            new Vector2(blockX + uvSize, blockY + uvSize)
            };
        }
        else
        {
            quadUVs = new Vector2[]
            {
            new Vector2(blockX + uvSize, blockY),
            new Vector2(blockX, blockY),
            new Vector2(blockX + uvSize, blockY + uvSize),
            new Vector2(blockX, blockY + uvSize)
            };
        }

        uvs.AddRange(quadUVs);

        // Add triangles
        triangles.Add(vertIndex);
        triangles.Add(vertIndex + 2);
        triangles.Add(vertIndex + 1);
        triangles.Add(vertIndex + 2);
        triangles.Add(vertIndex + 3);
        triangles.Add(vertIndex + 1);
    }
}

