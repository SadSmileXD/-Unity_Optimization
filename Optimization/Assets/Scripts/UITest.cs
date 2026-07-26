
using TMPro;
using UnityEngine;
using System.Text;
public class UITest : MonoBehaviour
{
    public TextMeshProUGUI test;
    private StringBuilder sb = new StringBuilder(128);
    public int score = 1000;

    void Update()
    {
        sb.Clear();
        sb.Append("HP : ");
        sb.Append("100");
        sb.Append(" / ");
        sb.Append("1000");
        sb.Append(100);
        sb.Append(1000);
        sb.Append(10000);
        test.text = sb.ToString(); // 오직 이 줄에서만 1회 할당!
    }
}
