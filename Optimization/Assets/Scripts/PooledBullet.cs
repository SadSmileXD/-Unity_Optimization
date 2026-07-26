using UnityEngine;
using UnityEngine.Pool;

public class PooledBullet : MonoBehaviour
{
    private IObjectPool<GameObject> pool;
    private float timer;

    public void SetPool(IObjectPool<GameObject> pool)
    {
        this.pool = pool;
    }

    private void OnEnable()
    {
        timer = 0f; // 활성화될 때 타이머 리셋
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= 1f) // 1초 뒤에 Destroy 대신 Release(반환)
        {
            pool.Release(gameObject);
        }
    }
}
