using UnityEngine;
using UnityEditor;

public class CreateRedObject
{
    [MenuItem("GameObject/Create Red Object", false, 0)]
    static void Create()
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = "RedObject";
        obj.transform.position = Vector3.zero;

        Renderer rend = obj.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Standard"));
        rend.material.color = Color.red;

        Selection.activeGameObject = obj;
    }
}
