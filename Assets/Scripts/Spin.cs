using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spin : MonoBehaviour
{
    public float SpinSpeed = 1;
    public Material SpinMaterial;
    // Start is called before the first frame update
    void Start()
    {
        SpinMaterial.SetFloat("_Rotation", 0);
    }
    private void OnDestroy()
    {
        SpinMaterial.SetFloat("_Rotation", 0);
    }

    // Update is called once per frame
    void Update()
    {
        float Rotation = SpinMaterial.GetFloat("_Rotation");
        Rotation += SpinSpeed * Time.deltaTime;
        Rotation = Rotation % 360;
        SpinMaterial.SetFloat("_Rotation", Rotation);
    }
}
