using UnityEngine;

public class Outline : MonoBehaviour
{
    public Color color = Color.black;
    public float thickness = 0.03f;

    void Start()
    {
        var outlineObj = new GameObject("Outline");
        outlineObj.transform.SetParent(transform, false);

        var mf = outlineObj.AddComponent<MeshFilter>();
        mf.sharedMesh = GetComponent<MeshFilter>().sharedMesh;

        var mr = outlineObj.AddComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Custom/Outline"));
        mr.material.SetColor("_Color", color);
        mr.material.SetFloat("_Thickness", thickness);
    }
}
