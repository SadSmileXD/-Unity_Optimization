# 1. GC Alloc / Instantiate 반복 → Object Pooling

### 상황

총알을 발사하는 슈팅 게임을 만든다.
```
플레이어
 └─ 1초에 20발 총알 발사
       └─ Instantiate(Bullet)
       └─ 0.5초 후 Destroy(Bullet)
```
### 사용된 코드
```csharp
using UnityEngine;

public class Player : MonoBehaviour
{
    public GameObject bulletPrefab;
    public int count;

   
    private void Update()
    {
        for (int i = 0; i < count; i++)
        {
            Fire();
        }
    }
    
    void Fire()
    {
        GameObject bullet = Instantiate(bulletPrefab);
        Destroy(bullet, 1f);
    }
}

```


![alt text](image.png)


- GC.Alloc: 힙 메모리 할당(Garbage Collection Allocation)을 기록하는 이벤트 이름입니다.  

- Calls: 100: 해당 프레임(또는 측정 구간) 동안 GC 메모리 할당 동작이 총 100번 호출되었다는 의미입니다.  

- GC Alloc: 3.9 KB: 해당 100번의 호출을 통해 힙에 새로 할당된 메모리의 총량이 3.9 KB라는 의미입니다.  

 


![alt text](image-1.png)

오브젝트 풀링결과 GC 발생이 없다.
```Csharp
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

```

```Csharp
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

```

-----


### 왜 GC Alloc이 성능에 치명적일까

#### 할당(Alloc) 자체보다, 나중에 올 청소부(GC)가 무서운 것
GC Alloc으로 힙 메모리에 가비지가 계속 쌓이다가 힙 영역이 가득 차면, Unity의 GC(가비지 컬렉터)가 동작하기 시작합니다.

- GC가 켜지면 메모리를 전체 탐색하면서 안 쓰는 데이터를 찾아서 지웁니다.

- 이 청소 작업을 하는 동안 게임의 메인 쓰레드가 멈춥니다(Stop-the-World).

- 그 결과 프레임이 순간적으로 뚝 떨어지는 프레임 드랍(Spike / 랙) 현상이 발생합니다.

즉, GC Alloc 발생 → 가비지 누적 → GC 발동 → 화면 뚝뚝 끊김으로 이어지게 됩니다.

## GC Alloc을 만드는 주범 6가지


### 매 프레임 new 생성
```
void Update()
{
    // 매 프레임 Vector3 배열이나 클래스를 새로 만들면 GC Alloc 폭발!
    int[] tempArray = new int[10]; 
}
*int는 구조체(값 타입)가 맞지만, int[](배열)는 클래스(참조 타입)
```

### 문자열 연산 (String Concatenation)
C#에서 string은 불변(Immutable) 객체라, 문자를 붙일 때마다 새로운 메모리가 할당됩니다.

```
// "Score: " 와 score.ToString()이 합쳐질 때 새로운 string 할당 발생
scoreText.text = "Score: " + score;
```

### 박싱 (Boxing)
값 타입(int, struct 등)이 참조 타입(object)으로 변환될 때 힙 메모리에 복사본이 할당됩니다.
```Csharp
int hp = 100;
object obj = hp; // int가 object로 포장되면서 GC Alloc 발생!
```
### Unity API의 배열 반환 함수들
```Csharp
void Update()
{
    // GetComponents, Physics.OverlapSphere 등 배열을 반환하는 함수는
    // 호출될 때마다 내부에서 new 배열을 만들어서 줍니다.
    var colliders = Physics.OverlapSphere(transform.position, 5f);
}
```

###  람다식/무명 메서드의 캡처(Capture)로 인한 GC Alloc  
C# 사용 시 정말 흔하게 놓치는 부분입니다. 람다식 안에서 외부에 있는 변수(지역 변수나 멤버 변수)를 참조하면, C# 컴파일러가 이를 처리하기 위해 익명 클래스 객체를 힙 메모리에 몰래 생성(new)합니다.

```Csharp

int targetDamage = 50;

// BAD: targetDamage라는 외부 변수를 캡처하므로 힙에 익명 객체가 생성됨 (GC Alloc!)
enemies.Find(e => e.damage == targetDamage);

// GOOD: 조건식을 별도 함수로 빼거나, 캡처가 없는 정적 람다/선언을 사용
```

### LINQ (Language Integrated Query)
Where, Select, OrderBy 같은 LINQ 구문은 코드를 매우 깔끔하게 만들어주지만, 내부적으로 임시 객체, 델리게이트, 박싱, Enumerator(이누메레이터)를 대량 생성합니다.

결론: Update나 주기적인 연산이 일어나는 루프 안에서는 LINQ 사용을 금지하고, 일반 for문이나 foreach문을 사용하는 것이 정석입니다.

----

### NonAlloc 계열 API 활용 (Unity API 최적화)
Unity는 배열을 새로 할당하는 함수 대신, 미리 만들어둔 배열을 재사용하는 NonAlloc 버전 API를 제공합니다.

```Csharp
// BAD: 매 호출마다 new Collider[] 배열을 반환해 GC Alloc 발생
Collider[] hitColliders = Physics.OverlapSphere(transform.position, 5f);

// GOOD: 미리 선언한 배열에 결과만 채워 넣음 (GC Alloc 0 B)
Collider[] results = new Collider[10];
int hitCount = Physics.OverlapSphereNonAlloc(transform.position, 5f, results);
```
