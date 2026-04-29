using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    [SerializeField] public GameObject prefab;
    [SerializeField] public int poolSize = 30;
    private Queue<GameObject> pool = new Queue<GameObject>();

    void Start()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    public GameObject GetFromPool()
    {
        if (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            obj.SetActive(true);
            return obj;
        }
        return Instantiate(prefab);
    }

    public void ReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
        pool.Enqueue(obj);
    }
}
