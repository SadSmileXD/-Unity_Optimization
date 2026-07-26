using UnityEngine;
using UnityEngine.Pool; // Unity 내장 풀링 라이브러리
public class Player : MonoBehaviour
{
    public GameObject bulletPrefab;
    public int count;

    private IObjectPool<GameObject> bulletPool;
    private void Awake()
    {
        // 풀 초기화 (생성, 가져올때, 반환할때, 파괴할때 동작 정의)
        bulletPool = new ObjectPool<GameObject>(
            createFunc: CreateBullet,
            actionOnGet: OnGetBullet,
            actionOnRelease: OnReleaseBullet,
            actionOnDestroy: OnDestroyBullet,
            defaultCapacity: 20, // 초기 미리 생성해둘 용량
            maxSize: 200         // 풀 최대 저장 개수
        );
    }

    private void Update()
    {
        for (int i = 0; i < count; i++)
        {
            Fire();
        }
    }

    private void Fire()
    {
        // 1. Instantiate 대신 풀에서 가져오기 (GC.Alloc 발생 안 함)
        bulletPool.Get();
    }

    // --- ObjectPool 콜백 함수들 ---

    private GameObject CreateBullet()
    {
        GameObject bullet = Instantiate(bulletPrefab);
        // 총알 자체에 풀 반환 스크립트가 없어도 작동하도록 연동
        var pooledBullet = bullet.AddComponent<PooledBullet>();
        pooledBullet.SetPool(bulletPool);
        return bullet;
    }

    private void OnGetBullet(GameObject bullet)
    {
        bullet.SetActive(true);
    }

    private void OnReleaseBullet(GameObject bullet)
    {
        bullet.SetActive(false);
    }

    private void OnDestroyBullet(GameObject bullet)
    {
        Destroy(bullet);
    }
}
// --- 총알이 1초 뒤에 자동으로 풀로 돌아가도록 만드는 보조 클래스 ---
